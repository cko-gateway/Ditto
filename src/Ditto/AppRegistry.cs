using EventStore.ClientAPI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Ditto
{
    /// <summary>
    /// Registry of application dependencies used to configure StructureMap containers
    /// </summary>
    public class AppRegistry
    {
        public static ServiceCollection Register(IConfiguration configuration, ServiceCollection services)
        {
            services.AddSingleton<IConfiguration>(configuration);
            
            // Binds the "Settings" section from appsettings.json to AppSettings
            var settings = Bind<AppSettings>(configuration, "Settings");
            services.AddSingleton<AppSettings>(settings);
            services.AddSingleton<AppService>();

            services.AddSingleton<ILogger>(Log.Logger);

            services.AddSingleton<IEventStoreConnection>(provider 
                => ConnectionFactory.CreateEventStoreConnection(provider.GetService<ILogger>(), settings.SourceEventStoreConnectionString, "Ditto:Source"));

            services.AddSingleton<IConsumerManager, CompetingConsumerManager>();
            services.AddSingleton<ReplicatingConsumerFactory>();

            // Register replicating consumers
            foreach (var subscription in settings.Subscriptions)
            {
                services.AddSingleton<ICompetingConsumer>(serviceProvider 
                    => serviceProvider.GetService<ReplicatingConsumerFactory>().CreateReplicatingConsumer(subscription.StreamName, subscription.GroupName));
            }

            return services;
        }
        
        /// <summary>
        /// Convenience method for binding configuration settings to strongly typed objects
        /// </summary>
        /// <param name="configuration">The configuration to use for binding</param>
        /// <param name="key">The configuration section to bind from</param>
        /// <returns>The created settings object bound from the configuration</returns>
        private static TSettings Bind<TSettings>(IConfiguration configuration, string key) where TSettings : new()
        {
            var settings = new TSettings();
            configuration.Bind(key, settings);
            return settings;
        }
    }
}