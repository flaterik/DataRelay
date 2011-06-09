using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Linq;
using Microsoft.Ccr.Core;
using MySpace.Common.IO;
using MySpace.Configuration;
using MySpace.DataRelay.Configuration;
using MySpace.DataRelay.Http;
using MySpace.DataRelay.Server.Common;
using MySpace.Logging;
using System.Text;
using MySpace.DataRelay.Common.Schemas;
using MySpace.PipelineTransport;

namespace MySpace.DataRelay
{
	/// <summary>
	/// Represents a server that hosts <see cref="IRelayComponent"/>s, transport and
	/// handles <see cref="RelayMessage"/> requests.
	/// </summary>
	/// <remarks>
	///     <para>The <see cref="RelayNode"/> is the central server component in the 
	///     relay transport.</para>
	/// </remarks>
	public class RelayNode : MarshalByRefObject, IRelayNode, IAsyncDataHandler, IDataHandler, IRelayNodeServices
	{
		#region Fields
		
		internal readonly static LogWrapper log = new LogWrapper();
		RelayNodeConfig _configuration;
		TypeSettingCollection _typeSettings;
		
		int _queuedTaskThreshold = Int32.MaxValue;
		RelayNodeCounters _counters;
		RelayNodeCounters _countersInternal;
		Dispatcher _inDispatcher;
		Dispatcher _outDispatcher;
		DispatcherQueue _inMessageQueue;
		DispatcherQueue _outMessageQueue;
		Port<RelayMessage> _inMessagePort = new Port<RelayMessage>();
		Port<RelayMessageWithContext> _inMessageWithContextPort = new Port<RelayMessageWithContext>();
		Port<IList<RelayMessage>> _inMessagesPort = new Port<IList<RelayMessage>>();

		Port<RelayMessageAsyncResult> _outMessagePort;
		Port<RelayMessageListAsyncResult> _outMessagesPort;

		MessageTracer _messageTracer;

		RelayComponents _components;
		Timer _queuedMessageCounterTimer;
		ushort _myZone;
		RelayNodeGroupDefinition _myGroup;		
		int _myClusterId;
		bool[] _typeIdBelongsHere;

		ICollection<IPAddress> _clusterAddresses;
		Timer _resetConnectionRefusalTimer;
		bool _redirectionConfigured;
		HttpServer _httpServer;

		const string forwardingComponentName = "Forwarding";
		IDataHandler _forwardingComponent;

        private PipelineListener _pipelineListener;

		#endregion

		#region Events

		/// <summary>
		/// Fires before a message or batch of messages are handled.
		/// </summary>
		public event EventHandler BeforeMessagesHandled;

		/// <summary>
		/// Fires after a message or batch of messages are handled.
		/// </summary>
		public event EventHandler AfterMessagesHandled;

		#endregion

		#region IRelayNode Members

		/// <summary>
		/// Initializes the <see cref="RelayNode"/>, must be called before calling <see cref="Start"/>
		/// </summary>
		public void Initialize()
		{
			Initialize(null);
		}

		/// <summary>
		/// Initializes the <see cref="RelayNode"/> with the given <see cref="ComponentRunState"/>s,
		/// must be called before calling <see cref="Start"/>
		/// </summary>
		/// <param name="componentRunStates"></param>
		public void Initialize(ComponentRunState[] componentRunStates)
		{
			AppDomain.CurrentDomain.UnhandledException += LogUnhandledException;

			try
			{
				if (log.IsInfoEnabled)
				{
					if (componentRunStates == null)
					{
						log.Info("Initializing Relay Node.");
					}
					else
					{
						log.Info("Initialzing Relay Node with Component Run States.");
					}
				}

				EnvironmentManager.EnvironmentChanged += EnvironmentChangedHandler;

				GetConfig();

				if (_configuration == null) throw new ConfigurationErrorsException("config failed to load, is null");

				SetClusterAddresses(_configuration);

				fatalFailureTimeout = _configuration.FatalShutdownTimeout < 0
				                      	? TimeSpan.FromMinutes(5)
				                      	: TimeSpan.FromSeconds(_configuration.FatalShutdownTimeout);

				_components = new RelayComponents(_configuration);


				_messageTracer = new MessageTracer(_configuration.TypeSettings.MaxTypeId, _configuration.TraceSettings);
				_messageTracer.Activated = _configuration.OutputTraceInfo;

				const string inThreadsName = "DataRelayNode";
				if (_configuration.NumberOfThreads > 0)
				{
					_inDispatcher = new Dispatcher(_configuration.NumberOfThreads, ThreadPriority.Normal, true, inThreadsName);
				}
				else
				{
					_inDispatcher = new Dispatcher {Name = inThreadsName};
				}

				const string outThreadsName = "DataRelayNodeOUT";
				if (_configuration.OutMessagesOnRelayThreads)
				{
					if (_configuration.NumberOfOutMessageThreads > 0)
					{
						_outDispatcher = new Dispatcher(_configuration.NumberOfOutMessageThreads, ThreadPriority.Normal, true,
						                                outThreadsName);
					}
					else
					{
						_outDispatcher = new Dispatcher {Name = outThreadsName};
					}

					_outMessagePort = new Port<RelayMessageAsyncResult>();
					_outMessagesPort = new Port<RelayMessageListAsyncResult>();

					_outMessageQueue = new DispatcherQueue("DataRelayDispatcherQueueOUT", _outDispatcher);
					Arbiter.Activate(_outMessageQueue,
					                 Arbiter.ReceiveWithIterator(true, _outMessagePort, HandleOutMessage));
					Arbiter.Activate(_outMessageQueue,
					                 Arbiter.ReceiveWithIterator(true, _outMessagesPort, HandleOutMessages));
				}

				_inMessageQueue = new DispatcherQueue("DataRelayDispatcherQueue", _inDispatcher);

				_queuedTaskThreshold = (int) Math.Floor(0.9*_configuration.MaximumMessageQueueDepth);


				// setup RelayServicesClient before initalizing components
				RelayServicesClient.Instance.RelayNodeServices = this;

				Arbiter.Activate(_inMessageQueue,
				                 Arbiter.Receive<RelayMessage>(true, _inMessagePort, HandleInMessage));
				Arbiter.Activate(_inMessageQueue,
				                 Arbiter.Receive<RelayMessageWithContext>(true, _inMessageWithContextPort, HandleInMessage));
				Arbiter.Activate(_inMessageQueue,
				                 Arbiter.Receive<IList<RelayMessage>>(true, _inMessagesPort, HandleInMessages));


				//by having after the Arbiter.Activate it allows Initialize components to use 
				//IRelayNodeServices that require Message handling
				_components.Initialize(componentRunStates, _configuration.IgnoredMessageTypes);

				_queuedMessageCounterTimer = new Timer(CountQueuedMessages, null, 5000, 5000);


				_forwardingComponent = _components.GetComponent(forwardingComponentName);
				if (_forwardingComponent == null)
				{
					log.Warn("Unable to locate forwarding component");
				}

				log.InfoFormat("Redirection enabled: {0}", RedirectionEnabled());
			}
			catch (Exception ex)
			{
				if (log.IsErrorEnabled)
					log.ErrorFormat("Exception initializing relay node: {0}", ex);
				throw; //should bring server down
			}
		}

