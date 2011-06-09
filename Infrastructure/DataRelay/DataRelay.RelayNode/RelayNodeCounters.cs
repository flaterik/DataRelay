using System;
using System.Collections.Generic;
using System.Text;
using MySpace.DataRelay.Performance;
using System.Diagnostics;

namespace MySpace.DataRelay
{
	internal class RelayNodeCounters
	{
		#region Counter Definitions

		public static readonly string PerformanceCategoryName = "MySpace DataRelay";
		private bool _nov09CountersExist = false;
		private bool _july10CountersExist = false;
		protected PerformanceCounter[] PerformanceCounters;
		protected enum PerformanceCounterIndexes : int
		{
			Save = 0,
			Update = 1,
			Get = 2,
			Delete = 3,
			DeleteInAllTypes = 4,
			DeleteAllInType = 5,
			DeleteAll = 6,
			HitRatio = 7,
			HitRatioBase = 8,
			AvgBulkLength = 9,
			AvgBulkLengthBase = 10,
			AvgInputBytes = 11,
			AvgInputBytesBase = 12,
			QueuedInMessages = 13,
			MessagesPerSecond = 14,
			TotalMessages = 15,
			AvgMsgLife = 16,
			AvgMsgLifeBase = 17,
			SaveWithConfirm = 18,		//SaveWithConfirm start added 11/9/09
			UpdateWithConfirm = 19,
			DeleteWithConfirm = 20,
			DeleteInAllTypesWithConfirm = 21,
			DeleteAllInTypeWithConfirm = 22,
			DeleteAllWithConfirm = 23,
			NotificationWithConfirm = 24,
			IncrementWithConfirm = 25,
			Query = 26,
			Invoke = 27,
			Notification = 28,
			Increment = 29,				//end add 11/9/09
			RedirectCount = 30,			//start add 7/20/10
			RedirectRate = 31,
			RedirectErrorCount = 32	//end add 7/20/10
		}
		
		public static readonly string[] PerformanceCounterNames = { 
			@"Msg/Sec - Save", 
			@"Msg/Sec - Update",
			@"Msg/Sec - Get", 			
			@"Msg/Sec - Delete",			
			@"Msg/Sec - Delete In All Types", 
			@"Msg/Sec - Delete All In Type",
			@"Msg/Sec - Delete All",
			"Hit Ratio",
			"Hit Ratio Base",
			"Avg Bulk Msg Length",
			"Avg Bulk Msg Length Base",
			"Avg Input Bytes",
			"Avg Input Bytes Base",
			"Queued In Message Tasks",
			"Msg/Sec",
			"Total Messages Processed",
			"Avg Message Life",
			"Avg Message Life Base",
			@"Msg/Sec - Confirmed Save", 
			@"Msg/Sec - Confirmed Update",
			@"Msg/Sec - Confirmed Delete",			
			@"Msg/Sec - Confirmed Delete In All Types", 
			@"Msg/Sec - Confirmed Delete All In Type",
			@"Msg/Sec - Confirmed Delete All",
			@"Msg/Sec - Confirmed Notification",
			@"Msg/Sec - Confirmed Increment",
			@"Msg/Sec - Query", 
			@"Msg/Sec - Invoke",
			@"Msg/Sec - Notification",
			@"Msg/Sec - Increment",
			@"Redirect Count",
			@"Redirect Rate",
			@"Redirect Error Count"
		};
		
		public static readonly string[] PerformanceCounterHelp = { 
			"Save Messages Per Second", 
			"Update Messages Per Second",
			"Get Messages Per Second", 
			"Delete Messages Per Second", 
			"Delete In All Types Messages Per Second", 
			"Delete All In Type Messages Per Second",
			"Delete All Messages Per Seconds", 
			"Ratio of successful to unsuccessful Get Messages over the last minute",
			"Base for Hit Ratio",
			"Average number of messages in bulk messages",
			"Base for Bulk Message Length",
			"Average number of bytes per save message",
			"Average Bytes Base",
			"The number of pending in messages and in message lists",
			"Total Messages Per Second",
			"Total Messages Processed",
			"Average amount of time a message exists on RelayNode.",
			"Base for Average Life",
			"Confirmed Save Messages Per Second", 
			"Confirmed Update Messages Per Second",
			"Confirmed Delete Messages Per Second", 
			"Confirmed Delete In All Types Messages Per Second", 
			"Confirmed Delete All In Type Messages Per Second",
			"Confirmed Delete All Messages Per Seconds", 
			"Confirmed Notification Messages Per Second",
			"Confirmed Increment Messages Per Second",
			"Query Messages Per Second", 
			"Invoke Messages Per Second", 
			"Notification Messages Per Second", 
			"Increment Messages Per Second",
			"Count of Messages Redirected",
			"Messages Redirected Per Second",
			"Count of messages redirected to the wrong node"
		};
		
