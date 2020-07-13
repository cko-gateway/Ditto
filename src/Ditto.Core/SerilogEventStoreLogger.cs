using System;
using Serilog;

namespace Ditto.Core
{
    /// <summary>
    /// Event Store logger that uses Serilog
    /// </summary>
    public class SerilogEventStoreLogger : EventStore.ClientAPI.ILogger
    {
        private readonly ILogger _logger;

        public SerilogEventStoreLogger(ILogger logger)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            _logger = logger.ForContext<SerilogEventStoreLogger>();
        }
        
        public void Debug(string format, params object[] args)
        {
            _logger.Debug(format, args);
        }

        public void Debug(Exception ex, string format, params object[] args)
        {
            _logger.Debug(ex, format, args);
        }

        public void Error(string format, params object[] args)
        {
            _logger.Error(format, args);
        }

        public void Error(Exception ex, string format, params object[] args)
        {
            _logger.Error(ex, format, args);
        }

        public void Info(string format, params object[] args)
        {
            _logger.Information(format, args);
        }

        public void Info(Exception ex, string format, params object[] args)
        {
            _logger.Information(ex, format, args);
        }
    }
}