using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Newtonsoft.Json;
using Polly;
using Serilog.Events;
using SerilogTimings.Extensions;
using ILogger = Serilog.ILogger;

namespace Ditto
{
    /// <summary>
    /// A checkpoint manager that writes Consumer Checkpoints to Event Store streams in batches.
    /// Each consumer will write to its own Checkpoint stream named "Ditto_{ConsumerName}_Checkpoint"
    /// </summary>
    public class EventStoreCheckpointManager : ICheckpointManager
    {
        private readonly Policy _loadPolicy;
        private readonly IEventStoreConnection _eventStoreConnection;
        private readonly int _checkpointSavingInterval;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, long> _checkpoints = new ConcurrentDictionary<string, long>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly Dictionary<string, long> _storedCheckpoints = new Dictionary<string, long>();
        private readonly Task _checkpointSavingTask;

        /// <summary>
        /// Creates a new <see cref="EventStoreCheckpointManager"/> instance
        /// </summary>
        /// <param name="eventStoreConnection">The event store connection used to write checkpoints</param>
        /// <param name="logger"></param>
        public EventStoreCheckpointManager(
            IEventStoreConnection eventStoreConnection, AppSettings settings, ILogger logger)
        {
            _eventStoreConnection = eventStoreConnection ?? throw new ArgumentNullException(nameof(eventStoreConnection));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (settings?.CheckpointSavingInterval <= 0) throw new ArgumentOutOfRangeException(nameof(settings.CheckpointSavingInterval));
            
            _checkpointSavingInterval = settings.CheckpointSavingInterval;

            _loadPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(settings.CheckpointManagerRetryCount,
                    i => TimeSpan.FromMilliseconds(settings.CheckpointManagerRetryInterval), (ex, ts) => {
                        _logger.Error(ex, "Error reading checkpoint stream");
                    });

            _checkpointSavingTask = Task.Run(SaveCheckpointsBatchedAsync);
        }

        /// <summary>
        /// Loads the checkpoint for the specified consumer
        /// </summary>
        /// <param name="consumerName">The name of the consumer</param>
        /// <returns>The checkpoint position if a checkpoint exists or null if the checkpoint does not exist or position has been reset (-1).</returns>
        public async Task<long?> LoadCheckpointAsync(string consumerName)
        {
            if (string.IsNullOrWhiteSpace(consumerName))
                throw new ArgumentException("Consumer name is required", nameof(consumerName));

            async Task<long?> loadCheckpoint() 
            { 
                RecordedEvent recordedEvent;
                using (_logger.OperationAt(LogEventLevel.Debug).Time("Loading Checkpoint from Event Store for {Consumer}", consumerName))
                {
                    recordedEvent = await GetLastEvent(GetCheckpointStreamName(consumerName));
                }

                if (recordedEvent == null)
                    return null;

                var checkpoint = DeserializeCheckpoint(recordedEvent).LastEventProcessed;
                
                // To support resetting the checkpoint we need to treat negative events as null
                if (checkpoint < 0)
                    return null;

                return checkpoint;
            }

            // Despite queuing the stream read, we've seen cases where the read will fail
            // on reconnection so we execute using a retry policy
            var executeResult = await _loadPolicy.ExecuteAndCaptureAsync(loadCheckpoint);
            return executeResult.Result;
        }

        /// <summary>
        /// Saves the checkpoint for the specified consumer
        /// </summary>
        /// <param name="consumerName">The name of the consumer</param>
        /// <param name="checkpoint">The position of the last event processed</param>
        /// <returns>A task that completes when the checkpoint has been written to the internal cache</returns>
        public async Task SaveCheckpointAsync(string consumerName, long checkpoint)
        {
            if (string.IsNullOrWhiteSpace(consumerName))
                throw new ArgumentException("Consumer name is required", nameof(consumerName));

            _checkpoints.AddOrUpdate(consumerName, checkpoint, (s, l) => checkpoint);

            var streamName = GetCheckpointStreamName(consumerName);

            if (checkpoint == 0) // Stream created
                await SetCheckpointStreamMaxCount(streamName, checkpoint);
        }

        /// <summary>
        /// Stops the batched checkpoint task and writes any pending checkpoints
        /// </summary>
        public void Dispose()
        {
            // Cancel the thread running
            _cancellationTokenSource.Cancel();
            _checkpointSavingTask.GetAwaiter().GetResult();

            // Save all the checkpoints since the last thread run
            SaveCheckpointsAsync().GetAwaiter().GetResult();
        }

        private async Task SetCheckpointStreamMaxCount(string streamName, long eventNumber)
        {
            var metadata = StreamMetadata.Build()
                            .SetMaxCount(10);

            using (_logger.OperationAt(LogEventLevel.Debug)
                .Time("Setting max count for Event Store Stream {Stream}", streamName))
            {
                await _eventStoreConnection.SetStreamMetadataAsync(
                    streamName, ExpectedVersion.Any, metadata);
            }
        }

        private async Task<RecordedEvent> GetLastEvent(string streamName)
        {
            var slice = await _eventStoreConnection.ReadStreamEventsBackwardAsync(
                    streamName,
                    StreamPosition.End,
                    1,
                    false);

            if (slice.Status == SliceReadStatus.StreamNotFound || slice.Events.Length == 0)
                return null;

            return slice.Events[0].Event;
        }

        private async Task SaveCheckpointsBatchedAsync()
        {
            do
            {
                try
                {
                    await SaveCheckpointsAsync();
                    await Task.Delay(_checkpointSavingInterval, _cancellationTokenSource.Token);
                }
                catch (TaskCanceledException) {}
            } while (!_cancellationTokenSource.IsCancellationRequested);

            _logger.Debug("Stopping Checkpoint Task");
        }

        private async Task SaveCheckpointsAsync()
        {
            foreach (var checkpoint in _checkpoints)
            {
                try
                {
                    var newCheckpointValue = checkpoint.Value;

                    // Skip the update if we've already saved the last checkpoint
                    if (_storedCheckpoints.TryGetValue(checkpoint.Key, out long lastStored) && lastStored == newCheckpointValue)
                        continue;

                    _storedCheckpoints[checkpoint.Key] = newCheckpointValue;

                    var streamName = GetCheckpointStreamName(checkpoint.Key);
                    using (_logger.OperationAt(LogEventLevel.Information)
                        .Time("Saving Event Store Checkpoint #{Checkpoint} for {Consumer} to {Stream}", newCheckpointValue, checkpoint.Key, streamName))
                    {
                        await _eventStoreConnection.AppendToStreamAsync(
                            streamName,
                            ExpectedVersion.Any,
                            CreateEvent(new Checkpoint { LastEventProcessed = newCheckpointValue }));
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error saving Event Store Checkpoint for {Consumer}", checkpoint.Key);
                }
            }
        }

        private static string GetCheckpointStreamName(string consumerName)
        {
            return "Ditto_" + consumerName + "_Checkpoint";
        }

        private static Checkpoint DeserializeCheckpoint(RecordedEvent e)
        {
            var json = Encoding.UTF8.GetString(e.Data);
            return JsonConvert.DeserializeObject(json, typeof(Checkpoint)) as Checkpoint;
        }

        private static EventData CreateEvent(Checkpoint eventCheckpoint)
        {
            var json = JsonConvert.SerializeObject(eventCheckpoint);

            return new EventData(
                Guid.NewGuid(),
                nameof(Checkpoint),
                true,
                Encoding.UTF8.GetBytes(json),
                null
            );
        }

        private class Checkpoint
        {
            public long LastEventProcessed { get; set; }
        }
    }
}