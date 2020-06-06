using System;
using EventStore.ClientAPI;
using ILogger = Serilog.ILogger;

namespace Ditto
{
    public class ConnectionFactory
    {
        public static IEventStoreConnection CreateEventStoreConnection(ILogger logger, string connectionString, string connectionName)
        {
            var connectionSettings = ConnectionSettings.Create()
                .KeepReconnecting()
                .SetReconnectionDelayTo(TimeSpan.FromSeconds(3))
                .KeepRetrying()
                .UseCustomLogger(new SerilogEventStoreLogger(logger));

            var connection = EventStoreConnection
                .Create(connectionString, connectionSettings, connectionName);

            connection
                .Connected += (sender, args)
                    => logger.Information("Connected to Event Store {ConnectionName}", connectionName);

            connection.Closed += (sender, args)
                => logger.Information("Connection to Event Store {ConnectionName} closed", connectionName);

            connection.Reconnecting += (sender, args)
                => logger.Information("Attempting to connect to Event Store {ConnectionName}", connectionName);

            connection.ConnectAsync().GetAwaiter().GetResult();

            return connection;
        }
    }
}