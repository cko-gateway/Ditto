using System;
using Amazon.Kinesis;
using Ditto.Core;
using EventStore.ClientAPI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Ditto.Kinesis
{
    /// <summary>
    /// Registry of application dependencies used to configure StructureMap containers
    /// </summary>
    public class AppRegistry
    {
        public static ServiceCollection Register(IConfiguration configuration, ServiceCollection services)
        {
            services.AddSingleton<IConfiguration>(configuration);

            services.AddDefaultAWSOptions(configuration.GetAWSOptions());
            services.AddAWSService<IAmazonKinesis>();
            
            // Binds the "Settings" section from appsettings.json to AppSettings
            var settings = configuration.Bind<DittoSettings>("Settings");
            services.AddSingleton(settings);

            var destinationSettings = configuration.Bind<KinesisSettings>("Kinesis");
            services.AddSingleton(destinationSettings);

            services.AddSingleton<AppService>();

            services.AddSingleton<ILogger>(Log.Logger);

            services.AddSingleton<IEventStoreConnection>(provider 
                => ConnectionFactory.CreateEventStoreConnection(provider.GetService<ILogger>(), settings.SourceEventStoreConnectionString, "Ditto:Source"));

            services.AddSingleton<IConsumerManager, CompetingConsumerManager>();

            // Register replicating consumers
            foreach (var subscription in settings.Subscriptions)
            {
                services.AddSingleton<ICompetingConsumer>(provider => CreateConsumer(provider, settings, destinationSettings, subscription.StreamName, subscription.GroupName));
            }

            return services;
        }

        private static ICompetingConsumer CreateConsumer(
            IServiceProvider provider,
            DittoSettings settings,
            KinesisSettings destinationSettings,
            string streamName,
            string groupName)
        {
            return new ReplicatingConsumer(
                provider.GetRequiredService<IAmazonKinesis>(),
                destinationSettings,
                settings,
                provider.GetRequiredService<ILogger>(),
                streamName,
                groupName
            );
        }
    }
}