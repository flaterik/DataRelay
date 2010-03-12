using System;
using System.Timers;
using MySpace.BerkeleyDb.Configuration;
using BerkeleyDbWrapper;
using Timer = System.Timers.Timer;

namespace MySpace.BerkeleyDb.Facade
{
	internal delegate void ConfigurableCallbackTimerDelegate();

	internal class ConfigurableCallbackTimer : IDisposable
	{
		BerkeleyDbStorage storage;
		ConfigurableCallbackTimerDelegate callback;
		Timer timer;
		string name;
		
	public ConfigurableCallbackTimer(BerkeleyDbStorage storage, ITimerConfig timerConfig, string name,
			int defaultInterval, ConfigurableCallbackTimerDelegate callback)
		{
			if (timerConfig != null && timerConfig.Enabled)
			{
				this.name = name;
				this.storage = storage;
				this.callback = callback;
				timer = new Timer();
				int interval = timerConfig.Interval;
				if (interval <= 0) interval = defaultInterval;
				timer.Interval = interval;
				timer.Elapsed += timer_Elapsed;
				timer.Enabled = true;
				if (BerkeleyDbStorage.Log.IsInfoEnabled)
				{
					BerkeleyDbStorage.Log.InfoFormat("Initialize() {0} Interval = {1} milliseconds", name, interval);
				}
			}
		}

		readonly object disposeLock = new object();

		void timer_Elapsed(object sender, ElapsedEventArgs e)
		{
			BerkeleyDbStorage errorStorage = null;
			BdbException bdbExc = null;
			Exception regExc = null;
			var errorHandleIteration = -1;
			lock (disposeLock)
			{
				if (timer == null) return; // for race condition where Elapsed event fires even after Dispose called
				timer.Stop();
				if (storage == null) return;
				if (storage.IsShuttingDown) return;
				if (storage.IsInRecovery) return;
				try
				{
					storage.BlockOnRecovery();
					callback();
				}
				catch (BdbException exc)
				{
					errorStorage = storage;
					errorHandleIteration = errorStorage.HandleIteration;
					bdbExc = exc;
				}
				catch (Exception exc)
				{
					errorStorage = storage;
					errorHandleIteration = errorStorage.HandleIteration;
					regExc = exc;
				}
			}
			// needed to store storage in case Dispose called after lock, but need to do error handling outside of lock
			// because error handler might wind up calling Dispose, which also uses lock
			if (errorStorage != null && errorHandleIteration == errorStorage.HandleIteration)
			{
				if (bdbExc != null)
					errorStorage.HandleBdbError(bdbExc);
				else
					errorStorage.HandleGeneralError(regExc);
			}
			if (timer != null) timer.Start();
		}

		public void Dispose()
		{
			lock (disposeLock)
			{
				if (timer != null)
				{
					timer.Stop();
					timer.Dispose();
					timer = null;
				}
				storage = null;
				callback = null;
			}
		}
	}
}
