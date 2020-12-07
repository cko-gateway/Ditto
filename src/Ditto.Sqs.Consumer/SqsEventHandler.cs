using System.Threading;
using System.Threading.Tasks;
using App.Metrics;
using Gateway.Extensions.Sqs.Consumers;
using Serilog;

namespace Ditto.Sqs.Consumer
{
    public class SqsEventHandler : IHandler<EventWrapper>
    {
        private readonly Serilog.ILogger _logger;
        private readonly IDiagnosticContext _diagnosticContext;
        private readonly IMetrics _metrics;
        private IEventStore _eventStore;

        public SqsEventHandler(IEventStore eventStore)
        {
            _eventStore = eventStore;
        }

        public Task<HandlerResult> HandleAsync(EventWrapper message, MessageContext context, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}