		private static void LogUnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			StringBuilder bld = new StringBuilder();
			Exception exc = e.ExceptionObject as Exception;

			bld.AppendLine("Unhandled Exception, ");
			if (e.IsTerminating)
			{
				bld.AppendLine("TERMINATING DOMAIN: ");
			}
			else
			{
				bld.AppendLine("non-terminating: ");
			}

			if (sender != null)
			{
				bld.AppendFormat("Sender {0} ", sender);
			}

			if (exc != null)
			{
				log.Error(bld.ToString(), exc);
			}
			else
			{
				bld.AppendFormat("Exception Object {0}", e.ExceptionObject);
				log.Error(bld.ToString());
			}
		}

		public ComponentRunState[] GetComponentRunStates()
		{
			return _components.GetComponentRunStates();
		}
		
		public ComponentRuntimeInfo[] GetComponentsRuntimeInfo()
		{
			return _components.GetComponentsRuntimeInfo();
		}

		public ComponentRuntimeInfo GetComponentRuntimeInfo(string componentName)
		{
			return _components.GetComponentRuntimeInfo(componentName);
		}

		public string GetComponentsDescription()
		{
			return _components.GetComponentsDescription();
		}

		private int GetResetDuration()
		{
			var resetDuration = 0;
			// node
			var node = _configuration.GetMyNode();
			if (node == null) return resetDuration;
			resetDuration = node.StartupRepopulateDuration;
			if (resetDuration != 0) return resetDuration;
			// cluster
			var cluster = _configuration.GetMyCluster();
			if (cluster == null) return resetDuration;
			resetDuration = cluster.StartupRepopulateDuration;
			if (resetDuration != 0) return resetDuration;
			// group
			var group = _configuration.GetMyGroup();
			if (group == null) return resetDuration;
			resetDuration = group.StartupRepopulateDuration;
			return resetDuration;
		}

		/// <summary>
		/// Starts the <see cref="RelayNode"/> server to listen for incoming TCP/IP 
		/// requests on the configured port.
		/// </summary>
		public void Start()
		{
			_counters = new RelayNodeCounters();
			_countersInternal = new RelayNodeCounters();
			_countersInternal.Initialize("Internal");
			_counters.Initialize(instanceName);
			if (portNumber != 0)
			{
				var resetDuration = GetResetDuration();
				bool whitelistOnly = resetDuration > 0;
				if (whitelistOnly && _clusterAddresses == null)
				{
					throw new ApplicationException(
						"Cannot configure refuse out of cluster connections when node not in cluster.");
				}
				
				SocketServerAdapter.Initialize(instanceName, portNumber, this,
					_configuration.OutMessagesOnRelayThreads, IsInCluster,
					whitelistOnly);

				if (whitelistOnly)
				{
					_resetConnectionRefusalTimer = new Timer(state =>
					{
						var timer = Interlocked.Exchange(ref _resetConnectionRefusalTimer, null);
						if (timer != null)
						{
							RefuseOutOfClusterConnection = false;
							timer.Dispose();
						}
					}, null, resetDuration*1000, Timeout.Infinite);
				}
				else
				{
					_resetConnectionRefusalTimer = null;
				}
			}

		    StartPipelineListener();

			StartHttpServer();
			
		}

        private void StartPipelineListener()
        {
            if (pipelinePort != 0)
            {
                try
                {
                    PipelineListener.EnablePerformanceCounters();
                    _pipelineListener = new PipelineListener(pipelinePort);
                    //_pipelineListener.Start
                    //    (
                    //        request => ThreadPool.UnsafeQueueUserWorkItem(OnIncomingPipelineRequest, request)
                    //    );
                    _pipelineListener.Start(OnIncomingPipelineRequest);
                    log.InfoFormat("Pipeline Transport is now listening on port {0}.", pipelinePort);
                }
                catch (Exception ex)
                {
                    log.ErrorFormat("Cannot start the PipelineTransport listener. {0}", ex);
                }
            }
            else
            {
                log.InfoFormat("PipelineTransport is not configured.  Set member <PipelinePort> in RelayTransportSettings.config to enable the PipelineTransport listener.");
            }            
        }

		private void StartHttpServer()
		{
			if (httpPortNumber != 0)
			{
				try
				{
					_httpServer = new HttpServer(httpPortNumber, this);
					_httpServer.Start();
				}
				catch (Exception e)
				{
					log.ErrorFormat("Error initializing http server: {0}", e);
				}
			}
		}

		/// <summary>
		/// Stops the <see cref="RelayNode"/> server from accepting TCP/IP requests.
		/// </summary>
		public void Stop()
		{
			if (_queuedMessageCounterTimer != null)
			{
				_queuedMessageCounterTimer.Change(Timeout.Infinite, Timeout.Infinite);
				_queuedMessageCounterTimer.Dispose();
			}

			var timer = Interlocked.Exchange(ref _resetConnectionRefusalTimer, null);
			if (timer != null)
			{
				timer.Change(Timeout.Infinite, Timeout.Infinite);
				timer.Dispose();
			}

			if (RefuseOutOfClusterConnection)
			{
				RefuseOutOfClusterConnection = false;
			}

			if(log.IsInfoEnabled)
					log.Info("Shutting down socket transport.");
			SocketServerAdapter.Shutdown();
			
			if (log.IsInfoEnabled)
				log.Info("Disposing Dispatcher.");
			_inDispatcher.Dispose();
			var od = _outDispatcher; //in case of config reload to null
			if (od != null) _outDispatcher.Dispose();

			StopHttpServer();

			_components.Shutdown();

			if (log.IsInfoEnabled)
				log.Info("Resetting Counters");

			if (_counters != null)
			{
				_counters.ResetCounters();
				_counters.Shutdown();
				_counters = null;
			}
			
			if (log.IsInfoEnabled)
				log.Info("Relay Node Stopped.");

			AppDomain.CurrentDomain.UnhandledException -= LogUnhandledException;
		}

