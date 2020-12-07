using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ditto.Core;
using EventStore.ClientAPI;

namespace Ditto.Sqs.Consumer.EventStore
{
    public class EventStoreWriter : IEventStoreWriter
    {
        private readonly IEventStoreConnectionProvider _eventStoreConnectionProvider;
        private readonly Serilog.ILogger _logger;
        private readonly DittoSettings _dittoSettings;

        private static readonly string ConnectionName = "Ditto:Destination";

        public EventStoreWriter(IEventStoreConnectionProvider eventStoreConnectionProvider, Serilog.ILogger logger, DittoSettings dittoSettings)
        {
            _eventStoreConnectionProvider = eventStoreConnectionProvider ?? throw new ArgumentNullException(nameof(eventStoreConnectionProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dittoSettings = dittoSettings ?? throw new ArgumentNullException(nameof(dittoSettings));
        }

        public async Task SaveAsync(Document document, CancellationToken cancellationToken)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var connection = await _eventStoreConnectionProvider.OpenAsync(_dittoSettings.DestinationEventStoreConnectionString, ConnectionName, cancellationToken);

            try
            {
                var eventData = new EventData(document.EventId, document.EventType, true, document.Data, document.Metadata);
                _ = await connection.AppendToStreamAsync(document.StreamName, document.EventNumber - 1, eventData);
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message, ex);
                throw;
            }
        }
    }
}
