using System.Threading.Tasks;
using Serilog;

namespace Ditto.Sqs.Consumer
{
    /// <summary>
    /// The entry point for your application logic
    /// </summary>
    public class AppService
    {
        private readonly ILogger _logger;
        
        /// <summary>
        /// Creates a new <see cref="AppSettings"/> instance.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="consumerManager"></param>
        public AppService(ILogger logger)
        {
            _logger = logger?.ForContext<AppService>() ?? throw new System.ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Called when the application first starts
        /// </summary>
        /// <returns>A Task that completes when the application is successfully started</returns>
        public Task StartAsync()
        {
            _logger.Information($"Starting {nameof(AppService)}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the application is stopping
        /// </summary>
        /// <returns>A task that completes when the application is successfully stopped</returns>
        public Task StopAsync()
        {
            _logger.Information($"Stopping {nameof(AppService)}");
            return Task.CompletedTask;
        }
    }
}