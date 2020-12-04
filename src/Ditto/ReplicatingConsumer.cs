using System;
using System.Threading.Tasks;
using Ditto.Core;
using EventStore.ClientAPI;
using Prometheus;
using Serilog.Events;
using SerilogTimings.Extensions;

namespace Ditto
{
    /// <summary>
    /// Stream consumer that replicates to the destination event store
    /// </summary>
    public class ReplicatingConsumer : ICompetingConsumer
    {
        private readonly IEventStoreConnection _connection;
        private readonly Serilog.ILogger _logger;
        private readonly DittoSettings _settings;
        private readonly StreamMetadata _streamMetadata;
        private readonly bool _ttl;

        public ReplicatingConsumer(
            IEventStoreConnection connection, Serilog.ILogger logger, DittoSettings settings, string streamName, string groupName)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            StreamName = streamName ?? throw new ArgumentNullException(nameof(streamName));
            GroupName = groupName ?? throw new ArgumentNullException(nameof(groupName));

            if (_settings.TimeToLive.GetValueOrDefault().TotalMilliseconds > 0)
            {
                _streamMetadata = StreamMetadata.Build().SetMaxAge(_settings.TimeToLive.Value);
                _ttl = true;
            }
        }

        public string StreamName { get; }
        public string GroupName { get; }
        public bool CanConsume(string eventType) => true;

        public async Task ConsumeAsync(string eventType, ResolvedEvent resolvedEvent)
        {
            if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("Event type required", nameof(eventType));

            if (_settings.EventNumberToStopAt.HasValue && resolvedEvent.OriginalEventNumber > _settings.EventNumberToStopAt.Value)
            {
                _logger.Error("Please stop ditto as it has reached the event number to stop at. Skipping processing. " +
                              "EventNumber to stop at {eventNumber}. ResolvedEventNumber: {resolvedEventVersion}. StreamId: {streamId}",
                    _settings.EventNumberToStopAt, resolvedEvent.OriginalEventNumber, resolvedEvent.Event.EventStreamId);
                return;
            }

            if (_settings.ReadOnly)
            {
                _logger.Debug("Received {EventType} #{EventNumber} from {StreamName} (Original Event: #{OriginalEventNumber})",
                    resolvedEvent.Event.EventType,
                    resolvedEvent.Event.EventNumber,
                    resolvedEvent.Event.EventStreamId,
                    resolvedEvent.OriginalEventNumber
                );

                return;
            }
            
            
            var eventData = new EventData(
                resolvedEvent.Event.EventId,
                resolvedEvent.Event.EventType,
                true,
                resolvedEvent.Event.Data,
                resolvedEvent.Event.Metadata
            );

            WriteResult result = default;

            using (_logger.OperationAt(LogEventLevel.Debug).Time("Replicating {EventType} #{EventNumber} from {StreamName} (Original Event: #{OriginalEventNumber})",
                resolvedEvent.Event.EventType,
                resolvedEvent.Event.EventNumber,
                resolvedEvent.Event.EventStreamId,
                resolvedEvent.OriginalEventNumber))
            using (DittoMetrics.IODuration.WithIOLabels("eventstore", "ditto-destination", "append_to_stream").NewTimer())
            {
                result = await _connection.AppendToStreamAsync(
                    resolvedEvent.Event.EventStreamId,
                    _settings.SkipVersionCheck ? ExpectedVersion.Any : resolvedEvent.Event.EventNumber - 1,
                    eventData
                );
            }

            if (_ttl && result.NextExpectedVersion == 0) // New stream created
                await SetStreamMetadataAsync(resolvedEvent.Event.EventStreamId);

            if (_settings.ReplicationThrottleInterval.GetValueOrDefault() > 0)
                await Task.Delay(_settings.ReplicationThrottleInterval.Value);
        }

        private async Task SetStreamMetadataAsync(string stream)
        {
            using (_logger.OperationAt(LogEventLevel.Debug).Time("Setting TTL on stream {StreamName}", stream))
            using (DittoMetrics.IODuration.WithIOLabels("eventstore", "ditto-destination", "set_stream_metadata").NewTimer())
            {
                await _connection.SetStreamMetadataAsync(stream, ExpectedVersion.Any, _streamMetadata);
            }
        }
    }
}