		public static readonly PerformanceCounterType[] PerformanceCounterTypes = { 			
			PerformanceCounterType.RateOfCountsPerSecond32, 
			PerformanceCounterType.RateOfCountsPerSecond32,
			PerformanceCounterType.RateOfCountsPerSecond32, 
			PerformanceCounterType.RateOfCountsPerSecond32, 
			PerformanceCounterType.RateOfCountsPerSecond32, 
			PerformanceCounterType.RateOfCountsPerSecond32, 
			PerformanceCounterType.RateOfCountsPerSecond32, 
			PerformanceCounterType.RawFraction,
			PerformanceCounterType.RawBase,
			PerformanceCounterType.AverageCount64,
			PerformanceCounterType.AverageBase,
			PerformanceCounterType.AverageCount64,
			PerformanceCounterType.AverageBase,
			PerformanceCounterType.NumberOfItems32,
			PerformanceCounterType.RateOfCountsPerSecond32,
			PerformanceCounterType.NumberOfItems64,
			PerformanceCounterType.AverageCount64,
			PerformanceCounterType.AverageBase,
			PerformanceCounterType.RateOfCountsPerSecond32, 
			PerformanceCounterType.RateOfCountsPerSecond32,
			PerformanceCounterType.RateOfCountsPerSecond32, 
			PerformanceCounterType.RateOfCountsPerSecond32, 
			PerformanceCounterType.RateOfCountsPerSecond32, 
			PerformanceCounterType.RateOfCountsPerSecond32,
			PerformanceCounterType.RateOfCountsPerSecond32, 
			PerformanceCounterType.RateOfCountsPerSecond32, 
			PerformanceCounterType.RateOfCountsPerSecond32, 
			PerformanceCounterType.RateOfCountsPerSecond32,
			PerformanceCounterType.RateOfCountsPerSecond32, 
			PerformanceCounterType.RateOfCountsPerSecond32,
			PerformanceCounterType.NumberOfItems32,
			PerformanceCounterType.RateOfCountsPerSecond32,
			PerformanceCounterType.NumberOfItems32
		};
		#endregion

		private bool countersInitialized = false;

		private MinuteAggregateCounter hitCounter;
		private MinuteAggregateCounter attemptCounter;		
		private System.Timers.Timer timer;
		private AverageMessageLife avgMessageLife;
		private static readonly MySpace.Logging.LogWrapper log = new MySpace.Logging.LogWrapper();

		internal void Initialize(string instanceName)
		{
			if (!PerformanceCounterCategory.Exists(RelayNodeCounters.PerformanceCategoryName))
			{
				InstallCounters();
			}
			else
			{
				countersInitialized = true;
				////check to see if all of the current counters are installed.
				//PerformanceCounterCategory perfCategory = new PerformanceCounterCategory(RelayNodeCounters.PerformanceCategoryName);
				//bool recreate = false;		
				//foreach (string counterName in RelayNodeCounters.PerformanceCounterNames)
				//{					
				//    if (!perfCategory.CounterExists(counterName))
				//    {
				//        LoggingWrapper.Write("Counter " + counterName + " not found in category " + RelayNodeCounters.PerformanceCategoryName, "Relay");
				//        recreate = true;
				//        break;
				//    }
				//}				
				//if (recreate)
				//{
				//    LoggingWrapper.Write("Recreating performance counter category " + RelayNodeCounters.PerformanceCategoryName, "Relay");
				//    RemoveCounters();
				//    InstallCounters();
				//}
				//else
				//{
				//    countersInitialized = true;
				//}
			}
			try
			{
				if (countersInitialized)
				{
					int numCounters = PerformanceCounterNames.Length;
					PerformanceCounters = new PerformanceCounter[numCounters];
					for (int i = 0; i < numCounters; i++)
					{
						PerformanceCounters[i] = new PerformanceCounter(
							PerformanceCategoryName,
							PerformanceCounterNames[i],
							instanceName,
							false
							);
					}
					PerformanceCounter avgMsgLife = PerformanceCounters[(int)PerformanceCounterIndexes.AvgMsgLife];
					PerformanceCounter avgMsgLifeBase = PerformanceCounters[(int)PerformanceCounterIndexes.AvgMsgLifeBase];

					if (PerformanceCounterCategory.CounterExists(PerformanceCounterNames[(int)PerformanceCounterIndexes.AvgMsgLife],
						RelayNodeCounters.PerformanceCategoryName))
					{
						avgMessageLife = new AverageMessageLife(avgMsgLife, avgMsgLifeBase);
					}

					if (PerformanceCounterCategory.CounterExists(PerformanceCounterNames[(int)PerformanceCounterIndexes.SaveWithConfirm],
						RelayNodeCounters.PerformanceCategoryName))
					{
						_nov09CountersExist = true;

						if (PerformanceCounterCategory.CounterExists(PerformanceCounterNames[(int)PerformanceCounterIndexes.RedirectCount],
							RelayNodeCounters.PerformanceCategoryName))
						{
							_july10CountersExist = true;
						}
					}

					if (!_july10CountersExist)
					{
						log.Warn("Some DataRelay performance counters are not installed, please reinstall DataRelay counters.");
					}
					   
					hitCounter = new MinuteAggregateCounter();
					attemptCounter = new MinuteAggregateCounter();
					ResetCounters();
					StartTimer();
				}
			}
			catch(Exception ex)
			{
				if (log.IsErrorEnabled)
					log.ErrorFormat("Exception creating Relay Node Counters: {0}. The counters might need to be reinstalled via InstallUtil.", ex);
				countersInitialized = false;
			}
		}

