using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using MySpace.Common;
using MySpace.DataRelay;
using MySpace.DataRelay.Common.Schemas;
using MySpace.DataRelay.Formatters;
using MySpace.DataRelay.Transports;

namespace MySpace.RelayComponent.Forwarding.Test
{
	/// <summary>
	/// Provides a Mock <see cref="IRelayTransport"/> and <see cref="IRelayTransportExtended"/> for testing.
	/// </summary>
	public class MockTransport : IAsyncRelayTransport, IRelayTransportExtended
	{
		private readonly RelayNodeDefinition _node;
		private readonly RelayNodeGroupDefinition _group;

		/// <summary>Initializes an instance of the <see cref="MockTransport"/> class.</summary>
		/// <param name="nodeDefinition">The node definition.</param>
		/// <param name="groupDefinition">The group definition.</param>
		public MockTransport(RelayNodeDefinition nodeDefinition,
			RelayNodeGroupDefinition groupDefinition)
		{
			if (nodeDefinition == null) throw new ArgumentNullException("nodeDefinition");
			if (groupDefinition == null) throw new ArgumentNullException("groupDefinition");

			_node = nodeDefinition;
			_group = groupDefinition;
			DoDispatchMessages = true;
		}

		/// <summary>
		/// Gets or sets a value indicating if messages received should call the 
		/// <see cref="MessageRecievedMethod"/> and <see cref="MessageListRecievedMethod"/>
		/// delegates.
		/// </summary>
		public bool DoDispatchMessages { get; set; }

		/// <summary>
		/// This delegate is called when a single message is received.
		/// </summary>
		public Action<RelayMessage, RelayNodeDefinition, RelayNodeGroupDefinition> MessageRecievedMethod;

		/// <summary>
		/// This delegate is called when a list of messages are received.
		/// </summary>
		public Action<IList<RelayMessage>, RelayNodeDefinition, RelayNodeGroupDefinition> MessageListRecievedMethod;

		#region IRelayTransport Members

		void IRelayTransport.SendMessage(RelayMessage message)
		{
			_messageRecieved(message);
		}

		private void _messageRecieved(RelayMessage message)
		{
			if (DoDispatchMessages == false) return;

			if (MessageRecievedMethod != null)
			{
				MessageRecievedMethod(message, _node, _group);
			}
		}

		private void _messageListRecieved(IList<RelayMessage> messages)
		{
			if (DoDispatchMessages == false) return;

			if (MessageListRecievedMethod != null)
			{
				MessageListRecievedMethod(messages, _node, _group);
			}
		}

		void IRelayTransport.SendMessage(SerializedRelayMessage serializedMessage)
		{
			if (DoDispatchMessages)
			{
				RelayMessage message = RelayMessageFormatter.ReadRelayMessage(serializedMessage.MessageStream);
				_messageRecieved(message);
			}
		}

		void IRelayTransport.SendInMessageList(SerializedRelayMessage[] messages)
		{
			_serializedMessageListReceive(messages);
		}

		private void _serializedMessageListReceive(IEnumerable<SerializedRelayMessage> messages)
		{
			if (DoDispatchMessages)
			{
				using (var stream = new MemoryStream())
				{
					foreach (var message in messages)
					{
						message.MessageStream.WriteTo(stream);
					}
					List<RelayMessage> messageList = RelayMessageFormatter.ReadRelayMessageList(stream);
					_messageListRecieved(messageList);
				}
			}
		}

		void IRelayTransport.SendInMessageList(List<SerializedRelayMessage> messages)
		{
			_serializedMessageListReceive(messages);
		}

		void IRelayTransport.SendOutMessageList(List<RelayMessage> messages)
		{
			_messageListRecieved(messages);
		}

		void IRelayTransport.GetConnectionStats(out int openConnections, out int activeConnections)
		{
			openConnections = 0;
			activeConnections = 0;
		}

		#endregion

		#region IRelayTransportExtended Members

		void IRelayTransportExtended.SendSyncMessage(MySpace.DataRelay.RelayMessage message)
		{
			_messageRecieved(message);
		}

