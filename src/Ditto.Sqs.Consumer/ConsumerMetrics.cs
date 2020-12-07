using System;
using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Timer;

namespace Ditto.Sqs.Consumer
{
    internal static class ConsumerMetrics
    {
        internal static void RecordEventOutcome(this IMetrics metrics, string groupName, string outcome)
        {
            if (metrics is null) return;
            if (string.IsNullOrWhiteSpace(groupName)) return;
            if (string.IsNullOrWhiteSpace(outcome)) return;
            
            metrics.Measure.Counter.Increment(EventOutcomeTotal, new MetricTags(new []{"group_name", "outcome"}, new []{ groupName, outcome }));
        }

        /// <summary>
        /// Records the delay between the event timestamp and when the event was received by Broadcast
        /// </summary>
        /// <param name="metrics">The metrics instance</param>
        /// <param name="sourceType">How the event was received, e.g. eventstore or sqs</param>
        /// <param name="eventType">The type of event</param>
        /// <param name="eventTimestamp">The timestamp of the event</param>
        internal static void RecordEventReceiveDelay(this IMetrics metrics, string sourceType, string eventType, DateTime eventTimestamp)
        {
            if (metrics is null)
                throw new ArgumentNullException(nameof(metrics));
            
            if (string.IsNullOrWhiteSpace(sourceType))
                throw new ArgumentException($"'{nameof(sourceType)}' cannot be null or whitespace", nameof(sourceType));

            if (string.IsNullOrWhiteSpace(eventType))
                throw new ArgumentException($"'{nameof(eventType)}' cannot be null or whitespace", nameof(eventType));

            metrics.Provider.Timer
                .Instance(EventReceiveDelay,
                    new MetricTags(
                        new[] { "source_type", "event_type" },
                        new[] { sourceType, eventType }))
                .Record((long)(DateTime.UtcNow - eventTimestamp.ToUniversalTime()).TotalMilliseconds, TimeUnit.Milliseconds);
        }

        private static readonly CounterOptions EventOutcomeTotal = new CounterOptions
        {
            Name = "events handled total",
            MeasurementUnit = Unit.Calls
        };

        private static readonly TimerOptions EventReceiveDelay = new TimerOptions
        {
            Name = "Event receive delay seconds",
            MeasurementUnit = Unit.Items,
            DurationUnit = TimeUnit.Seconds
        };
    }
}