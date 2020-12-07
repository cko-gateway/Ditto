using EventStore.ClientAPI;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Ditto.Core
{
    public class EventStoreConnectionProvider : IEventStoreConnectionProvider
    {
        private readonly Serilog.ILogger _logger;
        private readonly ConcurrentDictionary<string, Task<IEventStoreConnection>> _connections;

        public EventStoreConnectionProvider(Serilog.ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connections = new ConcurrentDictionary<string, Task<IEventStoreConnection>>();
        }

        public async Task<IEventStoreConnection> OpenAsync(string connectionString, string connectionName, CancellationToken cancellationToken)
        {
            return await _connections.GetOrAdd(connectionName, async _ =>
            {
                try
                {
                    var connection = await OpenConnectionAsync(connectionString, connectionName, cancellationToken);
                    return connection;
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Failed to open EventStore connection for {ConnectionName}", connectionName);
                    throw;
                }
            });
        }

        private Task<IEventStoreConnection> OpenConnectionAsync(string connectionString, string connectionName, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<IEventStoreConnection>();

            cancellationToken.Register(() =>
            {
                _logger.Warning("Cancelling connection to Event Store {ConnectionName}", connectionName);
                tcs.SetCanceled();
            });

            var connection = CreateConnection(connectionString, connectionName, tcs);
            connection.ConnectAsync();
            return tcs.Task;
        }

        private IEventStoreConnection CreateConnection(string connectionString, string connectionName, TaskCompletionSource<IEventStoreConnection> tcs, Action<ConnectionSettingsBuilder> configure = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) 
                throw new ArgumentException("Connection string is required", nameof(connectionString));

            var settings = ConnectionSettings.Create()
                .UseCustomLogger(new SerilogEventStoreLogger(_logger))
                .KeepReconnecting()
                .SetReconnectionDelayTo(TimeSpan.FromSeconds(3))
                .KeepRetrying()
                .PerformOnAnyNode()
                .PreferRandomNode();

            configure?.Invoke(settings);

            var connection = EventStoreConnection.Create(connectionString, settings, connectionName);

            connection.Connected += (sender, args) =>
            {
                _logger.Information("Connected to Event Store {ConnectionName}", connectionName);
                tcs.TrySetResult(args.Connection);
            };

            // Never gets hit if KeepReconnecting
            connection.Closed += (sender, args) =>
            {
                _logger.Warning("Connection to Event Store {ConnectionName} closed", connectionName);

                _connections.TryRemove(connectionName, out var conn);

                //Shouldn't be null, just to be safe
                conn?.Dispose();
            };

            connection.Reconnecting += (sender, args)
                => _logger.Information("Attempting to connect to Event Store {ConnectionName}", connectionName);

            connection.Disconnected += (sender, args)
                => _logger.Warning("Disconnected from Event Store {ConnectionName}", connectionName);

            connection.ErrorOccurred += (sender, args)
                => _logger.Error(args.Exception, "Error occurred with Event Store {ConnectionName}", connectionName);

            connection.AuthenticationFailed += (sender, args)
                => _logger.Error("Failed to authenticate to Event Store {ConnectionName} with reason {Reason}", connection, args.Reason);

            return connection;
        }
    }
}
}