		private void StopHttpServer()
		{
			if (_httpServer != null)
			{
				try
				{
					_httpServer.Stop();
					_httpServer = null;
				}
				catch (Exception e)
				{
					log.ErrorFormat("Error stopping http server: {0}", e);
				}
			}
		}
		#endregion

		#region Config

		string instanceName;

		private int portNumber;
        private ushort pipelinePort;
		private int httpPortNumber = 80;

		/// <summary>
		/// Gets the IP port this instance is listening on.
		/// </summary>
		public int Port
		{
			get { return portNumber; }
		}

        /// <summary>
        /// The port number of the PipelineTransport.
        /// </summary>
        /// <value>The port number of the <see cref="PipelineTransport"/> or 0 if PipelineTransport is not listening</value>
        public int PipelinePort
        {
            get { return pipelinePort; }
        }

		private void GetConfig()
		{
			EventHandler reloadEventHandler = ReloadConfig;
			_configuration = RelayNodeConfig.GetRelayNodeConfig(reloadEventHandler);
			
			if (_configuration != null)
			{

				_myZone = _configuration.GetLocalZone();
				_myGroup = _configuration.GetMyGroup();
				_myClusterId = _configuration.GetMyClusterIndex();
				_typeIdBelongsHere = GenerateTypeIdBelongsHere(_myGroup, _configuration.TypeSettings);
				_redirectionConfigured = _configuration.RedirectMessages;

				instanceName = _configuration.InstanceName;
				if (_configuration.TransportSettings != null)
				{
					portNumber = _configuration.TransportSettings.ListenPort;
				    pipelinePort = _configuration.TransportSettings.PipelinePort;
					httpPortNumber = _configuration.TransportSettings.HttpListenPort;
				}

				_typeSettings = _configuration.TypeSettings != null ? 
					_configuration.TypeSettings.TypeSettingCollection : null;

			}
			else
			{
				if (log.IsErrorEnabled)
					log.Error("NO CONFIG SECTION FOUND, SERVICE NOT STARTING.");
			}
		}

		private static bool[] GenerateTypeIdBelongsHere(RelayNodeGroupDefinition myGroup, TypeSettings typeSettings)
		{
			bool[] typeIdBelongsHere = new bool[typeSettings.MaxTypeId + 1];
			typeIdBelongsHere[0] = true; //typeid 0 = goes everywhere
			if(typeSettings.TypeSettingCollection == null || myGroup == null)
			{
				for (short typeId = 1; typeId <= typeSettings.MaxTypeId; typeId++)
				{
					typeIdBelongsHere[typeId] = false;
				}
			}
			else
			{
				for (short typeId = 1; typeId <= typeSettings.MaxTypeId; typeId++)
				{
					TypeSetting setting = typeSettings.TypeSettingCollection[typeId];
					typeIdBelongsHere[typeId] = (setting != null && setting.GroupName == myGroup.Name);
				}				
			}

			return typeIdBelongsHere;
		}

		private void EnvironmentChangedHandler(string oldEnvironment, string newEnvironment)
		{
			ReloadConfig(RelayNodeConfig.GetRelayNodeConfig());
		}

