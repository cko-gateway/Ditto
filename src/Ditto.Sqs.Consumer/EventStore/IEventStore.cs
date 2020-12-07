using System.Threading;
using System.Threading.Tasks;

namespace Ditto.Sqs.Consumer
{
    public interface IEventStoreWriter
    {
        Task SaveAsync(object a, CancellationToken cancellationToken);
    }
}