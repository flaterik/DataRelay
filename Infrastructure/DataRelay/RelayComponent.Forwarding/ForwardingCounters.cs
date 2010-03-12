using System;
using System.Collections.Generic;
using System.Diagnostics;
using MySpace.DataRelay.Performance;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	class ForwardingCounters
	{
		#region Counter Definitions

		public static readonly string PerformanceCategoryName = "MySpace Relay Forwarding";
		private bool nov09CountersExist = false;

		protected PerformanceCounter[] PerformanceCounters;
		protected enum PerformanceCounterIndexes
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
			ErrorQueuedMessages = 14,
			MessagesPerSec = 15,
			TotalMessages = 16,
			Query = 17,
			Invoke = 18,
			AvgMsgLife = 19,
			AvgMsgLifeBase = 20,
			Notification = 21,

			SaveWithConfirm = 22,// SaveWithConfirm start added 11/9/09
			UpdateWithConfirm = 23,
			DeleteWithConfirm = 24,
			DeleteInAllTypesWithConfirm = 25,
			DeleteAllInTypeWithConfirm = 26,
			DeleteAllWithConfirm = 27,
			NotificationWithConfirm = 28,
			IncrementWithConfirm = 29,
			Increment = 30 //end add 11/9/09
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
			"Error Queued Messages",
			@"Msg/Sec",
			"Total Messages Sent",
			@"Msg/Sec - Query",
			@"Msg/Sec - Invoke",
			"Avg Message Life",
			"Avg Message Life Base",
			"Msg/Sec - Notification",
			@"Msg/Sec - Confirmed Save", 
			@"Msg/Sec - Confirmed Update",
			@"Msg/Sec - Confirmed Delete",			
			@"Msg/Sec - Confirmed Delete In All Types", 
			@"Msg/Sec - Confirmed Delete All In Type",
			@"Msg/Sec - Confirmed Delete All",
			@"Msg/Sec - Confirmed Notification",
			@"Msg/Sec - Confirmed Increment",
			@"Msg/Sec - Increment"
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
			"The number of messages that have been queued because of a communication error.",
			"The total number of messages per second.",
			"The total number of messages that have been sent.",
			"Query Messages Per Second",
			"Invoke Messages Per Second",
			"Average amount of time a message exists before being forwarded.",
			"Base for Average Life",
			"Notification Messages Per Second",
			"Confirmed Save Messages Per Second", 
			"Confirmed Update Messages Per Second",
			"Confirmed Delete Messages Per Second", 
			"Confirmed Delete In All Types Messages Per Second", 
			"Confirmed Delete All In Type Messages Per Second",
			"Confirmed Delete All Messages Per Seconds", 
			"Confirmed Notification Messages Per Second",
			"Confirmed Increment Messages Per Second",
			"Increment Messages Per Second"
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
			PerformanceCounterType.NumberOfItems32,
			PerformanceCounterType.RateOfCountsPerSecond32,
			PerformanceCounterType.NumberOfItems64,
			PerformanceCounterType.RateOfCountsPerSecond32,
			PerformanceCounterType.RateOfCountsPerSecond32,
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
			PerformanceCounterType.RateOfCountsPerSecond32 
		};
		#endregion

		private bool _countersInitialized;
		private MinuteAggregateCounter _hitCounter;
		private MinuteAggregateCounter _attemptCounter;
		private System.Timers.Timer _timer;
		private AverageMessageLife _avgMessageLife;
		private static readonly Logging.LogWrapper _log = new Logging.LogWrapper();

		internal void Initialize(string instanceName)
		{
			try
			{
				if (!PerformanceCounterCategory.Exists(PerformanceCategoryName))
				{
					InstallCounters();
				}
				else
				{
					_countersInitialized = true;
					//the below seems to have issues upon reinitialization. Just force using the installer.
					////check to see if all of the current counters are installed.
					//PerformanceCounterCategory perfCategory = new PerformanceCounterCategory(ForwardingCounters.PerformanceCategoryName);
					//bool recreate = false;

					//foreach (string counterName in ForwardingCounters.PerformanceCounterNames)
					//{
					//    if (!perfCategory.CounterExists(counterName))
					//    {
					//        LoggingWrapper.Write("Counter " + counterName + " not found in category " + ForwardingCounters.PerformanceCategoryName, "Relay");
					//        recreate = true;
					//        break;
					//    }
					//}
					//if (recreate)
					//{
					//    LoggingWrapper.Write("Recreating performance counter category " + ForwardingCounters.PerformanceCategoryName, "Relay");
					//    RemoveCounters();
					//    InstallCounters();
					//}
					//else
					//{
					//    countersInitialized = true;
					//}
				}
				if (_countersInitialized)
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

					if (PerformanceCounterCategory.CounterExists(PerformanceCounterNames[(int)PerformanceCounterIndexes.AvgMsgLife], 
						PerformanceCategoryName))
					{
						_avgMessageLife = new AverageMessageLife(
							PerformanceCounters[(int)PerformanceCounterIndexes.AvgMsgLife],
							PerformanceCounters[(int)PerformanceCounterIndexes.AvgMsgLifeBase]);
					}

					if (PerformanceCounterCategory.CounterExists(PerformanceCounterNames[(int)PerformanceCounterIndexes.SaveWithConfirm],
						PerformanceCategoryName))
					{
						nov09CountersExist = true;
					}
					else
					{
						_log.Warn("Confirmed Update Counters are not installed, please reinstall DataRelay counters.");
					}

					_hitCounter = new MinuteAggregateCounter();
					_attemptCounter = new MinuteAggregateCounter();
					ResetCounters();
					StartTimer();
				}
			}
			catch (System.Security.SecurityException)
			{
				if (_log.IsWarnEnabled)
					_log.Warn("Could not automatically install relay forwarding counters. Please run InstallUtil against MySpace.RelayComponent.Forwarding.dll to install counters manually.");
				_countersInitialized = false;
			}
			catch (UnauthorizedAccessException)
			{
				if (_log.IsWarnEnabled)
					_log.Warn("Could not automatically install relay forwarding counters. Please run InstallUtil against MySpace.RelayComponent.Forwarding.dll to install counters manually.");
				_countersInitialized = false;
			}
			catch (Exception ex)
			{
				if (_log.IsErrorEnabled)
					_log.ErrorFormat("Error initializing relay forwarding counters: {0}", ex);
				_countersInitialized = false;
			}
		}

		internal void ResetCounters()
		{
			if (_countersInitialized)
			{
				PerformanceCounters[(int)PerformanceCounterIndexes.HitRatio].RawValue = 0;
				PerformanceCounters[(int)PerformanceCounterIndexes.HitRatioBase].RawValue = 0;
				PerformanceCounters[(int)PerformanceCounterIndexes.ErrorQueuedMessages].RawValue = 0;
				PerformanceCounters[(int)PerformanceCounterIndexes.TotalMessages].RawValue = 0;
				PerformanceCounters[(int)PerformanceCounterIndexes.AvgMsgLife].RawValue = 0;
				PerformanceCounters[(int)PerformanceCounterIndexes.AvgMsgLifeBase].RawValue = 0;
			}
		}

		private void StartTimer()
		{
			_timer = new System.Timers.Timer {Interval = 1000, AutoReset = true};
			_timer.Elapsed += timer_Elapsed;
			_timer.Start();
		}

		void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			int attempts = _attemptCounter.Tick();
			int hits = _hitCounter.Tick();
			if (attempts > 0)
			{
				PerformanceCounters[(int)PerformanceCounterIndexes.HitRatio].RawValue = hits;
				PerformanceCounters[(int)PerformanceCounterIndexes.HitRatioBase].RawValue = attempts;
			}
		}

		private void RemoveCounters()
		{
			CounterInstaller.RemoveCounters();
			_countersInitialized = false;
		}

		private void InstallCounters()
		{
			_countersInitialized = CounterInstaller.InstallCounters();
		}

		private void CalculateLife(RelayMessage message)
		{
			if (_avgMessageLife == null) 
				return;
			
			_avgMessageLife.CalculateLife(message);
		}

		private void CalculateLife(SerializedRelayMessage message)
		{
			if (_avgMessageLife == null) 
				return;
			
			_avgMessageLife.CalculateLife(message);
		}

		internal void CountMessage(SerializedRelayMessage message)
		{
			if (_countersInitialized)
			{
				CalculateLife(message);

				if (message.IsTwoWayMessage)
				{
					CountOutMessage(message.MessageType, message.PayloadLength);
				}
				else
				{
					CountInMessage(message.MessageType, message.PayloadLength);
				}
			}
		}

		internal void CountMessage(RelayMessage message)
		{
			if (_countersInitialized)
			{
				CalculateLife(message);
				
				int length = 0;
				if (message.Payload != null && message.Payload.ByteArray != null) length = message.Payload.ByteArray.Length;
				if (message.IsTwoWayMessage)
				{
					CountOutMessage(message.MessageType, length);
				}
				else
				{
					CountInMessage(message.MessageType, length);
				}
			}
		}

		private void CountInMessage(MessageType messageType, int payloadLength)
		{
			if (_countersInitialized)
			{
				PerformanceCounters[(int)PerformanceCounterIndexes.MessagesPerSec].Increment();
				PerformanceCounters[(int)PerformanceCounterIndexes.TotalMessages].Increment();

				switch (messageType)
				{
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
					case MessageType.Save:
						PerformanceCounters[(int)PerformanceCounterIndexes.Save].Increment();
						if (payloadLength > 0)
						{
							PerformanceCounters[(int)PerformanceCounterIndexes.AvgInputBytes].IncrementBy(payloadLength);
						}
						PerformanceCounters[(int)PerformanceCounterIndexes.AvgInputBytesBase].Increment();
						break;
					case MessageType.Update:
						PerformanceCounters[(int)PerformanceCounterIndexes.Update].Increment();
						if (payloadLength > 0)
						{
							PerformanceCounters[(int)PerformanceCounterIndexes.AvgInputBytes].IncrementBy(payloadLength);
						}
						PerformanceCounters[(int)PerformanceCounterIndexes.AvgInputBytesBase].Increment();
						break;
					case MessageType.Notification:
						PerformanceCounters[(int)PerformanceCounterIndexes.Notification].Increment();
						if (payloadLength > 0)
						{
							PerformanceCounters[(int)PerformanceCounterIndexes.AvgInputBytes].IncrementBy(payloadLength);
						}
						PerformanceCounters[(int)PerformanceCounterIndexes.AvgInputBytesBase].Increment();
						break;
					case MessageType.Increment:
						if (nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.Notification].Increment();
						break;
				}
			}
		}

		private void CountOutMessage(MessageType messageType, int payloadLength)
		{
			if (_countersInitialized)
			{
				PerformanceCounters[(int)PerformanceCounterIndexes.MessagesPerSec].Increment();
				PerformanceCounters[(int)PerformanceCounterIndexes.TotalMessages].Increment();

				switch (messageType)
				{
					case MessageType.Get:
						PerformanceCounters[(int)PerformanceCounterIndexes.Get].Increment();
						_attemptCounter.IncrementCounter();
						if (payloadLength > 0)
						{
							_hitCounter.IncrementCounter();
						}
						break;
					case MessageType.Query:
						PerformanceCounters[(int)PerformanceCounterIndexes.Query].Increment();
						_attemptCounter.IncrementCounter();
						if (payloadLength > 0)
						{
							_hitCounter.IncrementCounter();
						}
						break;
					case MessageType.Invoke:
						PerformanceCounters[(int)PerformanceCounterIndexes.Invoke].Increment();
						_attemptCounter.IncrementCounter();
						if (payloadLength > 0)
						{
							_hitCounter.IncrementCounter();
						}
						break;
					case MessageType.NotificationWithConfirm:
						if (nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.NotificationWithConfirm].Increment();
						break;
					case MessageType.IncrementWithConfirm:
						if (nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.IncrementWithConfirm].Increment();
						break;
					case MessageType.SaveWithConfirm:
						if (nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.SaveWithConfirm].Increment();
						if (payloadLength > 0)
						{
							PerformanceCounters[(int)PerformanceCounterIndexes.AvgInputBytes].IncrementBy(payloadLength);
						}
						PerformanceCounters[(int)PerformanceCounterIndexes.AvgInputBytesBase].Increment();
						break;
					case MessageType.UpdateWithConfirm:
						if (nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.UpdateWithConfirm].Increment();
						if (payloadLength > 0)
						{
							PerformanceCounters[(int)PerformanceCounterIndexes.AvgInputBytes].IncrementBy(payloadLength);
						}
						PerformanceCounters[(int)PerformanceCounterIndexes.AvgInputBytesBase].Increment();
						break;
					case MessageType.DeleteWithConfirm:
						if (nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.DeleteWithConfirm].Increment();
						break;
					case MessageType.DeleteInAllTypesWithConfirm:
						if (nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.DeleteInAllTypesWithConfirm].Increment();
						break;
					case MessageType.DeleteAllWithConfirm:
						if (nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.DeleteAllWithConfirm].Increment();
						break;
					case MessageType.DeleteAllInTypeWithConfirm:
						if (nov09CountersExist == false) break;
						PerformanceCounters[(int)PerformanceCounterIndexes.DeleteAllInTypeWithConfirm].Increment();
						break;
				}
			}
		}

		internal void CountInMessages(IList<SerializedRelayMessage> messages)
		{
			if (_countersInitialized)
			{
				SerializedRelayMessage msg;
				for (int i = 0; i < messages.Count; i++)
				{
					msg = messages[i];
					CalculateLife(msg);
					CountInMessage(msg.MessageType, msg.PayloadLength);
				}
			}
		}
		
		internal void CountInMessages(SerializedRelayMessage[] messages)
		{
			if (_countersInitialized)
			{
				SerializedRelayMessage msg;
				for (int i = 0; i < messages.Length; i++)
				{
					msg = messages[i];
					CalculateLife(msg);
					CountInMessage(msg.MessageType, msg.PayloadLength);
				}
			}
		}

		internal void CountOutMessages(IList<RelayMessage> messages)
		{
			if (_countersInitialized)
			{
				RelayMessage msg;
				for (int i = 0; i < messages.Count; i++)
				{
					msg = messages[i];
					CalculateLife(msg);
					int length = 0;
					if (msg.Payload != null && msg.Payload.ByteArray != null) length = msg.Payload.ByteArray.Length;
					CountOutMessage(messages[i].MessageType, length);
				}
			}
		}

		internal void CountMessageList(IList<RelayMessage> messages)
		{
			if (_countersInitialized)
			{
				PerformanceCounters[(int)PerformanceCounterIndexes.AvgBulkLength].IncrementBy(messages.Count);
				PerformanceCounters[(int)PerformanceCounterIndexes.AvgBulkLengthBase].Increment();
			}
		}

		internal void SetNumberOfQueuedMessages(int count)
		{
			if (_countersInitialized)
			{
				PerformanceCounters[(int)PerformanceCounterIndexes.QueuedInMessages].RawValue = count;
			}
		}

		internal void IncrementErrorQueue()
		{
			if (_countersInitialized)
			{
				PerformanceCounters[(int)PerformanceCounterIndexes.ErrorQueuedMessages].Increment();
			}
		}

		internal void IncrementErrorQueueBy(long count)
		{
			if (_countersInitialized)
			{
				PerformanceCounters[(int)PerformanceCounterIndexes.ErrorQueuedMessages].IncrementBy(count);
			}
		}

		internal void DecrementErrorQueue()
		{
			if (_countersInitialized)
			{
				PerformanceCounters[(int)PerformanceCounterIndexes.ErrorQueuedMessages].Decrement();
			}
		}

		internal void DecrementErrorQueueBy(long count)
		{
			if (_countersInitialized)
			{
				PerformanceCounters[(int)PerformanceCounterIndexes.ErrorQueuedMessages].IncrementBy(-1 * count);
			}
		}

		internal void Shutdown()
		{
			if (_countersInitialized)
			{
				_countersInitialized = false;
				foreach (PerformanceCounter counter in PerformanceCounters)
				{
					counter.Close();
					counter.Dispose();
				}
			}
		}
	}
}
