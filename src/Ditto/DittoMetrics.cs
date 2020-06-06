using Prometheus;

namespace Ditto
{
    public static class DittoMetrics
    {
        private static string[] ConsumerLabels = new[] { "app", "stream", "group" };
        
        public static readonly Counter ReceivedEvents = Metrics.CreateCounter("ditto_events_received_total", "Number of events received", ConsumerLabels);
        public static readonly Counter UnresolvedEvents = Metrics.CreateCounter("ditto_events_unresolved_total", "Number of unresolved events", ConsumerLabels);
        public static readonly Counter SkippedEvents = Metrics.CreateCounter("ditto_events_skipped_total", "Number of skipped events", ConsumerLabels);
        public static readonly Counter ProcessedEvents = Metrics.CreateCounter("ditto_events_processed_total", "Number of processed events", ConsumerLabels);
        public static readonly Counter FailedEvents = Metrics.CreateCounter("ditto_events_failed_total", "Number of failed events", ConsumerLabels);
        public static readonly Gauge CurrentEvent = Metrics.CreateGauge("ditto_current_event", "The current original event number being processed by Ditto", ConsumerLabels);

        public static Counter.Child WithConsumerLabels(this Counter counter, ICompetingConsumer consumer)
            => counter.WithLabels("ditto", consumer.GroupName, consumer.StreamName);

        public static Gauge.Child WithConsumerLabels(this Gauge counter, ICompetingConsumer consumer)
            => counter.WithLabels("ditto", consumer.GroupName, consumer.StreamName);
    }
}