		internal void ResetCounters()
		{
			if (countersInitialized)
			{
				PerformanceCounters[(int)PerformanceCounterIndexes.HitRatio].RawValue = 0;
				PerformanceCounters[(int)PerformanceCounterIndexes.HitRatioBase].RawValue = 0;
				PerformanceCounters[(int)PerformanceCounterIndexes.TotalMessages].RawValue = 0;

				if (_july10CountersExist)
				{
					PerformanceCounters[(int)PerformanceCounterIndexes.RedirectCount].RawValue = 0;
					PerformanceCounters[(int)PerformanceCounterIndexes.RedirectErrorCount].RawValue = 0;
				}
			}
		}

		private void StartTimer()
		{
			timer = new System.Timers.Timer();
			timer.Interval = 1000;
			timer.AutoReset = true;
			timer.Elapsed += new System.Timers.ElapsedEventHandler(timer_Elapsed);
			timer.Start();
		}

		void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			int attempts = attemptCounter.Tick();
			int hits = hitCounter.Tick();
			if (countersInitialized)
			{
				if (attempts > 0)
				{
					PerformanceCounters[(int)PerformanceCounterIndexes.HitRatio].RawValue = hits;
					PerformanceCounters[(int)PerformanceCounterIndexes.HitRatioBase].RawValue = attempts;
				}
			}
		}

		private void RemoveCounters()
		{
			CounterInstaller.RemoveCounters();
			countersInitialized = false;
		}

		private void InstallCounters()
		{
			countersInitialized = CounterInstaller.InstallCounters();
		}

		private void CalculateLife(RelayMessage message)
		{
			if (avgMessageLife != null)
				avgMessageLife.CalculateLife(message);			
		}

		internal void CountInMessage(RelayMessage message)
		{
			if (countersInitialized && !message.WasRedirected)
			{
				CalculateLife(message);

				PerformanceCounters[(int)PerformanceCounterIndexes.MessagesPerSecond].Increment();
				PerformanceCounters[(int)PerformanceCounterIndexes.TotalMessages].Increment();

				switch (message.MessageType)
				{
					case MessageType.Save:
						PerformanceCounters[(int)PerformanceCounterIndexes.Save].Increment();
						break;
					case MessageType.Update:
						PerformanceCounters[(int)PerformanceCounterIndexes.Update].Increment();
						break;
					case MessageType.Notification:
						if (_nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.Notification].Increment();
						break;
					case MessageType.Increment:
						if (_nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.Increment].Increment();
						break;
					case MessageType.Delete:
						PerformanceCounters[(int)PerformanceCounterIndexes.Delete].Increment();
						break;
					case MessageType.DeleteAll:
						PerformanceCounters[(int)PerformanceCounterIndexes.DeleteAll].Increment();
						break;
					case MessageType.DeleteAllInType:
						PerformanceCounters[(int)PerformanceCounterIndexes.DeleteAllInType].Increment();
						break;
					case MessageType.DeleteInAllTypes:
						PerformanceCounters[(int)PerformanceCounterIndexes.DeleteInAllTypes].Increment();
						break;
				}
			}
		}

		internal void CountInputBytes(IList<RelayMessage> messages)
		{
			if (countersInitialized)
			{
				for (int i = 0; i < messages.Count; i++)
				{
					CountInputBytes(messages[i]);
				}
			}
		}

