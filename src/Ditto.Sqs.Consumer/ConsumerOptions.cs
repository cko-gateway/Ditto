using System;

namespace Ditto.Sqs.Consumer
{
    public class ConsumerOptions
    {
        /// <summary>
        /// The amount of delay to add to a sqs message if it cannot be handled
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = new TimeSpan(0, 0, 15);

        /// <summary>
        /// The amount of times to retry a SQS message if it cannot be handled
        /// </summary>
        public int RetryAttempts { get; set; } = 5;
    }
}