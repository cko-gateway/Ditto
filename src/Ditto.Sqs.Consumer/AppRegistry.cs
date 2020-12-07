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

            var consumerOptions = configuration.GetSection("Consumer").Get<ConsumerOptions>();
            services.AddSingleton(consumerOptions);

            services.AddSqsConsumers(builder =>
                builder
                    .FromConfiguration(configuration)
                    .Consume("ditto",
                        consume => consume
                            .IgnoreUnregisteredTypes()
                            .TreatUnregisteredTypesAsHandled()
                            .WithHandler<EventWrapper, SqsEventHandler>(nameof(EventWrapper))
                            .WithReaderCount(1)
                            .WithMaxInFlightMessages(consumerOptions.WithMaxInFlightMessages))
                            .Build()
                );

            services.AddSingleton<AppService>();

            services.AddSingleton<ILogger>(Log.Logger);
            
            services.AddSingleton<IEventStoreConnection>(provider
                => ConnectionFactory.CreateEventStoreConnection(provider.GetService<ILogger>(), settings.SourceEventStoreConnectionString, "Ditto:Source"));

            services.AddSingleton<IEventStoreWriter, EventStoreWriter>();

            return services;
        }
    }
}