		internal void CountInputBytes(RelayMessage message)
		{
			if (countersInitialized && !message.WasRedirected)
			{
				switch(message.MessageType)
				{
					case MessageType.Save:
					case MessageType.SaveWithConfirm:
					case MessageType.Update:
					case MessageType.UpdateWithConfirm:
					case MessageType.Invoke:
					case MessageType.Notification:
					case MessageType.NotificationWithConfirm:
					case MessageType.Increment:
					case MessageType.IncrementWithConfirm:
						if (message.Payload != null && message.Payload.ByteArray != null)
						{
							PerformanceCounters[(int)PerformanceCounterIndexes.AvgInputBytes].IncrementBy(message.Payload.ByteArray.Length);
						}
						if (message.QueryData != null)
						{
							PerformanceCounters[(int)PerformanceCounterIndexes.AvgInputBytes].IncrementBy(message.QueryData.Length);
						}
						PerformanceCounters[(int)PerformanceCounterIndexes.AvgInputBytesBase].Increment();
					break;
				}
			}			
		}

		internal void CountOutMessage(RelayMessage message)
		{
			if (countersInitialized && !message.WasRedirected)
			{
				CalculateLife(message);

				PerformanceCounters[(int)PerformanceCounterIndexes.MessagesPerSecond].Increment();
				PerformanceCounters[(int)PerformanceCounterIndexes.TotalMessages].Increment();

				switch (message.MessageType)
				{
					case MessageType.Get:
						PerformanceCounters[(int)PerformanceCounterIndexes.Get].Increment();
						attemptCounter.IncrementCounter();
						if (message.Payload != null)
						{
							hitCounter.IncrementCounter();
						}
						break;
					case MessageType.Query:
						if (_nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.Query].Increment();
						attemptCounter.IncrementCounter();
						if (message.Payload != null)
						{
							hitCounter.IncrementCounter();
						}
						break;
					case MessageType.Invoke:
						if (_nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.Invoke].Increment();
						attemptCounter.IncrementCounter();
						if (message.Payload != null)
						{
							hitCounter.IncrementCounter();
						}
						break;
					case MessageType.NotificationWithConfirm:
						if (_nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.NotificationWithConfirm].Increment();
						break;
					case MessageType.IncrementWithConfirm:
						if (_nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.IncrementWithConfirm].Increment();
						break;
					case MessageType.SaveWithConfirm:
						if (_nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.SaveWithConfirm].Increment();
						break;
					case MessageType.UpdateWithConfirm:
						if (_nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.UpdateWithConfirm].Increment();
						break;
					case MessageType.DeleteWithConfirm:
						if (_nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.DeleteWithConfirm].Increment();
						break;
					case MessageType.DeleteInAllTypesWithConfirm:
						if (_nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.DeleteInAllTypesWithConfirm].Increment();
						break;
					case MessageType.DeleteAllWithConfirm:
						if (_nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.DeleteAllWithConfirm].Increment();
						break;
					case MessageType.DeleteAllInTypeWithConfirm:
						if (_nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.DeleteAllInTypeWithConfirm].Increment();
						break;
				}
			}
		}

		internal void CountInMessages(IList<RelayMessage> messages)
		{
			if (countersInitialized)
			{	
				for (int i = 0; i < messages.Count; i++)
				{
					CountInMessage(messages[i]);
				}
			}
		}

		internal void CountOutMessages(IList<RelayMessage> messages)
		{
			if (countersInitialized)
			{
				for (int i = 0; i < messages.Count; i++)
				{
					CountOutMessage(messages[i]);
				}
			}

		}

		internal void CountMessageList(IList<RelayMessage> messages)
		{
			if (countersInitialized)
			{
				PerformanceCounters[(int)PerformanceCounterIndexes.AvgBulkLength].IncrementBy(messages.Count);
				PerformanceCounters[(int)PerformanceCounterIndexes.AvgBulkLengthBase].Increment();
			}
		}

		internal void SetNumberOfQueuedMessages(int count)
		{
			if (countersInitialized)
			{
				PerformanceCounters[(int)PerformanceCounterIndexes.QueuedInMessages].RawValue = count;
			}
		}

		/// <summary>
		/// Counts redirected messages.
		/// </summary>
		/// <param name="count">The number of messages that were redirected.</param>
		internal void CountRedirectedMessage(int count)
		{
			if (_july10CountersExist)
			{
				PerformanceCounters[(int) PerformanceCounterIndexes.RedirectCount].IncrementBy(count);
				PerformanceCounters[(int)PerformanceCounterIndexes.RedirectRate].IncrementBy(count);
			}
		}

		/// <summary>
		/// Counts messages that were redirected to the wrong node.
		/// </summary>
		/// <param name="count">The number of messages redirected to the wrong server.</param>
		internal void CountRedirectionErrors(int count)
		{
			if (_july10CountersExist)
			{
				PerformanceCounters[(int)PerformanceCounterIndexes.RedirectErrorCount].IncrementBy(count);
			}
		}

		internal void Shutdown()
		{
			if (countersInitialized)
			{
				countersInitialized = false;
				foreach (PerformanceCounter counter in PerformanceCounters)
				{
					counter.Close();
					counter.Dispose();
				}
			}
		}
	}
}
