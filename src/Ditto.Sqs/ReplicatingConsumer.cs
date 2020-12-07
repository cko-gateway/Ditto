using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Auth.AccessControlPolicy;
using Amazon.SQS;
using Amazon.SQS.Model;
using Ditto.Core;
using EventStore.ClientAPI;
using Newtonsoft.Json;
using Prometheus;
using Serilog.Context;
using Serilog.Events;
using SerilogTimings.Extensions;
using ILogger = Serilog.ILogger;

namespace Ditto.Sqs
{
    /// <summary>
    /// Stream consumer that replicates to the destination event store
    /// </summary>
    public class ReplicatingConsumer : ICompetingConsumer
    {
        private readonly IAmazonSQS _sqs;
        private readonly SqsSettings _sqsSettings;
        private readonly Serilog.ILogger _logger;
        private readonly DittoSettings _dittoSettings;

        public ReplicatingConsumer(IAmazonSQS sqs, SqsSettings sqsSettings, DittoSettings dittoSettings, ILogger logger, string streamName, string groupName)
        {
            _sqs = sqs ?? throw new ArgumentNullException(nameof(sqs));
            _sqsSettings = sqsSettings ?? throw new ArgumentNullException(nameof(sqsSettings));
            _dittoSettings = dittoSettings ?? throw new ArgumentNullException(nameof(dittoSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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

            var request = new SendMessageRequest
            {
                QueueUrl = _sqsSettings.QueueUrl,
                MessageBody = CreateDataStream(resolvedEvent),
            };
         
           using (_logger.OperationAt(LogEventLevel.Debug).Time("Replicating {EventType} #{EventNumber} from {StreamName} (Original Event: #{OriginalEventNumber}) to SQS",
               resolvedEvent.Event.EventType,
               resolvedEvent.Event.EventNumber,
               resolvedEvent.Event.EventStreamId,
               resolvedEvent.OriginalEventNumber))
           using (DittoMetrics.IODuration.WithIOLabels("sqs", _sqsSettings.QueueName, "send_message").NewTimer())
           {
              await _sqs.SendMessageAsync(request);
           }
        }

        private string CreateDataStream(ResolvedEvent resolvedEvent)
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
            return json;
            //return new MemoryStream(Encoding.UTF8.GetBytes(json));
        }

        private class EventWrapper
        {
            public EventWrapper()
            {
                ReplicatedOn = DateTime.UtcNow;
            }

            public string StreamId { get; set; }
            public Guid EventId { get; set; }
            public long EventNumber { get; set; }
            public string EventType { get; set; }
            public DateTime EventTimestamp { get; set; }
            public DateTime ReplicatedOn { get; set; }

            [JsonConverter(typeof(JsonStringConverter))]
            public string Data { get; set; }

            [JsonConverter(typeof(JsonStringConverter))]
            public string Metadata { get; set; }
        }
    }
}