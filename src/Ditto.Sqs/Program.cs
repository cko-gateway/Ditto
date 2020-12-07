using System;
using System.IO;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Ditto.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prometheus;
using Serilog;

namespace Ditto.Sqs
{
    /// <summary>
    /// Application bootstrapper
    /// </summary>
    class Program
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly AutoResetEvent _closing = new AutoResetEvent(false);
        private IServiceProvider _serviceProvider;
        private AppService _service;
        private MetricServer _metricsServer;

        /// <summary>
        /// Entry point for the application
        /// </summary>
        /// <returns>A task that completes when the application ends.</returns>
        static async Task Main()
        {
            var configuration = BuildConfiguration();
            var logger = CreateLogger(configuration);

            await new Program(configuration, logger).RunAsync();
        }

        /// <summary>
        /// Initialises the application with the specified configuration and logger
        /// </summary>
        /// <param name="configuration">The application configuration</param>
        /// <param name="logger">The application logger</param>
        public Program(IConfiguration configuration, ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Runs the application
        /// </summary>
        /// <returns></returns>
        private async Task RunAsync()
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            AssemblyLoadContext.Default.Unloading += OnShutdown;
            Console.CancelKeyPress += OnCancelKeyPress;

            try
            {
                _logger.Information("Starting Ditto. Press Ctrl+C to exit.");

                // Log the configuration if running in Development mode
                if (IsDevelopmentEnvironment())
                    _logger.Information(_configuration.Dump());

                _serviceProvider = AppRegistry.Register(_configuration, new ServiceCollection()).BuildServiceProvider();
                _service = _serviceProvider.GetService<AppService>();
                await _service.StartAsync();

                MetricsSettings metrics = _configuration.Bind<MetricsSettings>("Metrics");

                if (metrics.Enabled)
                {
                    _logger.Information("Starting metrics server at {MetricsUrl}:{MetricsPort}", metrics.Path, metrics.Port);
                    _metricsServer = new MetricServer(port: metrics.Port, url: metrics.Path);
                    _metricsServer.Start();
                }

                // Block until an exit signal is detected
                _closing.WaitOne();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error starting Ditto");
            }
        }

        private void OnShutdown(AssemblyLoadContext context)
        {
            _logger.Information("Shutting down Ditto");

            try
            {
                _service?.StopAsync().GetAwaiter().GetResult();
                _metricsServer?.Stop();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error shutting down Ditto cleanly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true;
            _closing.Set();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs ex)
        {
            Console.WriteLine(ex.ExceptionObject.ToString());
            Environment.Exit(1);
        }

        /// <summary>
        /// Creates a logger using the application configuration
        /// </summary>
        /// <param name="configuration">The configuration to read from</param>
        /// <returns>An logger instance</returns>
        private static ILogger CreateLogger(IConfiguration configuration)
        {
            var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            Log.Logger = logger;
            return logger.ForContext<Program>();
        }

        /// <summary>
        /// Builds a configuration from file and event variable sources
        /// </summary>
        /// <returns>The built configuration</returns>
        private static IConfiguration BuildConfiguration()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{CurrentEnvironment}.json", optional: true)
                .AddEnvironmentVariables(prefix: "Ditto_")
                .Build();
        }

        /// <summary>
        /// Determines whether the application is running in Development mode
        /// </summary>
        /// <returns>True if running in Development, otherwise False</returns>
        private static bool IsDevelopmentEnvironment() => "Development".Equals(CurrentEnvironment, StringComparison.OrdinalIgnoreCase);

        private static string CurrentEnvironment => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    }
}
