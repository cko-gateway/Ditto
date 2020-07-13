using System.Threading.Tasks;
using Ditto.Core;
using Serilog;

namespace Ditto.Kinesis
{
    /// <summary>
    /// The entry point for your application logic
    /// </summary>
    public class AppService
    {
        private readonly ILogger _logger;
        private readonly IConsumerManager _consumerManager;

        /// <summary>
        /// Creates a new <see cref="AppSettings"/> instance.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="consumerManager"></param>
        public AppService(ILogger logger, IConsumerManager consumerManager)
        {
            _logger = logger?.ForContext<AppService>() ?? throw new System.ArgumentNullException(nameof(logger));
            _consumerManager = consumerManager ?? throw new System.ArgumentNullException(nameof(consumerManager));
        }

        /// <summary>
        /// Called when the application first starts
        /// </summary>
        /// <returns>A Task that completes when the application is successfully started</returns>
        public async Task StartAsync()
        {
            _logger.Information($"Starting {nameof(AppService)}");
            await _consumerManager.StartAsync();
        }

        /// <summary>
        /// Called when the application is stopping
        /// </summary>
        /// <returns>A task that completes when the application is successfully stopped</returns>
        public async Task StopAsync()
        {
            _logger.Information($"Stopping {nameof(AppService)}");
            await _consumerManager.StopAsync();
        }
    }
}