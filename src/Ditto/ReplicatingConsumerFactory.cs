using System;
using EventStore.ClientAPI;
using ILogger = Serilog.ILogger;

namespace Ditto
{
    public class ReplicatingConsumerFactory
    {
        private readonly AppSettings _appSettings;
        private readonly ILogger _logger;
        private Lazy<IEventStoreConnection> _destinationConnection;

        public ReplicatingConsumerFactory(AppSettings appSettings, ILogger logger)
        {
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _destinationConnection = new Lazy<IEventStoreConnection>(()
                => ConnectionFactory.CreateEventStoreConnection(_logger, _appSettings.DestinationEventStoreConnectionString, "Ditto:Destination"));
        }

        public ICompetingConsumer CreateReplicatingConsumer(string streamName, string groupName)
        {
            return new ReplicatingConsumer(
                _destinationConnection.Value,
                _logger.ForContext<ReplicatingConsumer>(),
                _appSettings,
                streamName,
                groupName
            );
        }
    }
}