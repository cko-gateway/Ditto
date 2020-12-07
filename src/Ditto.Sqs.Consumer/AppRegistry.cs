using Ditto.Core;
using EventStore.ClientAPI;
using Gateway.Extensions.Sqs.Consumers;
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
            services.AddSingleton<IConfiguration>(configuration);

            // Binds the "Settings" section from appsettings.json to AppSettings
            var settings = configuration.Bind<DittoSettings>("Settings");
            services.AddSingleton(settings);

            services.AddSqsConsumers(builder =>
                builder
                    .FromConfiguration(configuration)
                    .Consume("ditto",
                        consume => consume
                            .IgnoreUnregisteredTypes()
                            .TreatUnregisteredTypesAsHandled()
                            .WithHandler<EventWrapper, SqsEventHandler>(nameof(EventWrapper)))
                            .Build()
                );

            services.AddSingleton<AppService>();

            services.AddSingleton<ILogger>(Log.Logger);
            services.Configure<ConsumerOptions>(configuration.GetSection("Consumer"));
            services.AddSingleton<IEventStoreConnection>(provider
                => ConnectionFactory.CreateEventStoreConnection(provider.GetService<ILogger>(), settings.SourceEventStoreConnectionString, "Ditto:Source"));

            return services;
        }
    }
}