		internal void ReloadConfig(RelayNodeConfig newConfiguration)
		{
			if (newConfiguration != null)
			{
				if (log.IsInfoEnabled)
					log.Info("Reloading configs.");

				fatalFailureTimeout = newConfiguration.FatalShutdownTimeout < 0
					? TimeSpan.FromMinutes(5)
					: TimeSpan.FromSeconds(newConfiguration.FatalShutdownTimeout);

				if (newConfiguration.GetMyNode() != null)
				{
					_myZone = newConfiguration.GetLocalZone();
				}

				_myGroup = newConfiguration.GetMyGroup();
				_myClusterId = newConfiguration.GetMyClusterIndex();
				_typeIdBelongsHere = GenerateTypeIdBelongsHere(_myGroup, newConfiguration.TypeSettings);
				_typeSettings = newConfiguration.TypeSettings != null ?
					newConfiguration.TypeSettings.TypeSettingCollection : null;

				_redirectionConfigured = newConfiguration.RedirectMessages;
				log.InfoFormat("Redirection enabled: {0}", RedirectionEnabled());

				SetClusterAddresses(newConfiguration);

				_messageTracer.ReloadConfig(newConfiguration.TypeSettings != null ? newConfiguration.TypeSettings.MaxTypeId : (short)1, newConfiguration.TraceSettings);
				_messageTracer.Activated = newConfiguration.OutputTraceInfo;

				//TODO: handle changes in component definition
				_components.ReloadConfig(newConfiguration, newConfiguration.IgnoredMessageTypes);

				if (newConfiguration.TransportSettings != null)  
				{
					if(newConfiguration.TransportSettings.ListenPort != portNumber)
					{
						log.InfoFormat("Changing Socket Transport Port to {0}",
										   newConfiguration.TransportSettings.ListenPort);
						portNumber = newConfiguration.TransportSettings.ListenPort;
						SocketServerAdapter.ChangePort(portNumber);
					}
					if(newConfiguration.TransportSettings.HttpListenPort != httpPortNumber)
					{
						if (httpPortNumber < 1 && newConfiguration.TransportSettings.HttpListenPort > 0) //there was no http server and now we want one
						{
							httpPortNumber = newConfiguration.TransportSettings.HttpListenPort;
							StartHttpServer();

						}
						else if (newConfiguration.TransportSettings.HttpListenPort < 1 && httpPortNumber > 0) //shut off a running server
						{
							httpPortNumber = newConfiguration.TransportSettings.HttpListenPort;
							StopHttpServer();
						}
						else //just change the port on an existing server
						{
							log.InfoFormat("Changing Http Transport Port to {0}",
											   newConfiguration.TransportSettings.HttpListenPort);
							httpPortNumber = newConfiguration.TransportSettings.HttpListenPort;
							_httpServer.ChangePort(httpPortNumber);	
						}
						
					}
				}

				if (newConfiguration.NumberOfThreads != _configuration.NumberOfThreads)
				{
					if(log.IsInfoEnabled)
						log.InfoFormat("Changing number of relay node threads from {0} to {1}", 
							_configuration.NumberOfThreads, newConfiguration.NumberOfThreads);
					try
					{
						Dispatcher oldInDispatcher = _inDispatcher;
						
						const string inThreadsName = "DataRelayNode";
						Dispatcher newInDispatcher = 
							newConfiguration.NumberOfThreads > 0 ? 
							new Dispatcher(newConfiguration.NumberOfThreads, ThreadPriority.Normal, true, inThreadsName) : 
							new Dispatcher() { Name = inThreadsName };

						DispatcherQueue newInQueue = new DispatcherQueue("DataRelayDispatcherQueue", newInDispatcher, TaskExecutionPolicy.Unconstrained, 0);

						_inMessagePort = new Port<RelayMessage>();
						_inMessageWithContextPort = new Port<RelayMessageWithContext>();
						_inMessagesPort = new Port<IList<RelayMessage>>();

						Arbiter.Activate(newInQueue,
							 Arbiter.Receive<RelayMessage>(true, _inMessagePort, HandleInMessage));
						Arbiter.Activate(newInQueue,
							 Arbiter.Receive<RelayMessageWithContext>(true, _inMessageWithContextPort, HandleInMessage));
						Arbiter.Activate(newInQueue,
							 Arbiter.Receive<IList<RelayMessage>>(true, _inMessagesPort, HandleInMessages));

						_inMessageQueue = newInQueue;
						_inDispatcher = newInDispatcher;
						oldInDispatcher.Dispose();
					}
					catch (Exception e)
					{
						if (log.IsErrorEnabled)
							log.ErrorFormat("Error changing number of relay node threads: {0}", e);
					}
				}
				else
				{
					//not rebuilding the queue, but reset its max queue depth anyway
					_inMessageQueue.MaximumQueueDepth = newConfiguration.MaximumMessageQueueDepth;
				}

				SetupOutMessagesOnRelayThreads(newConfiguration);

				_queuedTaskThreshold = (int)Math.Floor(0.9 * newConfiguration.MaximumMessageQueueDepth);
				_configuration = newConfiguration;
				if (log.IsInfoEnabled)
					log.Info("Done Reloading configs.");
			}
			else
			{
				if (log.IsErrorEnabled)
					log.Error("Attempt to reload null config");
			}
		}



		private void ReloadConfig(object state, EventArgs args)
		{
			RelayNodeConfig newConfiguration = state as RelayNodeConfig;
			ReloadConfig(newConfiguration);
		}

		private void SetupOutMessagesOnRelayThreads(RelayNodeConfig newConfiguration) 
		{
			//if it was off and is now on, or if it was on and the number of threads changed
			bool setupNewOutMessages = (newConfiguration.OutMessagesOnRelayThreads && _configuration.OutMessagesOnRelayThreads == false)
									   || (_configuration.OutMessagesOnRelayThreads && newConfiguration.OutMessagesOnRelayThreads
										   && newConfiguration.NumberOfOutMessageThreads != _configuration.NumberOfOutMessageThreads);

			Dispatcher oldOutDispatcher = _outDispatcher;
			DispatcherQueue oldOutMessageQueue = _outMessageQueue;

			if (setupNewOutMessages)
			{
				try
				{
					const string outThreadsName = "DataRelayNodeOUT";
						
					_outMessagePort = new Port<RelayMessageAsyncResult>(); //atomic
					_outMessagesPort = new Port<RelayMessageListAsyncResult>(); //atomic

					if (newConfiguration.NumberOfOutMessageThreads > 0)
					{
						_outDispatcher = new Dispatcher(newConfiguration.NumberOfOutMessageThreads, ThreadPriority.Normal, true, outThreadsName);
					}
					else
					{
						_outDispatcher = new Dispatcher { Name = outThreadsName };
					}

					_outMessageQueue = new DispatcherQueue("DataRelayDispatcherQueueOUT", _outDispatcher);

					Arbiter.Activate(_outMessageQueue,
									 Arbiter.ReceiveWithIterator(true, _outMessagePort, HandleOutMessage));
					Arbiter.Activate(_outMessageQueue,
									 Arbiter.ReceiveWithIterator(true, _outMessagesPort, HandleOutMessages));
						
				}
				catch (Exception e)
				{	
					if(log.IsErrorEnabled)
						log.ErrorFormat("Error setting up Out Message Threads on RelayNode: {0}", e);                    
					throw;
				}
			}

			if (newConfiguration.OutMessagesOnRelayThreads == false)
			{
				_outMessagePort = null;
				_outMessagesPort = null;
				if (oldOutDispatcher != null) oldOutDispatcher.Dispose();
				if (oldOutMessageQueue != null) oldOutMessageQueue.Dispose();
			}
		}

		#endregion

		#region Private Members

		private void FireBeforeHandle()
		{
			var beforeHandle = BeforeMessagesHandled;
			if (beforeHandle != null)
			{
				beforeHandle(this, EventArgs.Empty);
			}
		}

		private void FireAfterHandle()
		{
			var afterHandle = AfterMessagesHandled;
			if (afterHandle != null)
			{
				afterHandle(this, EventArgs.Empty);
			}
		}

		private IEnumerator<ITask> HandleOutMessage(RelayMessageAsyncResult asyncMessage)
		{
			try
			{
				FireBeforeHandle();
				_counters.CountInputBytes(asyncMessage.Message);
				foreach (var task in _components.HandleOutMessage(asyncMessage))
				{
					yield return task;
				}
			}
			finally
			{
				_counters.CountOutMessage(asyncMessage.Message);
				const bool wasSynchronous = false;
				asyncMessage.CompleteOperation(wasSynchronous);
				FireAfterHandle();
			}
		}

