using System.Threading.Tasks;

namespace Ditto.Core
{
    public interface IConsumerManager
    {
        /// <summary>
        /// Starts each consumer registered with the manager
        /// </summary>
        /// <returns>A task that completes when all consumers have started</returns>
        Task StartAsync();

        /// <summary>
        /// Stops all running consumers
        /// </summary>
        /// <returns>A task that completes when all consumers have stopped</returns>
        Task StopAsync();
    }
}