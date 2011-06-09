using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Ccr.Core;
using MySpace.DataRelay.Configuration;
using System.Reflection;


namespace MySpace.DataRelay
{
	class MessageTracer
	{
		private StreamWriter _traceoutputStream;
		private bool _writingToFile;
		private bool _writingToDiagnostic;
		private readonly Dispatcher _traceDispatcher;
		private readonly DispatcherQueue _traceMessageQueue;
		readonly Port<RelayMessage> _tracePort = new Port<RelayMessage>();
		readonly Port<IList<RelayMessage>> _traceListPort = new Port<IList<RelayMessage>>();
		readonly Port<string> _stringPort = new Port<string>();

		private string _outputTraceFileName;

		private bool[] _messageTypesFilter = new bool[(int)MessageType.NumTypes];
		private bool[] _messageTypeIdsFilter;
		private bool[] _extendedIdDecodeFilter;
		private int _sampleSeconds;
		private readonly Timer _sampleTimer;


		internal MessageTracer(short maxTypeId, TraceSettings traceSettings)
		{
			//dispatchers and dispatcher queues need unique names. it's unlikely that a single class could want more than one of these.
			//blatantly stolen from mr custenborders logwrapper class
			string name = ExtractClassName(new StackFrame(1).GetMethod()); 

			_sampleTimer = new Timer(SampleTimerCallback);

			ReloadConfig(maxTypeId, traceSettings);

			_traceDispatcher = new Dispatcher(1, ThreadPriority.BelowNormal, true, name + "TraceDispatcher"); //using only 1 thread keeps us from needing any locking on the stream output

			_traceMessageQueue = new DispatcherQueue(name + "DispatcherQueue", _traceDispatcher, 
				TaskExecutionPolicy.ConstrainQueueDepthDiscardTasks, 10000);

			Arbiter.Activate(_traceMessageQueue,
								Arbiter.Receive<RelayMessage>(true, _tracePort, DoWriteTraceInfo));
			
			Arbiter.Activate(_traceMessageQueue,
						Arbiter.Receive<IList<RelayMessage>>(true, _traceListPort, DoWriteTraceInfo));

			Arbiter.Activate(_traceMessageQueue,
				Arbiter.Receive<string>(true, _stringPort, DoWriteTraceInfo));
		}

		private static string ExtractClassName(MethodBase callingMethod)
		{
			if (callingMethod == null)
			{
				return "Unknown";
			}
			Type callingType = callingMethod.DeclaringType;
			if (callingType != null)
			{
				return callingType.FullName;
			}
			string name = callingMethod.Name;
			int lastDotIndex = name.LastIndexOf('.');
			if (lastDotIndex > 0)
			{
				name = name.Substring(0, lastDotIndex);
			}
			return name;
		}



		/// <summary>
		/// Sets which message types should be traced. If this is set to null, all message types are traced.
		/// </summary>
		/// <param name="messageTypesToTrace"></param>
		internal void SetMessageTypeFilter(MessageType[] messageTypesToTrace)
		{
			bool[] newMessageTypesFilter = new bool[(int)MessageType.NumTypes];
			
			if(messageTypesToTrace == null || messageTypesToTrace.Length == 0) //trace all message types
			{
				for (int i = 0; i < newMessageTypesFilter.Length; i++)
				{
					newMessageTypesFilter[i] = true;
				}
			}
			else
			{
				for (int i = 0; i < newMessageTypesFilter.Length; i++)
				{
					newMessageTypesFilter[i] = false;
				}
				foreach (MessageType typeToTrace in messageTypesToTrace)
				{
					newMessageTypesFilter[(int) typeToTrace] = true;
				}
			}
			_messageTypesFilter = newMessageTypesFilter;
		}

