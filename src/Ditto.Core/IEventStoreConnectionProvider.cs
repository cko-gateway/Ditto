using EventStore.ClientAPI;
using System.Threading;
using System.Threading.Tasks;

namespace Ditto.Core
{
    public interface IEventStoreConnectionProvider
    {
        Task<IEventStoreConnection> OpenAsync(string connectionString, string connectionName, CancellationToken cancellationToken);
    }
}
