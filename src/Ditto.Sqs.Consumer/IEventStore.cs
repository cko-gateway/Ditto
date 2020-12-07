using System.Threading;
using System.Threading.Tasks;

namespace Ditto.Sqs.Consumer
{
    public interface IEventStore
    {
        Task Save(object a, CancellationToken c);
    }
}