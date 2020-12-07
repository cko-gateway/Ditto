using System;
using System.Text;

namespace Ditto.Sqs.Consumer.EventStore
{
    public class Document
    {
        public string StreamName { get; set; }

        public long EventNumber { get; set; }

        public string EventType { get; set; }

        public byte[] Data { get; set; }

        public byte[] Metadata { get; set; }
        public Guid EventId { get; set; }

        public static Document Map(EventWrapper eventWrapper)
        {
            if (eventWrapper == null)
                throw new ArgumentNullException(nameof(eventWrapper));

            return new Document
            {
                EventId = eventWrapper.EventId,
                StreamName = eventWrapper.StreamId,
                EventNumber = eventWrapper.EventNumber,
                EventType = eventWrapper.EventType,
                Data = Encoding.UTF8.GetBytes(eventWrapper.Data),
                Metadata = Encoding.UTF8.GetBytes(eventWrapper.Metadata)
            };
        }
    }
}
