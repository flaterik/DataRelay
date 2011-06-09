using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;
using MySpace.Common;
using MySpace.Logging;

namespace MySpace.Common
{
	/// <summary>
	/// Provides a Safety wrapper around PerformanceCounter.  If the perf counter isn't installed in the system,
	/// allows you to still track counts, and logs if the counter is not installed.
	/// </summary>
	public class SafeCounter : IDisposable
	{
		private static readonly MySpace.Logging.LogWrapper log = new LogWrapper();
		private PerformanceCounter counter = null;
		private bool counterInstalled = true;
		private string counterName = string.Empty;
		private string categoryName = string.Empty;
		private string instanceName = string.Empty;
		private string machineName = ".";
		private bool readOnly;
		private long updateTicks = 0;
		private long value = 0;
		private object padLock = new object();
        private object backingCounterCreationPadLock = new object();
		private bool guardCounter = false;

		#region Constructors

		public SafeCounter() 
		{ }

		public SafeCounter(string categoryName, string counterName, bool readOnly) : this(categoryName, counterName, string.Empty, readOnly) { }

		public SafeCounter(string categoryName, string counterName, string instanceName) : this(categoryName, counterName, instanceName, true) { }

		public SafeCounter(string categoryName, string counterName, string instanceName, bool readOnly) 
		{
			this.categoryName = categoryName;
			this.counterName = counterName;
			this.instanceName = instanceName;
			this.readOnly = readOnly;
		}

		public SafeCounter(string categoryName, string counterName, string instanceName, string machineName)
		{
			this.categoryName = categoryName;
			this.counterName = counterName;
			this.instanceName = instanceName;
			this.machineName = machineName;
		}
		#endregion

		private PerformanceCounter Counter
		{
			get
			{
                if (counter == null && counterInstalled)
                {
                    lock (backingCounterCreationPadLock)
                    {
                        if (counter != null || !counterInstalled) return counter;
                        
                        try
                        {
                            if (!machineName.Equals("."))
                                counter = new PerformanceCounter(categoryName, counterName, instanceName, machineName);
                            else
                                counter = new PerformanceCounter(categoryName, counterName, instanceName, readOnly);
                            counter.RawValue = 0;
                        }
                        catch
                        {
                            counterInstalled = false;
                            Console.WriteLine("Expected counter was not installed.");
                        }
                    }
                }
				return counter;
			}
		}

		public long Value
		{
			get { return value; }
			set
			{
				lock (padLock)
				{
					this.value = value;
					SetCounterRawValue(Counter, this.value);
				}
			}
		}

		public void Increment()
		{
			Interlocked.Increment(ref value);
			IncrementBy(Counter, 1);
		}

		public void Increment(long count)
		{
			Interlocked.Add(ref value, count < 0 ? count * -1 : count);
			IncrementBy(Counter, count);
		}

		public void Decrement()
		{
			Interlocked.Decrement(ref value);
			DecrementBy(Counter, 1);
		}

		public void Decrement(long count)
		{
			// normalize count
			count = count < 0 ? count : count * -1;
			if(value + count >= 0)
				Interlocked.Add(ref value, count);
			DecrementBy(Counter, count);
		}

		public void Reset()
		{
			lock (padLock)
			{
				this.value = 0;
				SetCounterRawValue(Counter, 0);
			}
		}

		private void IncrementBy(PerformanceCounter counter, long value)
		{
			if (counter != null)
				counter.IncrementBy(value);
		}

		private void DecrementBy(PerformanceCounter counter, long value)
		{
			if (counter != null)
				counter.IncrementBy(value < 0 ? value : value * -1);
		}

		private void SetCounterRawValue(PerformanceCounter counter, long value)
		{
			if (counter != null)
				counter.RawValue = value;
		}

		private long GetCounterRawValue(PerformanceCounter counter)
		{
			if (counter != null)
				return counter.RawValue;
			else
				return 0;
		}

		/// <summary>
		/// If True, protects the counter from being updated in sub-second intervals.  defaults to False.
		/// </summary>
		public bool GuardCounter
		{
			get { return guardCounter; }
			set { guardCounter = value; }
		}

		private bool ShouldUpdate
		{
			get
			{
				if (counterInstalled)
				{
					// This protects the system perf counter from sub-second frequency updates.
					// Some applications update the raw value so often, that under heavy CPU the counter will die.
					if (guardCounter)
					{
						if (DateTime.Now.Ticks > updateTicks)
						{
							updateTicks = DateTime.Now.Ticks + 1000;
							return true;
						}
					}
					else
						return true;
				}
				return false;
			}
		}

		#region IDisposable Members

		public void Dispose()
		{
			if (counter != null)
			{
				counter.RawValue = 0;
				counter.Dispose();
			}
		}

		#endregion
	}
}