		private void HandleOutMessage(RelayMessage message)
		{
			try
			{
				FireBeforeHandle();
				_counters.CountInputBytes(message);
				_components.HandleOutMessage(message);
				_counters.CountOutMessage(message);
				if (message.AllowsReturnPayload == false) message.Payload = null;
			}
			catch (Exception exc)
			{
				message.Payload = null;
				if (log.IsErrorEnabled)
					log.ErrorFormat("Error handling message {0}: {1}", message, exc);
			}
			finally
			{
				FireAfterHandle();
			}
		}

		private void HandleInMessage(RelayMessage message)
		{
			try
			{
				FireBeforeHandle();
				_counters.CountInputBytes(message);
				_components.HandleInMessage(message);
				_counters.CountInMessage(message);
			}
			catch (Exception exc)
			{
				if (log.IsErrorEnabled)
					log.ErrorFormat("Error handling message {0}: {1}", message, exc);
			}
			finally
			{
				FireAfterHandle();
			}
		}
		private void HandleInMessage(RelayMessageWithContext messageWithContext)
		{
			try
			{
				FireBeforeHandle();
				_counters.CountInputBytes(messageWithContext.RelayMessage);
				_components.HandleInMessage(messageWithContext);
				_countersInternal.CountInMessage(messageWithContext.RelayMessage);
			}
			catch (Exception exc)
			{
				if (log.IsErrorEnabled)
					log.ErrorFormat("Error handling message {0}: {1}", messageWithContext.RelayMessage, exc);				
			}
			finally
			{
				FireAfterHandle();
			}
		}

		private void HandleOutMessage(RelayMessageWithContext messageWithContext)
		{
			try
			{
				FireBeforeHandle();
				_counters.CountInputBytes(messageWithContext.RelayMessage);
				_components.HandleOutMessage(messageWithContext);
				_countersInternal.CountOutMessage(messageWithContext.RelayMessage);
				if (messageWithContext.RelayMessage.AllowsReturnPayload == false) messageWithContext.RelayMessage.Payload = null;
			}
			catch (Exception exc)
			{
				if (log.IsErrorEnabled)
					log.ErrorFormat("Error handling message {0}: {1}", messageWithContext.RelayMessage, exc);				
			}
			finally
			{
				FireAfterHandle();
			}
		}

		private void HandleInMessages(IList<RelayMessage> messages)
		{
			try
			{
				FireBeforeHandle();
				_counters.CountInputBytes(messages);
				_components.HandleInMessages(messages);
				_counters.CountInMessages(messages);
			}
			catch (Exception ex)
			{
				if (log.IsErrorEnabled)
					log.ErrorFormat("Error handling in message list: {0}", ex);				                
			}
			finally
			{
				FireAfterHandle();
			}
		}

		private IEnumerator<ITask> HandleOutMessages(RelayMessageListAsyncResult asyncMessages)
		{
			try
			{
				FireBeforeHandle();
				_counters.CountInputBytes(asyncMessages.Messages);
				foreach (var task in _components.HandleOutMessages(asyncMessages))
				{
					yield return task;
				}
			}
			finally
			{
				_counters.CountOutMessages(asyncMessages.Messages);
				const bool wasSynchronous = false;
				asyncMessages.CompleteOperation(wasSynchronous);
				FireAfterHandle();
			}
		}

		private void HandleOutMessages(IList<RelayMessage> messages)
		{
			try
			{
				FireBeforeHandle();
				_counters.CountInputBytes(messages);
				_components.HandleOutMessages(messages);
				_counters.CountOutMessages(messages);
				for (int i = 0; i < messages.Count; i++)
				{
					if (messages[i].AllowsReturnPayload == false)
					{
						messages[i].Payload = null;
					}
				}
			}
			catch (Exception ex)
			{
				if (log.IsErrorEnabled)
					log.ErrorFormat("Error handling out message list: {0}", ex);				                
			}
			finally
			{
				FireAfterHandle();
			}
		}

		private void CountQueuedMessages(object state)
		{
			if (_counters != null)
			{
				int count = _inDispatcher.PendingTaskCount;
				var od = _outDispatcher; //in case of config reload to null
				if (od != null) count += od.PendingTaskCount;
				_counters.SetNumberOfQueuedMessages(count);
			}
		}