		/// <summary>
		/// Sets which message type Ids should be traced. If this is set to null, all messages type ids will be traced.
		/// </summary>
		/// <param name="messageTypeIdsToTrace"></param>
		internal void SetMessageTypeIdFilter(short[] messageTypeIdsToTrace)
		{
			bool[] newMessageTypeIdsFilter = new bool[MaxTypeId+1];

			if (messageTypeIdsToTrace == null || messageTypeIdsToTrace.Length == 0) //trace all message types
			{
				for (int i = 0; i < newMessageTypeIdsFilter.Length; i++)
				{
					newMessageTypeIdsFilter[i] = true;
				}
			}
			else
			{
				for (int i = 0; i < newMessageTypeIdsFilter.Length; i++)
				{
					newMessageTypeIdsFilter[i] = false;
				}
				foreach (short typeIdToTrace in messageTypeIdsToTrace)
				{
					newMessageTypeIdsFilter[typeIdToTrace] = true;
				}
			}
			_messageTypeIdsFilter = newMessageTypeIdsFilter;
		}

		/// <summary>
		/// Sets which message type Ids should have their extended ids decoded. If this is set to null, no messages type ids will have their extended Ids decoded.
		/// </summary>
		/// <param name="messageTypeIdsToDecode"></param>
		internal void SetExtendedIdDecodeFilter(short[] messageTypeIdsToDecode)
		{
			bool[] newExtendedIdDecodeFilter = new bool[MaxTypeId + 1];

			if (messageTypeIdsToDecode == null || messageTypeIdsToDecode.Length == 0) //trace all message types
			{
				for (int i = 0; i < newExtendedIdDecodeFilter.Length; i++)
				{
					newExtendedIdDecodeFilter[i] = false;
				}
			}
			else
			{
				for (int i = 0; i < newExtendedIdDecodeFilter.Length; i++)
				{
					newExtendedIdDecodeFilter[i] = false;
				}
				foreach (short typeIdToDecode in messageTypeIdsToDecode)
				{
					newExtendedIdDecodeFilter[typeIdToDecode] = true;
				}
			}
			_extendedIdDecodeFilter = newExtendedIdDecodeFilter;
		}
		
		/// <summary>
		/// Writes the given message to the trace
		/// </summary>
		private void DoWriteTraceInfo(RelayMessage message)
		{
			if (MessageShouldBeTraced(message))
			{
				string logMessage = "Relay Node Got " + message.ToString(true, _extendedIdDecodeFilter[message.TypeId]);
				Trace.WriteLine(logMessage);
				WriteMessageTraceToFile(logMessage);
			}
		}

		private bool MessageShouldBeTraced(RelayMessage message)
		{
			return 
				_messageTypesFilter[(int)message.MessageType] &&
				_messageTypeIdsFilter[message.TypeId];
		}

		private bool MessageShouldBeTraced(MessageType messageType, short messageTypeId)
		{
			return
				_messageTypesFilter[(int)messageType] &&
				_messageTypeIdsFilter[messageTypeId];
		}

		/// <summary>
		/// Writes each of the given messages to the trace
		/// </summary>
		private void DoWriteTraceInfo(IList<RelayMessage> messages)
		{
			for (int i = 0; i < messages.Count; i++)
			{
				if (MessageShouldBeTraced(messages[i]))
				{
					string logMessage = "Relay Node Got " + messages[i].ToString(true, _extendedIdDecodeFilter[messages[i].TypeId]);
					if (_writingToDiagnostic)
						Trace.WriteLine(logMessage);
					WriteMessageTraceToFile(logMessage);
				}
			}
		}

		private void DoWriteTraceInfo(string message)
		{
			if (_writingToDiagnostic)
				Trace.WriteLine(message);
			WriteMessageTraceToFile(message);
		}
		
		private void WriteMessageTraceToFile(string logMessage)
		{
			try
			{
				if (_writingToFile)
					_traceoutputStream.WriteLine("{0}: {1}", DateTime.Now.TimeOfDay, logMessage);
			}
			catch(ObjectDisposedException)
			{
				//there is a very small chance of a failed race when tearing down the trace file. if that happens we can just ignore it
			}
		}

		internal void WriteMessageInfo(RelayMessage message)
		{
			if (Activated && message != null)
			{
				message.SetTracePoint();
				_tracePort.Post(message);
			}
		}

