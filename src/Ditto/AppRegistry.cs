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
            var settings = configuration.Bind<AppSettings>("Settings");
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
    }
}