using System;
using Newtonsoft.Json;

namespace Ditto.Sqs.Consumer.EventStore
{
    public class EventWrapper
    {
        public EventWrapper()
        {
            ReplicatedOn = DateTime.UtcNow;
        }

        public string StreamId { get; set; }
        public Guid EventId { get; set; }
        public long EventNumber { get; set; }
        public string EventType { get; set; }
        public DateTime EventTimestamp { get; set; }
        public DateTime ReplicatedOn { get; set; }

        [JsonConverter(typeof(JsonStringConverter))]
        public string Data { get; set; }

        [JsonConverter(typeof(JsonStringConverter))]
        public string Metadata { get; set; }
    }
}