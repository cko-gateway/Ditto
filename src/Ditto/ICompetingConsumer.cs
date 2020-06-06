using EventStore.ClientAPI;

namespace Ditto
{
    public interface ICompetingConsumer
    {
        /// <summary>
        /// Gets the name of the stream to consumer
        /// </summary>
        string StreamName { get; }

        /// <summary>
        /// Gets the name of the subscription group
        /// </summary>
        string GroupName { get; }
        
        /// <summary>
        /// Indicates whether an event with the specified type name can be consumed
        /// </summary>
        /// <param name="eventType">The type of event e.g. EmailSent</param>
        /// <returns>True if the event can be consumed, otherwise False</returns>
        bool CanConsume(string eventType);
        
        /// <summary>
        /// Consumes the Event Store event
        /// </summary>
        /// <param name="eventType">The event type</param>
        /// <param name="e">The event store event</param>
        void Consume(string eventType, ResolvedEvent e);
    }
}