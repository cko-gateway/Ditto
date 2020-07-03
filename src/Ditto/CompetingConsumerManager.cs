using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI;

namespace Ditto
{
    /// <summary>
    /// Manages the lifetime of multiple Event Store competing consumers
    /// </summary>
    public class CompetingConsumerManager : IConsumerManager
    {
        private const int DefaultBufferSize = 10;
        private const int SubscriptionStopTimeoutSeconds = 10;
        private static SubscriptionDropReason[] SkipStopReasons = new[] { SubscriptionDropReason.ConnectionClosed, SubscriptionDropReason.UserInitiated };

        private readonly IEventStoreConnection _connection;
        private readonly AppSettings _settings;
        private readonly Serilog.ILogger _logger;
        private readonly List<RunningConsumer> _consumers;
        private bool _stopping;

        /// <summary>
        /// Creates a new <see cref="CompetingConsumerManager"/> instance.
        /// </summary>
        /// <param name="connection">The event store connection to use for subscribing to streams</param>
        /// <param name="consumers">The consumers to start</param>
        /// <param name="settings">The application settings</param>
        /// <param name="logger">Application logger</param>
        public CompetingConsumerManager(
            IEventStoreConnection connection,
            IEnumerable<ICompetingConsumer> consumers,
            AppSettings settings,
            Serilog.ILogger logger)
        {
            _connection = connection ?? throw new System.ArgumentNullException(nameof(connection));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (consumers == null)
                throw new ArgumentNullException(nameof(consumers));

            _consumers = consumers
                .Select(consumer => new RunningConsumer { Consumer = consumer }).ToList();
        }

        /// <summary>
        /// Starts each consumer registered with the manager
        /// </summary>
        /// <returns>A task that completes when all consumers have started</returns>
        public Task StartAsync()
        {
            _stopping = false;
            var tasks = new List<Task>();

            foreach (var consumer in _consumers)
            {
                tasks.Add(StartConsumerAsync(consumer));
            }

            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Stops all running consumers
        /// </summary>
        /// <returns>A task that completes when all consumers have stopped</returns>
        public Task StopAsync()
        {
            _stopping = true;

            foreach (var consumer in _consumers)
            {
                StopConsumer(consumer);
            }

            _connection.Dispose();

            return Task.CompletedTask;
        }

        private async Task StartConsumerAsync(RunningConsumer runningConsumer)
        {
            var consumerId = GetConsumerId(runningConsumer.Consumer);

            _logger.Information("Starting {Consumer} subscribed to {StreamName} in group {GroupName}",
                runningConsumer.Consumer.GetType().Name, runningConsumer.Consumer.StreamName, runningConsumer.Consumer.GroupName);

            var settings = CatchUpSubscriptionSettings.Default;

            runningConsumer.Subscription = await _connection.ConnectToPersistentSubscriptionAsync(
                runningConsumer.Consumer.StreamName,
                runningConsumer.Consumer.GroupName,
                OnEventAppeared(runningConsumer.Consumer),
                OnSubscriptionDropped(runningConsumer),
                autoAck: false,
                bufferSize: _settings.PersistentSubscriptionBufferSize.GetValueOrDefault(DefaultBufferSize)
            );
        }

        private void StopConsumer(RunningConsumer runningConsumer)
        {
            if (runningConsumer.Subscription == null)
                return;

            _logger.Information("Stopping {Consumer} subscribed to {StreamName} in group {GroupName}",
                runningConsumer.Consumer.GetType().Name, runningConsumer.Consumer.StreamName, runningConsumer.Consumer.GroupName);

            runningConsumer.Subscription.Stop(TimeSpan.FromSeconds(10));
        }

        private Func<EventStorePersistentSubscriptionBase, ResolvedEvent, Task> OnEventAppeared(ICompetingConsumer consumer)
        {
            return async (sub, e) =>
            {               
                DittoMetrics.ReceivedEvents.WithConsumerLabels(consumer).Inc();
                DittoMetrics.CurrentEvent.WithConsumerLabels(consumer).Set(e.OriginalEventNumber);
                
                if (!e.IsResolved) // Handle deleted streams
                {
                    _logger.Information("Event #{EventNumber} from {StreamName} is not resolved. Skipping", e.OriginalEventNumber, consumer.StreamName);
                    
                    // log
                    DittoMetrics.UnresolvedEvents.WithConsumerLabels(consumer).Inc();
                    sub.Fail(e, PersistentSubscriptionNakEventAction.Skip, "Unresolved Event");
                    return;
                }

                double delaySeconds = (DateTime.UtcNow - e.Event.Created).TotalSeconds;

                DittoMetrics.ReplicationLatency.WithConsumerLabels(consumer).Observe(delaySeconds);

                if (!consumer.CanConsume(e.Event.EventType))
                {
                    _logger.Information("Unable to consume {EventType} #{EventNumber} from {StreamName}. Skipping", e.Event.EventType, e.OriginalEventNumber, consumer.StreamName);

                    DittoMetrics.SkippedEvents.WithConsumerLabels(consumer).Inc();
                    sub.Fail(e, PersistentSubscriptionNakEventAction.Skip, "Cannot consume");
                    return;
                }

                string consumerId = GetConsumerId(consumer);

                try
                {
                    await consumer.ConsumeAsync(e.Event.EventType, e);
                    DittoMetrics.ProcessedEvents.WithConsumerLabels(consumer).Inc();
                    sub.Acknowledge(e);
                }
                catch (Exception ex)
                {
                    DittoMetrics.FailedEvents.WithConsumerLabels(consumer).Inc();
                    
                    _logger.Error(ex, "{Consumer} failed to handle {EventType} #{EventNumber} from {StreamName} in group {GroupName}",
                        consumerId, e.Event.EventType, e.OriginalEventNumber, consumer.StreamName, consumer.GroupName);

                    sub.Fail(e, PersistentSubscriptionNakEventAction.Retry, ex.Message);
                }
            };
        }

        private Action<EventStorePersistentSubscriptionBase, SubscriptionDropReason, Exception> OnSubscriptionDropped(RunningConsumer runningConsumer)
        {
            return (sub, reason, exception) =>
            {
                if (!SkipStopReasons.Contains(reason))
                {
                    try
                    {
                        sub.Stop(TimeSpan.FromSeconds(SubscriptionStopTimeoutSeconds));
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error stopping persistent subscription to {StreamName} in group {GroupName} with reason {Reason}", 
                            runningConsumer.Consumer.StreamName, runningConsumer.Consumer.GroupName, reason.ToString());
                    }
                }

                if (!_stopping)
                {
                    _logger.Error(exception, "{Consumer} dropped subscription to {StreamName} in group {GroupName} with reason {Reason}",
                        GetConsumerId(runningConsumer.Consumer), runningConsumer.Consumer.StreamName, runningConsumer.Consumer.GroupName, reason.ToString());

                    if (reason == SubscriptionDropReason.EventHandlerException)
                        _logger.Fatal(exception, "Subscription to {StreamName} in group {GroupName} has been dropped due to an exception. Please restart", runningConsumer.Consumer.StreamName, runningConsumer.Consumer.GroupName);
                    else
                        StartConsumerAsync(runningConsumer).GetAwaiter().GetResult();
                }
            };
        }

        private string GetConsumerId(ICompetingConsumer consumer)
        {
            return consumer.GetType().Name;
        }

        private class RunningConsumer
        {
            public ICompetingConsumer Consumer { get; set; }
            public EventStorePersistentSubscriptionBase Subscription { get; set; }
        }
    }
}