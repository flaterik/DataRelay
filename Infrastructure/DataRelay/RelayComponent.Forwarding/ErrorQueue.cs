using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MySpace.Common;
using MySpace.Common.IO;
using MySpace.DataRelay.Common.Schemas;
using MySpace.Logging;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// Queue relay messages that have errored for later retry.
	/// </summary>
	/// <remarks>
	/// Save messages and other one-way relay messages are typically forwarded to all nodes in a cluster
	/// so that each node has consistent data.  If a node is unreachable when an attempt is made to forward
	/// such a message, the message will be placed in the error queue for that node.  The message will be retried
	/// when the destination node becomes available.
	/// </remarks>
	internal class ErrorQueue : IVersionSerializable
	{
		private static readonly LogWrapper _log = new LogWrapper();

		/// <summary>
		/// <para>
		/// Initializes a new instance of the <see cref="ErrorQueue"/> class.
		/// </para>
		/// <para>
		/// (Parameterless constructor only for serialization support. Do not use.)
		/// </para>
		/// </summary>
		public ErrorQueue() { }

		/// <summary>
		/// Initializes a new instance of the <see cref="ErrorQueue"/> class.
		/// </summary>
		/// <param name="config">The QueueConfig settings from RelayComponents.config.</param>
		/// <param name="nodeDefinition">The RelayNodeDefinition settings from RelayNodeMapping.config.</param>
		internal ErrorQueue(QueueConfig config, RelayNodeDefinition nodeDefinition)
		{
			_nodeName = string.Format("{0}-{1}", nodeDefinition.Host, nodeDefinition.Port);

			SetConfig(config);
		}

		/// <summary>
		/// Reloads the configuration.
		/// </summary>
		/// <param name="config">The new QueueConfig settings.</param>
		internal void ReloadConfig(QueueConfig config)
		{
			SetConfig(config);
		}

		private void SetConfig(QueueConfig config)
		{
			if (config != null)
			{
				if (!config.Enabled)
					_enabled = false;

				_itemsPerDequeue = config.ItemsPerDequeue;
				_maxCount = config.MaxCount;

				_persistence = ErrorQueuePersistence.Initialize(config.PersistenceFolder, _nodeName, _itemsPerDequeue,
				                                                config.PersistenceFileSize, config.MaxPersistedMB,
				                                                DequeueAllMessages, ProbeMessages);

				if (config.Enabled)
					_enabled = true;
			}
		}

		private readonly string _nodeName;
		private ErrorQueuePersistence _persistence;

		private bool _enabled;
		private int _maxCount = 1000;
		private int _itemsPerDequeue = 100;
		private int _discardCount;

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

							if (_persistence != null && !_persistence.CreateSpillFolder())
								_persistence = null;
						}
					}
				}
				return _inMessageQueue;
			}
		}

		/// <summary>
		/// Gets the approximate error queue message count.
		/// </summary>
		/// <value>The number of messages in the error queue.</value>
		internal int InMessageQueueCount
		{
			get
			{
				if (_persistence != null)
					return _persistence.MessageCount;

				if (_inMessageQueue != null)
					return _inMessageQueue.Count;

				return 0;
			}
		}

		/// <summary>
		/// Enqueues a single message to the error queue.
		/// </summary>
		/// <remarks>
		/// Two-way messages are ignored.
		/// </remarks>
		/// <param name="message">The message.</param>
		internal void Enqueue(SerializedRelayMessage message)
		{
			if (message.IsTwoWayMessage)
				return;

			SerializedRelayMessage discard = null;

			if (_enabled)
			{
				lock (_inMessageQueueLock)
				{
					NodeManager.Instance.Counters.IncrementErrorQueue();
					InMessageQueue.Enqueue(message);

					if (_persistence == null && InMessageQueue.Count > _maxCount)
					{
						discard = InMessageQueue.Dequeue();
					}
				}

				if (discard != null)
				{
					Forwarder.RaiseMessageDropped(discard);
					NodeManager.Instance.Counters.DecrementErrorQueue();
					IncrementDiscardsBy(1);
				}

				StartSpill();
			}
			else
			{
				Forwarder.RaiseMessageDropped(message);
			}
		}

		/// <summary>
		/// Enqueues a list of messages to the error queue.
		/// </summary>
		/// <remarks>
		/// Two-way messages in the message list are ignored.
		/// </remarks>
		/// <param name="messages">The list of messages.</param>
		internal void Enqueue(IList<SerializedRelayMessage> messages)
		{
			if (_enabled && messages.Count > 0)
			{
				int count = 0;
				SerializedRelayMessage[] discards = null;

				lock (_inMessageQueueLock)
				{
					for (int i = 0; i < messages.Count; ++i)
					{
						var message = messages[i];
						if (!message.IsTwoWayMessage)
						{
							InMessageQueue.Enqueue(message);
							++count;
						}
					}

					if (_persistence == null && InMessageQueue.Count > _maxCount)
					{
						discards = new SerializedRelayMessage[InMessageQueue.Count - _maxCount];
						for (int i = 0; i < discards.Length; ++i)
						{
							discards[i] = InMessageQueue.Dequeue();
						}
					}
				}

				if (discards != null)
				{
					for (int i = 0; i < discards.Length; ++i)
					{
						Forwarder.RaiseMessageDropped(discards[i]);
					}
					NodeManager.Instance.Counters.DecrementErrorQueueBy(discards.Length);
					IncrementDiscardsBy(discards.Length);
				}

				if (count > 0)
				{
					NodeManager.Instance.Counters.IncrementErrorQueueBy(count);
					StartSpill();
				}
			}
			else
			{
				for (int i = 0; i < messages.Count; i++)
				{
					var message = messages[i];
					if (!message.IsTwoWayMessage)
						Forwarder.RaiseMessageDropped(message);
				}
			}
		}

		/// <summary>
		/// Dequeues a list of messages from the error queue.  At most the number of messages
		/// specified by the ItemsPerDequeue parameter of the QueueConfig will be returned.
		/// </summary>
		/// <returns>A <see cref="SerializedMessageList"/> containing the dequeued messages.</returns>
		internal SerializedMessageList Dequeue()
		{
			SerializedMessageList list = null;
			
			if (_enabled && InMessageQueueCount > 0)
			{
				list = new SerializedMessageList();

				if (_persistence != null)
				{
					_persistence.Dequeue(list);
				}
				else
				{
					lock (_inMessageQueueLock)
					{
						for (int dequeueCount=0; _inMessageQueue.Count > 0 && dequeueCount < _itemsPerDequeue; dequeueCount++)
						{
							list.Add(_inMessageQueue.Dequeue());
						}
					}
				}

				// There is a slight possibility that _persistence.Dequeue can return no messages.
				if (list.InMessages.Count == 0)
				{
					list = null;
				}
				else
				{
					NodeManager.Instance.Counters.DecrementErrorQueueBy(list.InMessages.Count);
				}
			}

			return list;
		}

		/// <summary>
		/// Populates this error queue from the messages stored in a previous instance of the error queue.
		/// </summary>
		/// <remarks>
		/// <para>
		/// When an AppDomain restart occurs the error queues are serialized into the Forwarding component's
		/// RunState.  Upon restart they are deserialized and all messages from the memory queue from the old
		/// AppDomain are copied into the error queues for the new AppDomain.
		/// </para>
		/// <para>
		/// If persistent error queues are enabled then the memory queues will normally be empty as they are
		/// flushed to perisistent storage before serializing the error queues.
		/// </para>
		/// </remarks>
		/// <param name="errorQueue">The original error queue.</param>
		/// <param name="incrementCounters">if set to <see langword="true"/> the performance counter for messages
		/// in the error queue will be incremented by the number of messages in the source error queue.</param>
		internal void Populate(ErrorQueue errorQueue, bool incrementCounters)
		{
			if (errorQueue.InMessageQueueCount == 0) return;

			if (_persistence != null && !_persistence.CreateSpillFolder())
				_persistence = null;

			lock (_inMessageQueueLock)
			{
				SerializedRelayMessage[] curMessages = null;
				if (InMessageQueue.Count > 0)
				{
					curMessages = InMessageQueue.ToArray();
					InMessageQueue.Clear();
				}

				foreach (var message in errorQueue.InMessageQueue)
				{
					InMessageQueue.Enqueue(message);
				}

				if (curMessages != null)
				{
					foreach (var message in curMessages)
					{
						InMessageQueue.Enqueue(message);
					}
				}
			}

			if (incrementCounters)
			{
				NodeManager.Instance.Counters.IncrementErrorQueueBy(errorQueue.InMessageQueue.Count);
			}

			StartSpill();
		}

		private SerializedRelayMessage[] DequeueAllMessages()
		{
			lock (_inMessageQueueLock)
			{
				if (_inMessageQueue != null && _inMessageQueue.Count > 0)
				{
					var messages = _inMessageQueue.ToArray();
					_inMessageQueue.Clear();

					return messages;
				}
			}

			return null;
		}

		private bool ProbeMessages()
		{
			return _inMessageQueue != null && _inMessageQueue.Count > 0;
		}

		private void StartSpill()
		{
			if (_persistence != null) _persistence.StartSpill();
		}

		/// <summary>
		/// Waits for any spill to persistent storage to complete.
		/// </summary>
		private void WaitForSpill()
		{
			if (_persistence != null) _persistence.WaitForSpill();
		}

		private void IncrementDiscardsBy(int count)
		{
			Interlocked.Add(ref _discardCount, count);
			NodeManager.Instance.Counters.IncrementErrorQueueDiscardsBy(count);
		}

		/// <summary>
		/// Gets the count of files in use by the persistent error queue.
		/// </summary>
		/// <value>The file count.</value>
		public int FileCount { get { return _persistence == null ? 0 : _persistence.FileCount; }}

		public int DiscardCount
		{
			get
			{
				int count = _discardCount;
				if (_persistence != null)
				{
					count += _persistence.DiscardCount;
				}
				return count;
			}
		}

		#region IVersionSerializable Members

		/// <summary>
		/// Serialize the class data to a stream.
		/// </summary>
		/// <param name="writer">The <see cref="T:MySpace.Common.IO.IPrimitiveWriter"/> that writes to the stream.</param>
		public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			// If persistence is enabled, try to drain the queue before we serialize.
			StartSpill();
			WaitForSpill();

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

		/// <summary>
		/// Deserialize the class data from a stream.
		/// </summary>
		/// <param name="reader">The <see cref="T:MySpace.Common.IO.IPrimitiveReader"/> that extracts used to extra data from a stream.</param>
		/// <param name="version">The value of <see cref="P:MySpace.Common.IVersionSerializable.CurrentVersion"/> that was written to the stream when it was originally serialized to a stream;
		/// the version of the <paramref name="reader"/> data.</param>
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

		/// <summary>
		/// Gets the current serialization data version of your object.  The <see cref="M:MySpace.Common.IVersionSerializable.Serialize(MySpace.Common.IO.IPrimitiveWriter)"/> method
		/// will write to the stream the correct format for this version.
		/// </summary>
		/// <value></value>
		public int CurrentVersion
		{
			get { return 1; }
		}

		/// <summary>
		/// Deprecated. Has no effect.
		/// </summary>
		/// <value></value>
		public bool Volatile
		{
			get { return false; }
		}

		#endregion

		#region ICustomSerializable Members


		/// <summary>
		/// Deserialize data from a stream
		/// </summary>
		/// <param name="reader">The data stream.</param>
		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
		{
			Deserialize(reader,CurrentVersion);
		}

		#endregion
	}
}
