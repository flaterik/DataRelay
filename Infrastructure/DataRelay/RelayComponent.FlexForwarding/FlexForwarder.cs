using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MySpace.Common;
using MySpace.Common.IO;
using MySpace.DataRelay.Configuration;
using MySpace.Logging;

using MySpace.FlexCache;

namespace MySpace.DataRelay.RelayComponent.FlexForwarding
{
	/// <summary>
	/// Responsible for sending <see cref="RelayMessage"/> to FlexCache servers.
	/// </summary>
	public class FlexForwarder: IRelayComponent, IAsyncDataHandler
	{
		/// <summary>
		/// The official name of this component, for use in configuration files.
		/// </summary>
		public static readonly string ComponentName = "FlexForwarder";
		private static readonly LogWrapper Log = new LogWrapper();

		private FlexCacheClient _flexCacheClient;
		private Counters _counters;

		#region IRelayComponent Members

		/// <summary>
		/// Returns a unique human readable component name.  This name MUST match the name used
		/// in the component config file.
		/// </summary>
		/// <returns>The name of the component.</returns>
		public string GetComponentName()
		{
			return ComponentName;
		}

		/// <summary>
		/// Initializes the component for use using the supplied configuration.
		/// </summary>
		/// <param name="config">The config.</param>
		/// <exception cref="ArgumentNullException">Thrown if the config parameter is null.</exception>
		/// <exception cref="InvalidOperationException">Thrown if the RelayNode passes in a configuration with a missing section for this component.</exception>
		private void LoadConfig(RelayNodeConfig config)
		{
            if (config == null) return;
			if (config.RelayComponents == null) throw new InvalidOperationException("No component configurations were found in the RelayNodeConfig passed to this component.");

			var flexForwarderConfig = config.RelayComponents.GetConfigFor(GetComponentName()) as FlexForwarderConfig;
			var groupName = (flexForwarderConfig != null) ? flexForwarderConfig.GroupName : null;

			var flexCacheClient = new FlexCacheClient(groupName);

			if (_flexCacheClient == null || flexCacheClient.GroupName != _flexCacheClient.GroupName)
			{
				_flexCacheClient = flexCacheClient;
				Log.InfoFormat("FlexCache will use group: \"{0}\".", _flexCacheClient.GroupName);

				if (_counters != null) _counters.RemoveInstance();
				_counters = Counters.GetInstance(_flexCacheClient.GroupName);
			}
		}

		/// <summary>
		/// Reloads the configuration from the given <see cref="FlexForwarder"/> and applies the new settings.
		/// </summary>
		/// <param name="config">The given <see cref="T:MySpace.DataRelay.Configuration.RelayNodeConfig"/>.</param>
		public void ReloadConfig(RelayNodeConfig config)
		{
			LoadConfig(config);
		}

		/// <summary>
		/// Returns a ComponentRunState with any existing error queues, so that they can be persisted through
		/// AppDomain reloads.
		/// </summary>		
		public ComponentRunState GetRunState()
		{
			var runState = new ComponentRunState(GetComponentName());
			return runState;
		}

		/// <summary>
		/// Returns the results of GetHtmlStatus in a ComponentRuntimeInfo object.
		/// </summary>		
		public ComponentRuntimeInfo GetRuntimeInfo()
		{
			return null;
		}

		/// <summary>
		/// Initializes the component. Any error queues in runState will be reinstantiated.
		/// </summary>        
		public void Initialize(RelayNodeConfig config, ComponentRunState runState)
		{
			LoadConfig(config);
		}

		/// <summary>
		/// Shut down the component.
		/// </summary>
		public void Shutdown()
		{
			Log.Info("Shutting down.");
			_flexCacheClient = null;
			if(_counters!=null) _counters.RemoveInstance();
			// TODO: Probably need to wait until all messages have been serviced by _flexCacheClient before completing shutdown.
			Log.Info("Shutting down has completed.");
		}
		#endregion

		/// <summary>
		///	Performs processing on single message
		/// </summary>
		/// <param name="message">Message to be processed</param>
		public virtual void HandleMessage(RelayMessage message)
		{
			var asyncResult = ((IAsyncDataHandler)this).BeginHandleMessage(message, null, null);
			((IAsyncDataHandler)this).EndHandleMessage(asyncResult);
		}

