using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MySpace.DataRelay.Performance
{
	public class AggregateCounter
	{
		private int countThisSecond;
		private int countThisMinute;
		private int cursor;
		private int[] data;// = new int[60];
        private const Int32 c_lsFree = 0;
        private const Int32 c_lsOwned = 1;
        private int _lock = c_lsFree;
		
        public AggregateCounter(int counts)
		{
			data = new int[counts];
		}

		public void IncrementCounter()
		{
			Interlocked.Increment(ref countThisSecond);
		}

		public void IncrementCounterBy(int value)
		{
			Interlocked.Add(ref countThisSecond, value);
		}


        private bool EnterLock()
        {
            Thread.BeginCriticalRegion();
            // If resource available, set it to in-use and return
            if (Interlocked.Exchange(
                ref _lock, c_lsOwned) == c_lsFree)
            {
                return true;
            }
            else
            {
                Thread.EndCriticalRegion();
                return false;
            }

        }

        private void ExitLock()
        {
            // Mark the resource as available
            Interlocked.Exchange(ref _lock, c_lsFree);
            Thread.EndCriticalRegion();
        }


		/// <summary>
		/// Grabs the accumulated amount in the counter and clears it.
		/// This needs to be called by a 1 second timer.
		/// </summary>
		/// <returns>The total aggregated over the last min</returns>
        public int Tick()
		{
            if (EnterLock())
            {
                try
                {
                    int totalThisSecond = Interlocked.Exchange(ref countThisSecond, 0);
                    int valueFrom1MinAgo = Interlocked.Exchange(ref data[cursor], totalThisSecond);
                    cursor++;
                    if (cursor >= data.Length) cursor = 0;

                    countThisMinute -= valueFrom1MinAgo;
                    countThisMinute += totalThisSecond;

                    return countThisMinute;
                }
                finally
                {
                    ExitLock();
                }
            }
            else
            {
                return -1;
            }
		}
	}

}