		internal void WriteMessageInfo(IList<RelayMessage> messages)
		{
			if (Activated && messages != null)
			{
				SetTracePoints(messages);
				_traceListPort.Post(messages);
			}
		}
		
		private static void SetTracePoints(IList<RelayMessage> messages)
		{
			for (int i = 0; i < messages.Count; i++)
			{
				messages[i].SetTracePoint();
			}
		}

		internal void WriteLogMessage(MessageType originatingMessageType, short originatingMessageTypeId, string logMessage)
		{
			if(Activated && MessageShouldBeTraced(originatingMessageType, originatingMessageTypeId))
			{
				_stringPort.Post(logMessage);
			}
		}

		private bool outputtingTraceInfo;
		internal bool Activated
		{
			get { return outputtingTraceInfo; }
			set
			{
				if (!outputtingTraceInfo && value) //turning tracing on from off
				{
					SetupTraceFile();
					outputtingTraceInfo = true;
					StartSampleTimer();
				}
				else if (outputtingTraceInfo && !value) //turning tracing off from on
				{
					outputtingTraceInfo = false;
					TeardownTraceFile();
				}
			}
		}

		private void StartSampleTimer()
		{
			if (_sampleSeconds < 1)
				return;
			_sampleTimer.Change(_sampleSeconds * 1000, Timeout.Infinite);
		}

		public void SampleTimerCallback(object state)
		{
			_sampleTimer.Change(Timeout.Infinite, Timeout.Infinite);
			Activated = false;
		}

		private short maxTypeId;
		internal short MaxTypeId
		{
			get { return maxTypeId; }
			set
			{
				if(value != maxTypeId)
				{
					bool tracingAllIds = true;
					bool[] newMessageTypeIdsFilter = new bool[value+1];
					for (int i = 0; i < newMessageTypeIdsFilter.Length; i++)
					{
						if(i < _messageTypeIdsFilter.Length)
						{
							newMessageTypeIdsFilter[i] = _messageTypeIdsFilter[i];
							if(!_messageTypeIdsFilter[i])
								tracingAllIds = false;
						}
						else
						{
							newMessageTypeIdsFilter[i] = tracingAllIds; 
						}
					}
					_messageTypeIdsFilter = newMessageTypeIdsFilter;
					maxTypeId = value;
				}
			}
		}

		private void SetupTraceFile()
		{
			if (String.IsNullOrEmpty(_outputTraceFileName))
				return;

			if (_traceoutputStream != null)
			{
				_traceoutputStream.Flush();
				_traceoutputStream.Dispose();

			}
			_traceoutputStream = new StreamWriter(_outputTraceFileName, false) {AutoFlush = false};
			_traceoutputStream.WriteLine("Trace started at {0}", DateTime.Now);

			_writingToFile = true;

		}

		private void TeardownTraceFile()
		{
			_writingToFile = false;

			if (_traceoutputStream != null)
			{
				_traceoutputStream.WriteLine("Trace ended at {0}", DateTime.Now);
				_traceoutputStream.Flush();
				_traceoutputStream.Dispose();
				_traceoutputStream = null;
			}
		}

		internal void ReloadConfig(short newMaxTypeId, TraceSettings traceSettings)
		{
			
			maxTypeId = newMaxTypeId;

			if (traceSettings != null)
			{
				_sampleSeconds = traceSettings.SampleSeconds;
				_outputTraceFileName = traceSettings.TraceFilename;
				_writingToDiagnostic = traceSettings.WriteToDiagnostic;
				SetMessageTypeFilter(traceSettings.GetTracedMessageTypeEnums());
				SetMessageTypeIdFilter(traceSettings.TracedMessageTypeIds);
				SetExtendedIdDecodeFilter(traceSettings.DecodeExtendedIdTypeIds);
			}
			else
			{
				_writingToDiagnostic = true;
				_sampleSeconds = 0;
				_outputTraceFileName = null;
				SetMessageTypeFilter(null);
				SetMessageTypeIdFilter(null);
				SetExtendedIdDecodeFilter(null);
			}
		}
	}
}
