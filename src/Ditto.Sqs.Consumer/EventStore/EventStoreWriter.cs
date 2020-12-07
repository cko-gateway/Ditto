using EventStore.ClientAPI;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ditto.Sqs.Consumer
{
    public class EventStoreWriter : IEventStoreWriter
    {
        private readonly IEventStoreConnection _eventStoreConnection;

        public EventStoreWriter(IEventStoreConnection eventStoreConnection)
        {
            _eventStoreConnection = eventStoreConnection;
        }

        public async Task SaveAsync(object a, CancellationToken c)
        {
            if (a == null)
                throw new ArgumentNullException(nameof(a));

            await _eventStoreConnection.AppendToStreamAsync();
        }
    }
}