        private void OnIncomingPipelineRequest(object state)
        {
            var request = state as PipelineStream;
            if (request != null)
            {
                try
                {
                    var relayMessage = new RelayMessage();
                    if (Serializer.Deserialize(request, relayMessage))
                    {
                        if (relayMessage.MessageType == MessageType.Get)
                        {
                            // relayMessage.Payload = new RelayPayload(relayMessage.TypeId, relayMessage.Id, relayMessage.ExtendedId, null, new byte[300], false);
                            // relayMessage.ResultOutcome = RelayOutcome.Success;
                            //_counters.CountOutMessage(relayMessage);
                            HandleOutMessage(relayMessage);
                            if (relayMessage.Payload == null) request.SendResponse(Stream.Null);
                            else
                            {
                                using (var stream = new MemoryStream())
                                {
                                    Serializer.Serialize(stream, relayMessage, SerializerFlags.Default);
                                    stream.Position = 0;
                                    request.SendResponse(stream);
                                }
                            }
                        }
                        else if (relayMessage.MessageType == MessageType.Save)
                        {
                            HandleInMessage(relayMessage);
                            request.SendResponse(Stream.Null);
                        }
                        else if (relayMessage.MessageType == MessageType.Delete)
                        {
                            HandleInMessage(relayMessage);
                            request.SendResponse(Stream.Null);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.ErrorFormat("While handling an incoming request off of the pipeline transport: {0}", ex);
                    request.SendResponse(Stream.Null);
                }
                finally
                {
                    request.Close();
                }
            }
        }

		#endregion

		#region MarshalByRefObject.InitializeLifetimeService

		public override object InitializeLifetimeService()
		{
			return null;
		}

		#endregion

		#region HandleMessage

		/// <summary>
		/// Processes a given <see cref="RelayMessage"/>.
		/// </summary>
		/// <remarks>
		///     <para>This method is the primary entry point for handling a <see cref="RelayMessage"/>. </para>
		/// </remarks>
		/// <param name="message">The given <see cref="RelayMessage"/>.</param>
		public void HandleMessage(RelayMessage message)
		{
			if (message != null)
			{
				if (RedirectMessage(message)) return;

				_messageTracer.WriteMessageInfo(message);

				if (_components.DoHandleMessagesOfType(message.MessageType))
				{
					#region Assign SourceZone
					if (message.SourceZone == 0)
					{
						message.SourceZone = _myZone;
					}
					#endregion

					if (message.IsTwoWayMessage)
					{
						HandleOutMessage(message);
					}
					else
					{
						//post message to async queue
						_inMessagePort.Post(message);
					}
				}
			}
		}

		/// <summary>
		/// If message redirection is enabled and the message has been sent to either the wrong group
		/// or wrong cluster based on the local config, this will send the message to the correct 
		/// group and cluster. 
		/// </summary>
		/// <returns>True if the message needed to be redirected, false if redirection is disabled 
		/// or the message did not need to be redirected</returns>
		private bool RedirectMessage(RelayMessage message)
		{
			if(!RedirectionEnabled())
				return false;

			if ( MessageInWrongCluster(message) || MessageInWrongGroup(message) )
			{

				_counters.CountRedirectedMessage(1);
				ForwardMessage(message);
				_messageTracer.WriteMessageInfo(message);

				return true;
			}
			
			return false;
		}

		/// <summary>
		/// If message redirection is enabled and any of the messages in the list 
		/// has been sent to either the wrong group or wrong cluster based on the local config, 
		/// this will send them to the correct group and cluster. 
		/// </summary>
		private IList<RelayMessage> RedirectMessages(IList<RelayMessage> messages)
		{
			if (RedirectionEnabled())
			{
				List<RelayMessage> redirectedMessages = null;
				for (int i = 0; i < messages.Count; i++)
				{
					RelayMessage message = messages[i];
					if ( MessageInWrongCluster(message) || MessageInWrongGroup(message) )
					{
						if (redirectedMessages == null)
							redirectedMessages = new List<RelayMessage>(messages.Count); //it's likely they all are

						PrepareForRedirection(message);
						redirectedMessages.Add(message);

					}
				}

				if (redirectedMessages != null)
				{
					_counters.CountRedirectedMessage(redirectedMessages.Count);
					ForwardMessages(redirectedMessages);
					_messageTracer.WriteMessageInfo(redirectedMessages);
					return messages.Where(msg => !msg.WasRedirected).ToList();
				}
			}

			return messages;
		}


		private void ForwardMessage(RelayMessage message)
		{
			_forwardingComponent.HandleMessage(message);
		}

		private void ForwardMessages(IList<RelayMessage> messages)
		{
			_forwardingComponent.HandleMessages(messages);
		}	
		
		private bool MessageInWrongCluster(RelayMessage message)
		{
			if (_myGroup == null)
				return false;
				
			int properClusterId = _myGroup.GetClusterIndexFor(message.Id);
			if(properClusterId != _myClusterId )
			{			
				log.WarnFormat("Message {0} was sent to cluster {1} and belongs in cluster {2}, redirecting.", message, _myClusterId, properClusterId);
				PrepareForRedirection(message);
				return true;			
			}
			return false;
		}

		private bool MessageInWrongGroup(RelayMessage message)
		{
			if (_typeSettings == null || _myGroup == null)
				return false;

			if (!_typeIdBelongsHere[message.TypeId])
			{
				string properGroup = _typeSettings.GetGroupNameForId(message.TypeId);
				log.WarnFormat("Message {0} was sent to group {1} and belongs in group {2}, redirecting.", message, _myGroup.Name, properGroup);
				PrepareForRedirection(message);
				return true;
			}

			return false;
		}

		private static void PrepareForRedirection(RelayMessage message)
		{
			message.WasRedirected = true;
			message.IsInterClusterMsg = true;
			message.RelayTTL++;
		}

		private bool RedirectionEnabled()
		{
			if(!_redirectionConfigured || _forwardingComponent == null)
				return false;
			
			return true;
		}

		/// <summary>
		/// Processes the given list of <see cref="RelayMessage"/>.
		/// </summary>
		/// <remarks>
		///     <para>This method is the primary entry point for handling a list of <see cref="RelayMessage"/>.
		///     </para>
		/// </remarks>
		/// <param name="messages">The given list of <see cref="RelayMessage"/>.</param>
		public void HandleMessages(IList<RelayMessage> messages)
		{
			messages = RedirectMessages(messages);
			if (messages.Count == 0) return;
	
			_counters.CountMessageList(messages);
			_messageTracer.WriteMessageInfo(messages);
			#region Assign SourceZone for each msg

			for (int i = 0; i < messages.Count; i++)
			{
				if (messages[i].SourceZone == 0)
				{
					messages[i].SourceZone = _myZone;
				}				
			}

			#endregion

			MessageList list = new MessageList(messages);
			
			
			
			if (list.InMessageCount > 0)
			{
				_inMessagesPort.Post(list.InMessages);
			}
			if (list.OutMessageCount > 0)
			{
				HandleOutMessages(list.OutMessages);
			}
		}
		
		#endregion

		#region AcceptNewRequest

		/// <summary>
		/// Returns a value indicating if the server can accept a new request.
		/// </summary>
		/// <returns></returns>
		public bool AcceptNewRequest()
		{
			int count = _inDispatcher.PendingTaskCount;
			var od = _outDispatcher; //in case of config reload to null
			if (od != null) count += od.PendingTaskCount;
			return count < _queuedTaskThreshold;
		}

		#endregion

		#region Out Of Cluster Connection
		/// <summary>
		/// Gets or sets a value indicating if the server will refuse new or
		/// terminate existing out of cluster connections.
		/// </summary>
		public bool RefuseOutOfClusterConnection
		{
			get { return SocketServerAdapter.WhitelistOnly; }
			set
			{
				var whitelistOnly = SocketServerAdapter.WhitelistOnly;
				if (value != whitelistOnly)
				{
					if (value && _clusterAddresses == null)
					{
						throw new ApplicationException(
							"Cannot refuse out of cluster connections when node not in any cluster.");
					}
					SocketServerAdapter.WhitelistOnly = value;
					if (value)
					{
						log.Info("Refusing out of cluster connections.");
					} else
					{
						log.Info("No longer refusing out of cluster connections.");
					}
				}
			}
		}

		private bool IsInCluster(IPEndPoint remoteEndpoint)
		{
			var remoteAddress = remoteEndpoint.Address;
			var localClusterAddresses = _clusterAddresses;
			if (localClusterAddresses == null) return false;
			return localClusterAddresses.Contains(remoteAddress);
		}

		private void SetClusterAddresses(RelayNodeConfig config)
		{
			HashSet<IPAddress> localClusterAddresses = null;
			if (config != null)
			{
				var cluster = config.GetMyCluster();
				if (cluster != null)
				{
					localClusterAddresses = new HashSet<IPAddress>();
					foreach (var clusterNode in cluster.RelayNodes)
					{
						if (clusterNode.Activated)
						{
							var addr = clusterNode.IPAddress;
							if (!localClusterAddresses.Contains(addr))
							{
								localClusterAddresses.Add(addr);
							}
						}
					}
				}
			}
			_clusterAddresses = localClusterAddresses;
		}
		#endregion

		#region IRelayNodeServices Members

		/// <summary>
		/// Use this method to process an 'In' <see cref="RelayMessage"/> while providing a list of 
		/// component types that should not receive the message.
		/// </summary>
		/// <param name="message">The message to process</param>
		/// <param name="exclusionList">The components that should not receive the message</param>
		/// <exception cref="InvalidOperationException"> This exception is thrown if the message is NOT an 'In 'message type </exception>
		void IRelayNodeServices.HandleInMessageWithComponentExclusionList(RelayMessage message, params Type[] exclusionList)
		{

			if (message != null)
			{
				if (message.MessageType == MessageType.Get ||
					 message.MessageType == MessageType.Query ||
					 message.MessageType == MessageType.Invoke)
				{
					throw new InvalidOperationException("HandleInMessageWithComponentExclusionList() processes 'In' MessageTypes Only.  Encountred Out MessageType: " + message.MessageType);
				}

				_messageTracer.WriteMessageInfo(message);

				if (_components.DoHandleMessagesOfType(message.MessageType))
				{
					#region Assign SourceZone
					if (message.SourceZone == 0)
					{
						message.SourceZone = _myZone;
					}
					#endregion

					// Create RelayMessageWithContext                   
					RelayMessageWithContext msgWithContext =
						 new RelayMessageWithContext(
							  message,
							  new RelayMessageProcessingContext(exclusionList));

					//post message to async queue
					_inMessageWithContextPort.Post(msgWithContext);
				}
			}
		}

		/// <summary>
		/// Gets the size of the in message queue.
		/// </summary>
		int IRelayNodeServices.InMessageQueueSize
		{
			get
			{
				try
				{
					return _inDispatcher.PendingTaskCount;
				}
				catch (Exception)
				{
					return 0;
				}
			}
		}

		/// <summary>
		/// Gets the size of the out message queue.
		/// </summary>
		int IRelayNodeServices.OutMessageQueueSize
		{
			get
			{
				try
				{
					return _outDispatcher.PendingTaskCount;
				}
				catch (Exception)
				{
					return 0;
				}
			}
		}

		private readonly object fatalFailureLock = new object();
		private Timer fatalFailureTimer;
		private TimeSpan fatalFailureTimeout = TimeSpan.FromMinutes(5);

		/// <summary>
		///	<para>Instructs the host to shutdown because one or more components are in a corrupt state.</para>
		/// </summary>
		/// <param name="message">
		///	<para>A message that explains the failure.<see langword="null"/> if no message is available.</para>
		/// </param>
		/// <param name="exception">
		///	<para>An exception to log. <see langword="null"/> if no exception is available.</para>
		/// </param>
		void IRelayNodeServices.FailFatally(string message, Exception exception)
		{
			if (string.IsNullOrEmpty(message))
			{
				message = "Fatal failure signaled via unknown source.";
			}
			else
			{
				message = string.Format("Fatal failure signaled: {0}", message);
			}

			if (exception == null)
			{
				if (log.IsErrorEnabled)
					log.Error(message);                
			}
			else
			{
				if (log.IsErrorEnabled)
					log.ErrorFormat("{0}: {1}", message, exception);				
			}

			try
			{
				WriteToEventLog(0, 0, "Data Relay", "Application", message, exception);
			}
			catch (Exception ex)
			{

				if (log.IsErrorEnabled)
					log.ErrorFormat("Failed to write to windows event log: {0}", ex);
			}

			if (fatalFailureTimer == null)
			{
				lock (fatalFailureLock)
				{
					if (fatalFailureTimer == null)
					{
						fatalFailureTimer = new Timer(
							msg =>
							{
								if(log.IsErrorEnabled)
									log.ErrorFormat(
										"Fatal failure was signaled and clean shutdown timed out after {0}. Killing AppDomain...",
										fatalFailureTimeout);
								Environment.FailFast(msg as string);
							},
							message,
							fatalFailureTimeout,
							TimeSpan.FromMilliseconds(-1));
					}
				}
			}

			Stop();
			Environment.Exit(1);
		}

		/// <summary>
		/// Use this method to process an 'Out' <see cref="RelayMessage"/> while providing a list of 
		/// component types that should not receive the message.
		/// </summary>
		/// <param name="message">The message to process</param>
		/// <param name="exclusionList">The components that should not receive the message</param>
		/// <exception cref="InvalidOperationException"> This exception is thrown if the message is NOT an 'Out 'message type </exception>
		void IRelayNodeServices.HandleOutMessageWithComponentExclusionList(RelayMessage message, params Type[] exclusionList)
		{

			if (message != null)
			{
				if (message.MessageType != MessageType.Get &&
					message.MessageType != MessageType.Query &&
					message.MessageType != MessageType.Invoke)
				{
					throw new InvalidOperationException("HandleOutMessageWithComponentExclusionList() processes 'Out' MessageTypes Only.  Encounterd In MessageType: " + message.MessageType);
				}

				_messageTracer.WriteMessageInfo(message);

				if (_components.DoHandleMessagesOfType(message.MessageType))
				{
					#region Assign SourceZone
					if (message.SourceZone == 0)
					{
						message.SourceZone = _myZone;
					}
					#endregion

					// Create RelayMessageWithContext                   
					RelayMessageWithContext msgWithContext =
						new RelayMessageWithContext(
							message,
							new RelayMessageProcessingContext(exclusionList));

					HandleOutMessage(msgWithContext);
				}
			}
		}
		#endregion

		#region IAsyncDataHandler Members

		public IAsyncResult BeginHandleMessage(RelayMessage message, object state, AsyncCallback callback)
		{
			RelayMessageAsyncResult resultMessage = new RelayMessageAsyncResult(message, state, callback);
			try
			{
				if (message != null)
				{
					_messageTracer.WriteMessageInfo(message);

					if (_components.DoHandleMessagesOfType(message.MessageType))
					{
						#region Assign SourceZone
						if (message.SourceZone == 0)
						{
							message.SourceZone = _myZone;
						}
						#endregion

						if (message.IsTwoWayMessage)
						{
							if (_outMessagesPort == null)
							{
								throw new InvalidOperationException("DataRelay is misconfigured.  BeginHandleMessages was called without OutMessagesOnRelayThreads enabled.");
							}
							_outMessagePort.Post(resultMessage);	
						}
						else
						{
							//post message to async queue
							_inMessagePort.Post(message);
							//by wasSync being false we're letting the caller know
							//that complete is being called on the same thread
							const bool wasSynchronous = true;
							resultMessage.CompleteOperation(wasSynchronous);
						}
					}
				}
			}
			catch (Exception exc)
			{
				if (log.IsErrorEnabled)
				{
					log.ErrorFormat("Exception doing BeginHandleMessage: {0}", exc);                    
				}
				resultMessage.Exception = exc;
				const bool wasSynchronous = true;
				resultMessage.CompleteOperation(wasSynchronous);
			}
			return resultMessage;
		}

		public void EndHandleMessage(IAsyncResult asyncResult)
		{
			if (asyncResult == null) throw new ArgumentNullException("asyncResult");
			RelayMessageAsyncResult resultMessage = (RelayMessageAsyncResult)asyncResult;
			if (resultMessage.Exception != null)
			{
				throw resultMessage.Exception;
			}
		}

		public IAsyncResult BeginHandleMessages(IList<RelayMessage> messages, object state, AsyncCallback callback)
		{
			RelayMessageListAsyncResult result;

			MessageList list = new MessageList(messages);
			
			_messageTracer.WriteMessageInfo(messages);

			if (list.OutMessageCount > 0)
			{
				result = new RelayMessageListAsyncResult(list.OutMessages, state, callback);
			}
			else
			{
				result = new RelayMessageListAsyncResult(new List<RelayMessage>(0), state, callback);
			}

			try
			{
				_counters.CountMessageList(messages);

				#region Assing SourceZone for each msg
				foreach (RelayMessage message in messages)
				{
					if (message.SourceZone == 0)
					{
						message.SourceZone = _myZone;
					}
				}
				#endregion


				if (list.InMessageCount > 0)
				{
					_inMessagesPort.Post(list.InMessages);
				}

				if (list.OutMessageCount > 0)
				{
					if (_outMessagesPort == null)
					{
						throw new InvalidOperationException("DataRelay is misconfigured.  BeginHandleMessages was called without OutMessagesOnRelayThreads enabled.");
					}
					_outMessagesPort.Post(result); 
				}
				else //list.OutMessageCount == 0  // if there were no out messages we're done.
				{
					//we say it's sync because the callback is being called on the same thread
					const bool wasSynchronous = true;
					result.CompleteOperation(wasSynchronous);
				}
			}
			catch (Exception exc)
			{
				if (log.IsErrorEnabled)
				{
					log.ErrorFormat("Exception doing BeginHandleMessages: {0}", exc);
				}                
				result.Exception = exc;
				//we say it's sync because the callback is being called on the same thread
				const bool wasSynchronous = true;
				result.CompleteOperation(wasSynchronous);
			}
			return result;
		}

		public void EndHandleMessages(IAsyncResult asyncResult)
		{
			if (asyncResult == null) throw new ArgumentNullException("asyncResult");
			RelayMessageListAsyncResult resultMessage = (RelayMessageListAsyncResult)asyncResult;
			if (resultMessage.Exception != null)
			{
				throw resultMessage.Exception;
			}
		}

		#endregion

		#region Event Log Writing
		/// <summary>
		/// Write to event log using System.Diagnostics
		/// Note: this method can insert specfic event and category ID information 
		/// if MOM requires this info for detail monitoring and report generation
		/// </summary>
		/// <param name="eventID">The id associated with the event. 0 if not available.</param>
		/// <param name="eventCategory">The id associated with the event categeory. 0 if not available.</param>
		/// <param name="source">The source name by which the application is registered on the local computer.</param>
		/// <param name="logName">The name of the log the source's entries are written to. Possible values include: Application, System, or a custom event log.</param>
		/// <param name="message">The message to log.</param>
		/// <param name="exception">The exception to log; <see langword="null"/> if not available.</param>
		public static void WriteToEventLog(short eventID, short eventCategory, string source, string logName, string message, Exception exception)
		{
			string eventMessage;

			if (!EventLog.SourceExists(logName))
			{
				EventLog.CreateEventSource(source, logName);
			}

			if (exception != null)
			{
				message = string.Format("{0}\r\nException:\r\n{1}", message, GetExceptionString(exception));
			}

			// Limit event log message to 32K
			if (message.Length > 32000)
			{
				// Truncate excess characters
				eventMessage = message.Substring(0, 32000);
			}
			else
			{
				eventMessage = message;
			}

			EventLog.WriteEntry(source, eventMessage, EventLogEntryType.Error, eventID, eventCategory);
		}
		
		private static string GetExceptionString(Exception exception)
		{
			const int maxDepth = 10;
			StringBuilder result = new StringBuilder();
			for (int i = 0; i < maxDepth && exception != null; i++)
			{
				result.AppendLine(exception.ToString());
				if (exception.StackTrace != null)
				{
					result.AppendLine(exception.StackTrace);
				}
				if (exception.InnerException != null)
				{
					result.AppendLine("Inner Exception:");
				}
				exception = exception.InnerException;
			}
			return result.ToString();
		}

		#endregion 
	}
}
