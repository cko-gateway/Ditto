using System;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics;
using Ditto.Core;
using Gateway.Extensions.Sqs.Consumers;
using Serilog;
using Serilog.Context;

namespace Ditto.Sqs.Consumer
{
    public class SqsEventHandler : IHandler<EventWrapper>
    {
        private static readonly HandlerResult Handled = new HandlerResult.Handled();
        private readonly Serilog.ILogger _logger;
        private readonly IDiagnosticContext _diagnosticContext;
        private readonly IMetrics _metrics;
        private readonly IEventStoreWriter _eventStore;
        private readonly ConsumerOptions _consumerOptions;
        private readonly IEventStoreConnectionProvider _eventStoreConnectionProvider;

        public SqsEventHandler(ILogger logger, IDiagnosticContext diagnosticContext, IMetrics metrics, IEventStoreWriter eventStore, ConsumerOptions consumerOptions)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _diagnosticContext = diagnosticContext ?? throw new ArgumentNullException(nameof(diagnosticContext));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
            _consumerOptions = consumerOptions ?? throw new ArgumentNullException(nameof(consumerOptions));
        }

        public async Task<HandlerResult> HandleAsync(EventWrapper message, MessageContext context, CancellationToken cancellationToken)
        {
            _ = message ?? throw new ArgumentNullException(nameof(message));

            _ = context ?? throw new ArgumentNullException(nameof(context));

            _metrics.RecordEventReceiveDelay("sqs", message.EventType, message.EventTimestamp);
            using var logContext = LogContext.Push(
                new PropertyBagEnricher().Add(nameof(message.EventId), message.EventId.ToString())
            );

            EnrichDiagnosticContext(message);

            try
            {
                await _eventStore.SaveAsync(SqsEvent.Map(message), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Warning("Found exception while saving to EVS", ex);
                return new HandlerResult.Retry(_consumerOptions.RetryDelay, _consumerOptions.RetryAttempts);
            }

            return Handled;
        }

        private void EnrichDiagnosticContext(EventWrapper message)
        {
            var processingLatency = (DateTime.UtcNow - message.EventTimestamp.ToUniversalTime()).TotalMilliseconds;
            _diagnosticContext.Set("ProcessingLatency", $"{processingLatency}Ms");
            _diagnosticContext.Set(nameof(message.EventType), message.EventType);
            _diagnosticContext.Set(nameof(message.EventId), message.EventId);
            _diagnosticContext.Set(nameof(message.EventTimestamp), message.EventTimestamp);
        }
    }
}