		void IRelayTransportExtended.SendSyncMessageList(List<MySpace.DataRelay.RelayMessage> messages)
		{
			_messageListRecieved(messages);
		}

		#endregion

		private class SimpleAsyncResult : IAsyncResult
		{
			private readonly object _syncRoot = new object();
			private readonly AsyncCallback _callback;
			private readonly object _asyncState;
			private readonly MonitorWaitHandle _handle = new MonitorWaitHandle(false, EventResetMode.ManualReset);
			private bool _isComplete;

			public SimpleAsyncResult(AsyncCallback callback, object asyncState)
			{
				_callback = callback;
				_asyncState = asyncState;
			}

			public void SetComplete()
			{
				lock (_syncRoot)
				{
					_isComplete = true;
					_handle.Set();
				}
				if (_callback != null) _callback(this);
			}

			#region IAsyncResult Members

			object IAsyncResult.AsyncState
			{
				get { return _asyncState; }
			}

			WaitHandle IAsyncResult.AsyncWaitHandle
			{
				get { return _handle; }
			}

			bool IAsyncResult.CompletedSynchronously
			{
				get { return false; }
			}

			bool IAsyncResult.IsCompleted
			{
				get { return _isComplete; }
			}

			#endregion
		}

		#region IAsyncRelayTransport Members

		IAsyncResult IAsyncRelayTransport.BeginSendMessage(RelayMessage message, bool forceRoundTrip, AsyncCallback callback, object state)
		{
			var result = new SimpleAsyncResult(callback, state);
			if (forceRoundTrip)
			{
				ThreadPool.QueueUserWorkItem(o =>
				{
					((IRelayTransportExtended)this).SendSyncMessage(message);
					result.SetComplete();
				});
				return result;
			}
			ThreadPool.QueueUserWorkItem(o =>
			{
				((IRelayTransport)this).SendMessage(message);
				result.SetComplete();
			});
			return result;
		}

		IAsyncResult IAsyncRelayTransport.BeginSendMessage(SerializedRelayMessage message, AsyncCallback callback, object state)
		{
			var result = new SimpleAsyncResult(callback, state);
			ThreadPool.QueueUserWorkItem(o =>
			{
				((IRelayTransport)this).SendMessage(message);
				result.SetComplete();
			});
			return result;
		}

		void IAsyncRelayTransport.EndSendMessage(IAsyncResult result)
		{
			result.AsyncWaitHandle.WaitOne();
		}

		IAsyncResult IAsyncRelayTransport.BeginSendInMessageList(SerializedRelayMessage[] messages, AsyncCallback callback, object state)
		{
			var result = new SimpleAsyncResult(callback, state);
			ThreadPool.QueueUserWorkItem(o =>
			{
				((IRelayTransport)this).SendInMessageList(messages);
				result.SetComplete();
			});
			return result;
		}

		IAsyncResult IAsyncRelayTransport.BeginSendInMessageList(List<SerializedRelayMessage> messages, AsyncCallback callback, object state)
		{
			var result = new SimpleAsyncResult(callback, state);
			ThreadPool.QueueUserWorkItem(o =>
			{
				((IRelayTransport)this).SendInMessageList(messages);
				result.SetComplete();
			});
			return result;
		}

		void IAsyncRelayTransport.EndSendInMessageList(IAsyncResult result)
		{
			result.AsyncWaitHandle.WaitOne();
		}

		IAsyncResult IAsyncRelayTransport.BeginSendMessageList(List<RelayMessage> messages, AsyncCallback callback, object state)
		{
			var result = new SimpleAsyncResult(callback, state);

			if (messages.Count == 0)
			{
				result.SetComplete();
				return result;
			}

			ThreadPool.QueueUserWorkItem(o =>
			{
				if (messages[0].IsTwoWayMessage)
				{
					((IRelayTransport)this).SendOutMessageList(messages);
				}
				else
				{
					((IRelayTransportExtended)this).SendSyncMessageList(messages);
				}
				result.SetComplete();
			});
			return result;
		}

		void IAsyncRelayTransport.EndSendMessageList(IAsyncResult result)
		{
			result.AsyncWaitHandle.WaitOne();
		}

		#endregion
	}
}
