using System.Threading;
using EventStore.ClientAPI;
using SerilogTimings.Extensions;

namespace Ditto
{
    public class ReplicatingConsumer : IConsumer
    {
        private readonly IEventStoreConnection _connection;
        private readonly Serilog.ILogger _logger;
        private readonly AppSettings _settings;

        public ReplicatingConsumer(
            IEventStoreConnection connection, Serilog.ILogger logger, AppSettings settings, string streamName)
        {
            _connection = connection ?? throw new System.ArgumentNullException(nameof(connection));
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            _settings = settings ?? throw new System.ArgumentNullException(nameof(settings));
            StreamName = streamName ?? throw new System.ArgumentNullException(nameof(streamName));
        }

        public string StreamName { get; }

        public bool CanConsume(string eventType)
        {
            return true;
        }

        public void Consume(string eventType, ResolvedEvent e)
        {
            var eventData = new EventData(
                e.Event.EventId,
                e.Event.EventType,
                true,
                e.Event.Data,
                e.Event.Metadata
            );

            using (_logger.TimeOperation("Replicating {EventType} #{EventNumber} from {StreamName}", 
                e.Event.EventType, 
                e.Event.EventNumber, 
                e.Event.EventStreamId))
                {
                    _connection.AppendToStreamAsync(e.Event.EventStreamId, e.Event.EventNumber - 1, eventData).GetAwaiter().GetResult();
                }

            if (_settings.ReplicationThrottleInterval.GetValueOrDefault() > 0)
                Thread.Sleep(_settings.ReplicationThrottleInterval.Value);
        }

        public override string ToString()
        {
            var normalised = StreamName
                .Replace("$", "")
                .Replace("-", "_"); // To avoid category stream breaking down checkpoint streams

            return $"ReplicatingConsumer_{normalised}";
        }
    }
}