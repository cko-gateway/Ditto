using System.Threading;
using System.Threading.Tasks;

namespace Ditto.Sqs.Consumer.EventStore
{
    public interface IEventStoreWriter
    {
        Task SaveAsync(Document document, CancellationToken cancellationToken);
    }
}