		/// <summary>
		///	Performs processing on a block of messages
		/// </summary>
		/// <param name="messages">A list of RealyMessage objects to be processed</param>
		public virtual void HandleMessages(IList<RelayMessage> messages)
		{
			var asyncResult = ((IAsyncDataHandler)this).BeginHandleMessages(messages, null, null);
			((IAsyncDataHandler)this).EndHandleMessages(asyncResult);
		}

		#region IAsyncDataHandler Members

		/// <summary>
		/// Begins asynchronous processing of a single <see cref="T:MySpace.DataRelay.RelayMessage"/>.
		/// </summary>
		/// <param name="message">The <see cref="T:MySpace.DataRelay.RelayMessage"/>.</param>
		/// <param name="state">Callers can put any state they like here.</param>
		/// <param name="callback">The method to call upon completion.</param>
		/// <returns>
		/// Returns an <see cref="T:System.IAsyncResult"/>.
		/// </returns>
		public virtual IAsyncResult BeginHandleMessage(RelayMessage message, object state, AsyncCallback callback)
		{
			var messages = new List<RelayMessage>(1) {message};
			return ((IAsyncDataHandler)this).BeginHandleMessages(messages, state, callback);
		}

		/// <summary>
		/// Begins asynchronous processing of a <see cref="T:System.Collections.Generic.List`1"/> of <see cref="T:MySpace.DataRelay.RelayMessage"/>s.
		/// </summary>
		/// <param name="messages">The list of <see cref="T:MySpace.DataRelay.RelayMessage"/>s.</param>
		/// <param name="state">Callers can put any state they like here.</param>
		/// <param name="callback">The method to call upon completion.</param>
		/// <returns>
		/// Returns an <see cref="T:System.IAsyncResult"/>.
		/// </returns>
		/// <remarks>
		///	<para>This method simply redirects to the synchronous <see cref="HandleMessages"/> method for now.
		///	In the future this may change if we have compelling use-cases for making bulk message handling asynchronous.</para>
		/// </remarks>
		IAsyncResult IAsyncDataHandler.BeginHandleMessages(IList<RelayMessage> messages, object state, AsyncCallback callback)
		{
			var asyncMessages = new Dictionary<RelayMessage, Future>(messages.Count);
			foreach (var message in messages)
			{
				asyncMessages[message] = _flexCacheClient.GetFuture(message);
			}
			if (asyncMessages.Count != messages.Count) throw new ArgumentException("Detected a message used twice in the same list.", "messages");
			return new AsynchronousResult(callback, state, asyncMessages, _counters);
		}

		/// <summary>
		/// Ends asynchronous processing of a single <see cref="T:MySpace.DataRelay.RelayMessage"/>.
		/// </summary>
		/// <param name="asyncResult">The <see cref="T:System.IAsyncResult"/> from <see cref="M:MySpace.DataRelay.IAsyncDataHandler.BeginHandleMessage(MySpace.DataRelay.RelayMessage,System.Object,System.AsyncCallback)"/></param>
		public virtual void EndHandleMessage(IAsyncResult asyncResult)
		{
			((IAsyncDataHandler)this).EndHandleMessages(asyncResult); 
		}

		/// <summary>
		/// Ends asynchronous processing of a <see cref="T:System.Collections.Generic.List`1"/> of <see cref="T:MySpace.DataRelay.RelayMessage"/>s.
		/// </summary>
		/// <param name="asyncResult">The <see cref="T:System.IAsyncResult"/> from <see cref="M:MySpace.DataRelay.IAsyncDataHandler.BeginHandleMessages(System.Collections.Generic.IList{MySpace.DataRelay.RelayMessage},System.Object,System.AsyncCallback)"/></param>
		void IAsyncDataHandler.EndHandleMessages(IAsyncResult asyncResult)
		{
			asyncResult.AsyncWaitHandle.WaitOne();
			((AsynchronousResult)asyncResult).Complete();
		}

		#endregion

		private class AsynchronousResult : IAsyncResult
		{
			private readonly AsyncCallback _completionCallback;
			private readonly ManualResetEvent _waitHandle = new ManualResetEvent(false);
			private readonly object _state;
			private readonly bool _completedSynchrononously = true;
			private readonly IDictionary<RelayMessage, Future> _asyncMessages;
			private int _completeCount;
			private readonly Counters _counters;

