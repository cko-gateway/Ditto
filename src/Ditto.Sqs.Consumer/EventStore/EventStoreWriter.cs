using Ditto.Core;
using EventStore.ClientAPI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ditto.Sqs.Consumer
{
    public class EventStoreWriter : IEventStoreWriter
    {
        private readonly IEventStoreConnectionProvider _eventStoreConnectionProvider;
        private readonly Serilog.ILogger _logger;
        private readonly DittoSettings _dittoSettings;

        private static readonly string _connectionName = "Ditto:Source";
        private IEventStoreConnection _eventStoreConnection = null;

        public EventStoreWriter(IEventStoreConnectionProvider eventStoreConnectionProvider, Serilog.ILogger logger, DittoSettings dittoSettings)
        {
            _eventStoreConnectionProvider = eventStoreConnectionProvider ?? throw new ArgumentNullException(nameof(eventStoreConnectionProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dittoSettings = dittoSettings ?? throw new ArgumentNullException(nameof(dittoSettings));
        }

        public async Task SaveAsync(SqsEvent sqsEvent, CancellationToken cancellationToken)
        {
            if (sqsEvent == null)
                throw new ArgumentNullException(nameof(sqsEvent));

            if (_eventStoreConnection == null)
                _eventStoreConnection = await _eventStoreConnectionProvider.OpenAsync(_dittoSettings.SourceEventStoreConnectionString, _connectionName, cancellationToken);

            try
            {
                _ = await _eventStoreConnection.AppendToStreamAsync(sqsEvent.StreamName, sqsEvent.EventNumber - 1, new List<EventData>
                {
                    new EventData(Guid.NewGuid(), sqsEvent.EventType, true, sqsEvent.Data, sqsEvent.Metadata)
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message, ex);
                throw;
            }
        }
    }
}
