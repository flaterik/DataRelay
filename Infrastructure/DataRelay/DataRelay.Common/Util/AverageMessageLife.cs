using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;


namespace MySpace.DataRelay
{
    /// <summary>
    /// Responsible for recording the average message life for a <see cref="RelayMessage"/>.
    /// </summary>
    public class AverageMessageLife
    {
        private PerformanceCounter elapsedTimer;
        private PerformanceCounter baseCount;

        /// <summary>
        /// Initializes the <see cref="AverageMessageLife"/> instrument with the required 
        /// <see cref="PerformanceCounter"/>s.
        /// </summary>
        /// <param name="averageCount64">A <see cref="PerformanceCounter"/> of type 
        /// <see cref="PerformanceCounterType.AverageCount64"/>.</param>
        /// <param name="averageBase">A <see cref="PerformanceCounter"/> of type 
        /// <see cref="PerformanceCounterType.AverageBase"/>.</param>
        public AverageMessageLife(PerformanceCounter averageCount64, PerformanceCounter averageBase)
        {
            if (averageCount64 == null) throw new ArgumentNullException("averageCount64");
            if (averageBase == null) throw new ArgumentNullException("averageBase");
            if (averageCount64.CounterType != PerformanceCounterType.AverageCount64)
            {
                throw new ArgumentException("averageCount64 must of type PerformanceCounterType.AverageCount64");
            }
            if (averageBase.CounterType != PerformanceCounterType.AverageBase)
            {
                throw new ArgumentException("averageBase must of type PerformanceCounterType.AverageBase");
            }
            elapsedTimer = averageCount64;
            baseCount = averageBase;
        }

        /// <summary>
        /// Calculates the life of the given <see cref="SerializedRelayMessage"/>.
        /// </summary>
        /// <param name="message">The given message.</param>
        public void CalculateLife(SerializedRelayMessage message)
        {
			CalculateLife(message.EnteredCurrentSystemAt, Stopwatch.GetTimestamp());
        }

        /// <summary>
        /// Calculates the life of the given <see cref="RelayMessage"/>.
        /// </summary>
        /// <param name="message">The given message.</param>
        public void CalculateLife(RelayMessage message)
        {
			CalculateLife(message.EnteredCurrentSystemAt, Stopwatch.GetTimestamp());
        }

        /// <summary>
        /// Calculates the life each <see cref="SerializedRelayMessage"/> in the given list.
        /// </summary>
        /// <param name="messages">The messages to calculate.</param>
        public void CalculateLife(IList<SerializedRelayMessage> messages)
        {
            long now = Stopwatch.GetTimestamp();
            for (int i = 0; i < messages.Count; i++)
            {
				CalculateLife(messages[i].EnteredCurrentSystemAt, now);
            }
        }

        /// <summary>
        /// Calculates the life each <see cref="RelayMessage"/> in the given list.
        /// </summary>
        /// <param name="messages">The messages to calculate.</param>
        public void CalculateLife(IList<RelayMessage> messages)
        {
            long now = Stopwatch.GetTimestamp();
            for (int i = 0; i < messages.Count; i++)
            {
				CalculateLife(messages[i].EnteredCurrentSystemAt, now);
            }
        }

        /// <summary>
        /// Calculates and records the life of a message given it's start and leave times.
        /// </summary>
        /// <param name="messageEnteredAt">The timestamp the message entered the system (from <see cref="Stopwatch"/>).</param>
        /// <param name="messageLeftAt">The timestamp the message left the system (from <see cref="Stopwatch"/>).</param>
        public void CalculateLife(long messageEnteredAt, long messageLeftAt)
        {
            long diff = (messageLeftAt - messageEnteredAt);
            double seconds = ((double)diff) / Stopwatch.Frequency;
            long microseconds = (long)(seconds * 1000000);

			elapsedTimer.IncrementBy(microseconds);
            baseCount.Increment();
        }

    }
}
