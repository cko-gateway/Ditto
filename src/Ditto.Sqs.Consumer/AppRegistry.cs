using System;
using Amazon.Kinesis;
using Ditto.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Ditto.Sqs.Consumer
{
    /// <summary>
    /// Registry of application dependencies used to configure StructureMap containers
    /// </summary>
    public class AppRegistry
    {
        public static ServiceCollection Register(IConfiguration configuration, ServiceCollection services)
        {
            services.AddSingleton(configuration);

            services.AddDefaultAWSOptions(configuration.GetAWSOptions());
            services.AddAWSService<IAmazonKinesis>();
            
            // Binds the "Settings" section from appsettings.json to AppSettings
            var settings = configuration.Bind<DittoSettings>("Settings");
            services.AddSingleton(settings);

            var destinationSettings = configuration.Bind<KinesisSettings>("Kinesis");
            services.AddSingleton(destinationSettings);

            services.AddSingleton<AppService>();

            services.AddSingleton(Log.Logger);

            services.AddSingleton(provider 
                => ConnectionFactory.CreateEventStoreConnection(provider.GetService<ILogger>(), settings.SourceEventStoreConnectionString, "Ditto:Source"));

            services.AddSingleton<IConsumerManager, CompetingConsumerManager>();

            // Register replicating consumers
            foreach (var subscription in settings.Subscriptions)
            {
                services.AddSingleton(provider => CreateConsumer(provider, settings, destinationSettings, subscription.StreamName, subscription.GroupName));
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
                provider.GetRequiredService<IEventStoreWriter>(),
                streamName,
                groupName
            );
        }
    }
}