			public AsynchronousResult(AsyncCallback completionCallback, object state, IDictionary<RelayMessage, Future> asyncMessages, Counters counters) 
			{
				if (asyncMessages.Count == 0) throw new ArgumentException("Cannot start an aynchronous operation with no messages.", "asyncMessages");
				_completionCallback = completionCallback;
				_state = state;
				_asyncMessages = asyncMessages;
				_counters = counters;

				if (asyncMessages.Any(message => !message.Value.IsComplete))
				{
					_completedSynchrononously = false;
				}

				foreach (var message in asyncMessages)
				{
					try
					{
						message.Value
							.OnSuccess(FutureCallback)
							.OnError(ErrorCallback);
					}
					catch (Exception ex)
					{
						FrequencyBoundLogError(String.Format("Unexpected Exception occurred while connection reactions to Flex Futures.  Exception: {0}", ex));
						ErrorCallback(ex);
					}
				}
			}

			private static Dictionary<string, ParameterlessDelegate> _errorLogDelegates = new Dictionary<string, ParameterlessDelegate>();
			private static readonly object ErrorLogSync = new object();
			private static void FrequencyBoundLogError(string message)
			{
				ParameterlessDelegate logDelegate;
				if (!_errorLogDelegates.TryGetValue(message, out logDelegate))
				{
					lock (ErrorLogSync)
					{
						if (!_errorLogDelegates.TryGetValue(message, out logDelegate))
						{
							var errorDelegates = new Dictionary<string, ParameterlessDelegate>(_errorLogDelegates);
							logDelegate = Algorithm.FrequencyBoundMethod(count =>
																	{
																		if (count > 1) Log.ErrorFormat("{0} occurances: {1}", count + 1, message);
																		else Log.Error(message);
																	}
																	, TimeSpan.FromMinutes(1)
																	);

							errorDelegates[message] = logDelegate;
							Interlocked.Exchange(ref _errorLogDelegates, errorDelegates);
						}
					}
				}
				logDelegate();
			}

			private void FutureCallback()
			{
				if (Interlocked.Increment(ref _completeCount) == _asyncMessages.Count)
				{
					try
					{
						foreach (var message in _asyncMessages)
						{
							if (message.Value.HasError)
							{
                                // Errors are logged already in ErrorCallback().
                                message.Key.ResultOutcome = RelayOutcome.Error;
								string errorMessage = message.Value.Error.ToString();
								message.Key.ResultDetails = errorMessage;
								_counters.TotalErrorsPerSecond.Increment();
							}
							else
							{
								message.Key.ResultOutcome = RelayOutcome.Success;
								switch (message.Key.GetMessageActionType())
								{
									case MessageActionType.Get:
										if (((CacheResultFuture<Stream>)message.Value).WasFound)
										{
											var futureStream = (CacheResultFuture<Stream>)message.Value;

											message.Key.Payload = new RelayPayload(message.Key.TypeId, message.Key.Id) { ByteArray = futureStream.Value.ReadToEnd(), ExtendedId = message.Key.ExtendedId };

											_counters.TotalHitRatio.RecordAttempt(true);
											_counters.AverageMessageSize.RecordCount(message.Key.Payload.ByteArray.Length);
										}
										else
										{
											message.Key.Payload = null;
											_counters.TotalHitRatio.RecordAttempt(false);
										}
										_counters.TotalGetMessagesPerSecond.Increment();
										break;
									case MessageActionType.Put:
										_counters.AverageMessageSize.RecordCount(message.Key.Payload.ByteArray.Length);
										_counters.TotalPutMessagesPerSecond.Increment();
										break;
									case MessageActionType.Delete:
										_counters.TotalDeleteMessagesPerSecond.Increment();
										break;
								}
								_counters.TotalMessagesPerSecond.Increment();
							}
						}
					}
					finally
					{
						_waitHandle.Set();
						if (_completionCallback != null) _completionCallback(this);
					}
				}
			}

			public void Complete()
			{
				_waitHandle.Close();
			}

			private void ErrorCallback(Exception e)
			{
                try
                {
                    FrequencyBoundLogError(e.ToString());
                }
                finally
                {
                    FutureCallback();
                }
			}

			#region IAsyncResult Members

			object IAsyncResult.AsyncState
			{
				get { return _state; }
			}

			WaitHandle IAsyncResult.AsyncWaitHandle
			{
				get { return _waitHandle; }
			}

			bool IAsyncResult.CompletedSynchronously
			{
				get { return _completedSynchrononously; }
			}

			bool IAsyncResult.IsCompleted
			{
				get { return _completeCount == _asyncMessages.Count; }
			}

			#endregion
		}
	}
}
