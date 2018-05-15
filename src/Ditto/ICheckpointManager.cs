using System;
using System.Threading.Tasks;

namespace Ditto
{
    /// <summary>
    /// Defines an interface for classes that manage the checkpoints (last processed event numbers)
    /// for Event Store consumers
    /// </summary>
    public interface ICheckpointManager : IDisposable
    {
        /// <summary>
        /// Loads the checkpoint for the specified consumer
        /// </summary>
        /// <param name="consumerName">The name of the consumer</param>
        /// <returns>The checkpoint position if a checkpoint exists, otherwise Null.</returns>
        Task<long?> LoadCheckpointAsync(string consumerName);
        
        /// <summary>
        /// Saves the checkpoint for the specified consumer
        /// </summary>
        /// <param name="consumerName">The name of the consumer</param>
        /// <param name="checkpoint">The position of the last event processed</param>
        /// <returns>A task that completes when the checkpoint has been written</returns>
        Task SaveCheckpointAsync(string consumerName, long checkpoint);
    }
}