using EventStore.ClientAPI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ditto.Sqs.Consumer
{
    public class EventStoreWriter : IEventStoreWriter
    {
        private readonly IEventStoreConnection _eventStoreConnection;
        private readonly Serilog.ILogger _logger;

        public EventStoreWriter(IEventStoreConnection eventStoreConnection, Serilog.ILogger logger)
        {
            _eventStoreConnection = eventStoreConnection ?? throw new ArgumentNullException(nameof(eventStoreConnection));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SaveAsync(SqsEvent sqsEvent, CancellationToken cancellationToken)
        {
            if (sqsEvent == null)
                throw new ArgumentNullException(nameof(sqsEvent));

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
