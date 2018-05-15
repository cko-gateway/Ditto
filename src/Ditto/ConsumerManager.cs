using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI;

namespace Ditto
{
    /// <summary>
    /// Manages the lifetime of multiple Event Store stream consumers
    /// </summary>
    public class ConsumerManager : IConsumerManager
    {
        private readonly IEventStoreConnection _connection;
        private readonly ICheckpointManager _checkpointManager;
        private readonly Serilog.ILogger _logger;
        private readonly List<RunningConsumer> _consumers;
        private bool _stopping;

        /// <summary>
        /// Creates a new <see cref="ConsumerManager"/> instance.
        /// </summary>
        /// <param name="connection">The event store connection to use for subscribing to streams</param>
        /// <param name="checkpointManager">The checkpoint manager used for loading and saving consumer checkpoints</param>
        /// <param name="consumers">The consumers to start</param>
        /// <param name="logger">Application logger</param>
        public ConsumerManager(
            IEventStoreConnection connection,
            ICheckpointManager checkpointManager,
            IEnumerable<IConsumer> consumers,
            Serilog.ILogger logger)
        {
            _connection = connection ?? throw new System.ArgumentNullException(nameof(connection));
            _checkpointManager = checkpointManager ?? throw new System.ArgumentNullException(nameof(checkpointManager));
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

            // Ideally we would wait for the tasks to complete with Task.WhenAll
            // but since the EventStoreCheckpointManager does not return until a connection is available
            // we do not wait for the subscription to start
            return Task.CompletedTask;
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

            _checkpointManager.Dispose();
            _connection.Dispose();

            return Task.CompletedTask;
        }

        private async Task StartConsumerAsync(RunningConsumer runningConsumer)
        {           
            var consumerId = GetConsumerId(runningConsumer.Consumer);
            long? checkpoint = await _checkpointManager.LoadCheckpointAsync(consumerId);
            
            _logger.Information("Starting {Consumer} subscribed to {StreamName} from Checkpoint #{Checkpoint}",
                runningConsumer.Consumer.GetType().Name, runningConsumer.Consumer.StreamName, checkpoint);

            var settings = CatchUpSubscriptionSettings.Default;
            
            runningConsumer.Subscription = _connection.SubscribeToStreamFrom(
                runningConsumer.Consumer.StreamName,
                checkpoint,
                settings,
                OnEventAppeared(runningConsumer.Consumer),
                OnLiveProcessingStarted(runningConsumer.Consumer),
                OnSubscriptionDropped(runningConsumer));
        }

        private void StopConsumer(RunningConsumer runningProjection)
        {
            if (runningProjection.Subscription == null)
                return;
            
            _logger.Information("Stopping {Consumer} subscribed to {StreamName}",
                runningProjection.Consumer.GetType().Name, runningProjection.Consumer.StreamName);
            
            runningProjection.Subscription.Stop();
        }

        private Action<EventStoreCatchUpSubscription, ResolvedEvent> OnEventAppeared(IConsumer consumer)
        {
            return (sub, e) =>
            {
                if (!consumer.CanConsume(e.Event.EventType))
                    return;

                var consumerId = GetConsumerId(consumer);

                try
                {
                    consumer.Consume(e.Event.EventType, e);
                    _checkpointManager.SaveCheckpointAsync(consumerId, e.OriginalEventNumber).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "{Consumer} failed to handle {EventType} #{EventNumber} from {StreamName}",
                        consumerId, e.Event.EventType, e.OriginalEventNumber, consumer.StreamName);

                    throw;
                }
            };
        }

        private Action<EventStoreCatchUpSubscription> OnLiveProcessingStarted(IConsumer consumer)
        {
            return sub
                => _logger.Information("{Consumer} has caught up, now processing live", GetConsumerId(consumer));
        }

        private Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> OnSubscriptionDropped(RunningConsumer runningConsumer)
        {
            return (sub, reason, exception) => {

                sub.Stop();
                
                if (!_stopping) {
                    _logger.Error(exception, "{Consumer} dropped subscription to {StreamName} with reason {Reason}",
                        GetConsumerId(runningConsumer.Consumer), runningConsumer.Consumer.StreamName, reason.ToString());
                    
                    StartConsumerAsync(runningConsumer).GetAwaiter().GetResult();
                }
            };
        }

        private string GetConsumerId(IConsumer consumer)
        {
            //return consumer.GetType().Name;
            return consumer.ToString();
        }

        private class RunningConsumer
        {
            public IConsumer Consumer { get; set; }
            public EventStoreCatchUpSubscription Subscription { get; set; }
        }
    }
}