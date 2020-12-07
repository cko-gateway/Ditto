using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Amazon.Kinesis;
using Amazon.Kinesis.Model;
using Ditto.Core;
using Ditto.Sqs.Consumer;
using EventStore.ClientAPI;
using Newtonsoft.Json;
using Prometheus;
using Serilog.Events;
using SerilogTimings.Extensions;
using ILogger = Serilog.ILogger;

namespace Ditto.Sqs.Consumer
{
    /// <summary>
    /// Stream consumer that replicates to the destination event store
    /// </summary>
    public class ReplicatingConsumer : ICompetingConsumer
    {
        private readonly IAmazonKinesis _kinesis;
        private readonly KinesisSettings _kinesisSettings;
        private readonly ILogger _logger;
        private readonly IEventStoreWriter _eventStoreWriter;
        private readonly DittoSettings _dittoSettings;

        public ReplicatingConsumer(IAmazonKinesis kinesis, KinesisSettings kinesisSettings, DittoSettings dittoSettings, ILogger logger, IEventStoreWriter eventStoreWriter, string streamName, string groupName)
        {
            _kinesis = kinesis ?? throw new ArgumentNullException(nameof(kinesis));
            _kinesisSettings = kinesisSettings ?? throw new ArgumentNullException(nameof(kinesisSettings));
            _dittoSettings = dittoSettings ?? throw new ArgumentNullException(nameof(dittoSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventStoreWriter = eventStoreWriter ?? throw new ArgumentNullException(nameof(eventStoreWriter));

            StreamName = streamName ?? throw new ArgumentNullException(nameof(streamName));
            GroupName = groupName ?? throw new ArgumentNullException(nameof(groupName));
        }

        public string StreamName { get; }
        public string GroupName { get; }
        public bool CanConsume(string eventType) => true;

        public async Task ConsumeAsync(string eventType, ResolvedEvent resolvedEvent)
        {
            if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("Event type required", nameof(eventType));

            if (_dittoSettings.ReadOnly)
            {
                _logger.Debug("Received {EventType} #{EventNumber} from {StreamName} (Original Event: #{OriginalEventNumber})",
                    resolvedEvent.Event.EventType,
                    resolvedEvent.Event.EventNumber,
                    resolvedEvent.Event.EventStreamId,
                    resolvedEvent.OriginalEventNumber
                );

                return;
            }

            var request = new PutRecordRequest
            {
                StreamName = _kinesisSettings.StreamName,
                PartitionKey = resolvedEvent.Event.EventStreamId,
                Data = CreateDataStream(resolvedEvent)
            };

            using (_logger.OperationAt(LogEventLevel.Debug).Time("Replicating {EventType} #{EventNumber} from {StreamName} (Original Event: #{OriginalEventNumber}) to Kinesis",
                resolvedEvent.Event.EventType,
                resolvedEvent.Event.EventNumber,
                resolvedEvent.Event.EventStreamId,
                resolvedEvent.OriginalEventNumber))
            using (DittoMetrics.IODuration.WithIOLabels("kinesis", _kinesisSettings.StreamName, "put_record").NewTimer())
            {
                await _kinesis.PutRecordAsync(request);
            }

            // Fill out
            await _eventStoreWriter.SaveAsync(null, default);

            if (_dittoSettings.ReplicationThrottleInterval.GetValueOrDefault() > 0)
                await Task.Delay(_dittoSettings.ReplicationThrottleInterval.Value);
        }

        private MemoryStream CreateDataStream(ResolvedEvent resolvedEvent)
        {
            string dataJson = default, metadataJson = default;

            if (resolvedEvent.Event.Data != null)
            {
                dataJson = Encoding.UTF8.GetString(resolvedEvent.Event.Data);
            }

            if (resolvedEvent.Event.Metadata != null)
            {
                metadataJson = Encoding.UTF8.GetString(resolvedEvent.Event.Metadata);
            }

            var wrapper = new EventWrapper
            {
                StreamId = resolvedEvent.Event.EventStreamId,
                EventId = resolvedEvent.Event.EventId,
                EventNumber = resolvedEvent.Event.EventNumber,
                EventType = resolvedEvent.Event.EventType,
                EventTimestamp = resolvedEvent.Event.Created,
                Data = dataJson,
                Metadata = metadataJson
            };

            string json = JsonConvert.SerializeObject(wrapper, Formatting.None, SerializerSettings.Default);
            return new MemoryStream(Encoding.UTF8.GetBytes(json));
        }
    }
}