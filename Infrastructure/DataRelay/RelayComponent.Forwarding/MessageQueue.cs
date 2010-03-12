using System.Collections.Generic;
using MySpace.Common;
using MySpace.DataRelay.Common.Schemas;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	
	internal class MessageQueue : IVersionSerializable
	{
		/// <summary>
		/// Only for serialization support. Do not use.
		/// </summary>
		public MessageQueue() { } 

		internal MessageQueue(QueueConfig config)
		{
			if (config != null)
			{
				_enabled = config.Enabled;
				_itemsPerDequeue = config.ItemsPerDequeue;
				_maxCount = config.MaxCount;
			}
		}

		internal void ReloadConfig(QueueConfig config)
		{
			if (config != null)
			{
				_itemsPerDequeue = config.ItemsPerDequeue;
				_maxCount = config.MaxCount;
				_enabled = config.Enabled; //do this last so if it's switching on for the first time 
										  //the settings will be in place when it starts up
			}
		}

		private bool _enabled;
		private int _maxCount = 1000;
		private int _itemsPerDequeue = 100;
		
		private readonly object _inMessageQueueLock = new object();
		private readonly object _inMessageQueueCreateLock = new object();
		private Queue<SerializedRelayMessage> _inMessageQueue;
		private Queue<SerializedRelayMessage> InMessageQueue
		{
			get
			{
				if (_inMessageQueue == null)
				{
					lock (_inMessageQueueCreateLock)
					{
						if (_inMessageQueue == null)
						{
							_inMessageQueue = new Queue<SerializedRelayMessage>(100);
						}
					}
				}
				return _inMessageQueue;
			}
		}
		internal int InMessageQueueCount
		{
			get
			{
				if (_inMessageQueue != null)
				{
					return _inMessageQueue.Count;
				}
				return 0;
			}
		}
			
		internal void Enqueue(SerializedRelayMessage message)
		{
			if (_enabled)
			{
				if (message.IsTwoWayMessage)
				{
					return;
				}
				lock (_inMessageQueueLock)
				{
					while (InMessageQueue.Count >= (_maxCount - 1))
					{
						Forwarder.RaiseMessageDropped(InMessageQueue.Dequeue());
						NodeManager.Instance.Counters.DecrementErrorQueue();
					}
					NodeManager.Instance.Counters.IncrementErrorQueue();
					InMessageQueue.Enqueue(message);
				}
			}
			else
			{
				Forwarder.RaiseMessageDropped(message);
			}
		}

		internal void Enqueue(IList<SerializedRelayMessage> messages)
		{
			if (_enabled && messages.Count > 0)
			{
				lock (_inMessageQueueLock)
				{
					while (InMessageQueue.Count > 0 && InMessageQueue.Count >= (_maxCount - messages.Count))
					{
						Forwarder.RaiseMessageDropped(InMessageQueue.Dequeue());
						NodeManager.Instance.Counters.DecrementErrorQueue();
					}
					for (int i = 0; i < messages.Count; i++)
					{
						InMessageQueue.Enqueue(messages[i]);
					}					
				}
				NodeManager.Instance.Counters.IncrementErrorQueueBy(messages.Count);
			}
			else
			{
				for (int i = 0; i < messages.Count; i++)
				{
					Forwarder.RaiseMessageDropped(messages[i]);
				}
			}
		}

		internal SerializedMessageList Dequeue()
		{
			SerializedMessageList list = null;
			
			if (_enabled && InMessageQueueCount > 0)
			{
				int dequeueCount = 0;
				
				list = new SerializedMessageList();
		
				lock (_inMessageQueueLock)
				{
					for (; _inMessageQueue.Count > 0 && dequeueCount < _itemsPerDequeue; dequeueCount++)
					{
						list.Add(_inMessageQueue.Dequeue());
					}
				}
				NodeManager.Instance.Counters.DecrementErrorQueueBy(list.InMessages.Count);
			}
			return list;
		}


		#region IVersionSerializable Members

		public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			lock (_inMessageQueueLock)
			{
				writer.Write(_enabled);
				writer.Write(_maxCount);
				writer.Write(_itemsPerDequeue);
				if (_inMessageQueue != null)
				{
					writer.Write(true);
					writer.Write(_inMessageQueue.Count);
					foreach (SerializedRelayMessage message in _inMessageQueue)
					{
						writer.Write<SerializedRelayMessage>(message, false);
					}
				}
				else
				{
					writer.Write(false);
				}
			}
		}

		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
		{
			//TODO figure out if this reverses the order of the queue
			_enabled = reader.ReadBoolean();
			_maxCount = reader.ReadInt32();
			_itemsPerDequeue = reader.ReadInt32();
			if (reader.ReadBoolean())
			{
				int count = reader.ReadInt32();
				_inMessageQueue = new Queue<SerializedRelayMessage>(count);
				for (int i = 0; i < count; i++)
				{
					_inMessageQueue.Enqueue(reader.Read<SerializedRelayMessage>());
				}
			}
		}

		public int CurrentVersion
		{
			get { return 1; }
		}

		public bool Volatile
		{
			get { return false; }
		}

		#endregion

		#region ICustomSerializable Members


		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
		{
			Deserialize(reader,CurrentVersion);
		}

		#endregion
	}
}
