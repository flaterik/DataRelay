using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Threading;
using Microsoft.Ccr.Core;
using MySpace.Configuration;
using MySpace.DataRelay.Configuration;
using MySpace.DataRelay.Http;
using MySpace.DataRelay.Server.Common;
using MySpace.Logging;
using System.Text;

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
		RelayNodeConfig configuration;
		int queuedTaskThreshold = Int32.MaxValue;
		RelayNodeCounters counters;
		RelayNodeCounters countersInternal;
		Dispatcher inDispatcher;
		Dispatcher outDispatcher;
		DispatcherQueue inMessageQueue;
		DispatcherQueue outMessageQueue;
		Port<RelayMessage> inMessagePort = new Port<RelayMessage>();
		Port<RelayMessageWithContext> inMessageWithContextPort = new Port<RelayMessageWithContext>();
		Port<IList<RelayMessage>> inMessagesPort = new Port<IList<RelayMessage>>();

		Port<RelayMessageAsyncResult> outMessagePort;
		Port<RelayMessageListAsyncResult> outMessagesPort;

		private MessageTracer messageTracer;

		RelayComponents components;
		Timer queuedMessageCounterTimer;
		ushort MyZone;
		
		private ICollection<IPAddress> clusterAddresses;
		private Timer resetConnectionRefusalTimer;

		private HttpServer _httpServer;


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
				
				if (configuration == null) throw new ConfigurationErrorsException("config failed to load, is null");

				SetClusterAddresses(configuration);

				fatalFailureTimeout = configuration.FatalShutdownTimeout < 0
					? TimeSpan.FromMinutes(5)
					: TimeSpan.FromSeconds(configuration.FatalShutdownTimeout);

				components = new RelayComponents(configuration);

				if (configuration != null)
				{
					messageTracer = new MessageTracer(configuration.TypeSettings.MaxTypeId, configuration.TraceSettings);
					messageTracer.Activated = configuration.OutputTraceInfo;

					const string inThreadsName = "DataRelayNode";
					if (configuration.NumberOfThreads > 0)
					{
						inDispatcher = new Dispatcher(configuration.NumberOfThreads, ThreadPriority.Normal, true, inThreadsName);
					}
					else
					{
						inDispatcher = new Dispatcher() { Name = inThreadsName } ;
					}

					const string outThreadsName = "DataRelayNodeOUT";
					if (configuration.OutMessagesOnRelayThreads)
					{
						if (configuration.NumberOfOutMessageThreads > 0)
						{
							outDispatcher = new Dispatcher(configuration.NumberOfOutMessageThreads, ThreadPriority.Normal, true, outThreadsName);
						}
						else
						{
							outDispatcher = new Dispatcher { Name = outThreadsName };
						}

						outMessagePort = new Port<RelayMessageAsyncResult>();
						outMessagesPort = new Port<RelayMessageListAsyncResult>();

						outMessageQueue = new DispatcherQueue("DataRelayDispatcherQueueOUT", outDispatcher, TaskExecutionPolicy.ConstrainQueueDepthThrottleExecution, configuration.MaximumOutMessageQueueDepth);
						Arbiter.Activate(outMessageQueue,
								Arbiter.ReceiveWithIterator(true, outMessagePort, HandleOutMessage));
						Arbiter.Activate(outMessageQueue,
								Arbiter.ReceiveWithIterator(true, outMessagesPort, HandleOutMessages));
					}

					inMessageQueue = new DispatcherQueue("DataRelayDispatcherQueue", inDispatcher, TaskExecutionPolicy.ConstrainQueueDepthThrottleExecution, configuration.MaximumMessageQueueDepth);
					
					queuedTaskThreshold = (int)Math.Floor(0.9 * configuration.MaximumMessageQueueDepth);
					
					
					// setup RelayServicesClient before initalizing components
					RelayServicesClient.Instance.RelayNodeServices = this;
					
					Arbiter.Activate(inMessageQueue,
						Arbiter.Receive<RelayMessage>(true, inMessagePort, HandleInMessage));
					Arbiter.Activate(inMessageQueue,
								Arbiter.Receive<RelayMessageWithContext>(true, inMessageWithContextPort, HandleInMessage));
					Arbiter.Activate(inMessageQueue,
								Arbiter.Receive<IList<RelayMessage>>(true, inMessagesPort, HandleInMessages));
					

					//by having after the Arbiter.Activate it allows Initialize components to use 
					//IRelayNodeServices that require Message handling
					components.Initialize(componentRunStates, configuration.IgnoredMessageTypes);

					queuedMessageCounterTimer = new Timer(CountQueuedMessages, null, 5000, 5000);
				}
			}
			catch (Exception ex)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Exception initializing relay node: {0}", ex);
				throw; //should bring server down
			}
		}

		public ComponentRunState[] GetComponentRunStates()
		{
			return components.GetComponentRunStates();
		}
		
		public ComponentRuntimeInfo[] GetComponentsRuntimeInfo()
		{
			return components.GetComponentsRuntimeInfo();
		}

		public ComponentRuntimeInfo GetComponentRuntimeInfo(string componentName)
		{
			return components.GetComponentRuntimeInfo(componentName);
		}

		public string GetComponentsDescription()
		{
			return components.GetComponentsDescription();
		}

		private int GetResetDuration()
		{
			var resetDuration = 0;
			// node
			var node = configuration.GetMyNode();
			if (node == null) return resetDuration;
			resetDuration = node.StartupRepopulateDuration;
			if (resetDuration != 0) return resetDuration;
			// cluster
			var cluster = configuration.GetMyCluster();
			if (cluster == null) return resetDuration;
			resetDuration = cluster.StartupRepopulateDuration;
			if (resetDuration != 0) return resetDuration;
			// group
			var group = configuration.GetMyGroup();
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
			counters = new RelayNodeCounters();
			countersInternal = new RelayNodeCounters();
			countersInternal.Initialize("Internal");
			counters.Initialize(instanceName);
			if (portNumber != 0)
			{
				var resetDuration = GetResetDuration();
				bool whitelistOnly = resetDuration > 0;
				if (whitelistOnly && clusterAddresses == null)
				{
					throw new ApplicationException(
						"Cannot configure refuse out of cluster connections when node not in cluster.");
				}
				
				SocketServerAdapter.Initialize(instanceName, portNumber, this,
					configuration.OutMessagesOnRelayThreads, IsInCluster,
					whitelistOnly);

				if (whitelistOnly)
				{
					resetConnectionRefusalTimer = new Timer(state =>
                	{
                		var timer = Interlocked.Exchange(ref resetConnectionRefusalTimer, null);
						if (timer != null)
						{
							RefuseOutOfClusterConnection = false;
							timer.Dispose();
						}
                	}, null, resetDuration*1000, Timeout.Infinite);
				}
				else
				{
					resetConnectionRefusalTimer = null;
				}
			}

			StartHttpServer();
			
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
			if (queuedMessageCounterTimer != null)
			{
				queuedMessageCounterTimer.Change(Timeout.Infinite, Timeout.Infinite);
				queuedMessageCounterTimer.Dispose();
			}

			var timer = Interlocked.Exchange(ref resetConnectionRefusalTimer, null);
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
			inDispatcher.Dispose();
			var od = outDispatcher; //in case of config reload to null
			if (od != null) outDispatcher.Dispose();

			StopHttpServer();

			components.Shutdown();

            if (log.IsInfoEnabled)
                log.Info("Resetting Counters");

			if (counters != null)
			{
				counters.ResetCounters();
				counters.Shutdown();
				counters = null;
			}
            
            if (log.IsInfoEnabled)
                log.Info("Relay Node Stopped.");
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

		string instanceName = null;

		private int portNumber = 0;
		private int httpPortNumber = 80;

		/// <summary>
		/// Gets the IP port this instance is listening on.
		/// </summary>
		public int Port
		{
			get { return portNumber; }
		}

		private void GetConfig()
		{
			EventHandler reloadEventHandler = ReloadConfig;
			configuration = RelayNodeConfig.GetRelayNodeConfig(reloadEventHandler);
			
			if (configuration != null)
			{
				if (configuration.GetMyNode() != null)
				{
					MyZone = configuration.GetMyNode().Zone;
				}

				instanceName = configuration.InstanceName;
				if (configuration.TransportSettings != null)
				{
					portNumber = configuration.TransportSettings.ListenPort;
					httpPortNumber = configuration.TransportSettings.HttpListenPort;
				}

			}
			else
			{
                if (log.IsErrorEnabled)
                    log.Error("NO CONFIG SECTION FOUND, SERVICE NOT STARTING.");
			}
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
					MyZone = newConfiguration.GetMyNode().Zone;
				}

				SetClusterAddresses(newConfiguration);
				
				messageTracer.ReloadConfig(newConfiguration.TypeSettings.MaxTypeId, newConfiguration.TraceSettings);
				messageTracer.Activated = newConfiguration.OutputTraceInfo;

				//TODO: handle changes in component definition
				components.ReloadConfig(newConfiguration, newConfiguration.IgnoredMessageTypes);

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

				if (newConfiguration.NumberOfThreads != configuration.NumberOfThreads)
				{
					if(log.IsInfoEnabled)
                        log.InfoFormat("Changing number of relay node threads from {0} to {1}", 
                            configuration.NumberOfThreads, newConfiguration.NumberOfThreads);
					try
					{
						Dispatcher oldInDispatcher = inDispatcher;
						Dispatcher newInDispatcher;
						const string inThreadsName = "DataRelayNode";
						if (newConfiguration.NumberOfThreads > 0)
						{
							newInDispatcher = new Dispatcher(newConfiguration.NumberOfThreads, ThreadPriority.Normal, true, inThreadsName);
						}
						else
						{
							newInDispatcher = new Dispatcher() { Name = inThreadsName };
						}

						DispatcherQueue newInQueue = new DispatcherQueue("DataRelayDispatcherQueue", newInDispatcher, TaskExecutionPolicy.ConstrainQueueDepthThrottleExecution, newConfiguration.MaximumMessageQueueDepth);

						Interlocked.Exchange(ref inMessagePort, new Port<RelayMessage>());
						Interlocked.Exchange(ref inMessageWithContextPort, new Port<RelayMessageWithContext>());
						Interlocked.Exchange(ref inMessagesPort, new Port<IList<RelayMessage>>());

						Arbiter.Activate(newInQueue,
							 Arbiter.Receive<RelayMessage>(true, inMessagePort, HandleInMessage));
						Arbiter.Activate(newInQueue,
							 Arbiter.Receive<RelayMessageWithContext>(true, inMessageWithContextPort, HandleInMessage));
						Arbiter.Activate(newInQueue,
							 Arbiter.Receive<IList<RelayMessage>>(true, inMessagesPort, HandleInMessages));

						inMessageQueue = newInQueue;
						inDispatcher = newInDispatcher;
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
					inMessageQueue.MaximumQueueDepth = newConfiguration.MaximumMessageQueueDepth;
				}

				SetupOutMessagesOnRelayThreads(newConfiguration);

				queuedTaskThreshold = (int)Math.Floor(0.9 * newConfiguration.MaximumMessageQueueDepth);
				configuration = newConfiguration;
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
			bool setupNewOutMessages = (newConfiguration.OutMessagesOnRelayThreads && configuration.OutMessagesOnRelayThreads == false)
			                           || (configuration.OutMessagesOnRelayThreads && newConfiguration.OutMessagesOnRelayThreads
			                               && newConfiguration.NumberOfOutMessageThreads != configuration.NumberOfOutMessageThreads);

			Dispatcher oldOutDispatcher = outDispatcher;
			DispatcherQueue oldOutMessageQueue = outMessageQueue;

			if (setupNewOutMessages)
			{
				try
				{
					const string outThreadsName = "DataRelayNodeOUT";
						
					outMessagePort = new Port<RelayMessageAsyncResult>(); //atomic
					outMessagesPort = new Port<RelayMessageListAsyncResult>(); //atomic

					if (newConfiguration.NumberOfOutMessageThreads > 0)
					{
						outDispatcher = new Dispatcher(newConfiguration.NumberOfOutMessageThreads, ThreadPriority.Normal, true, outThreadsName);
					}
					else
					{
						outDispatcher = new Dispatcher { Name = outThreadsName };
					}

					outMessageQueue = new DispatcherQueue("DataRelayDispatcherQueueOUT", outDispatcher, TaskExecutionPolicy.ConstrainQueueDepthThrottleExecution, newConfiguration.MaximumOutMessageQueueDepth);

					Arbiter.Activate(outMessageQueue,
									 Arbiter.ReceiveWithIterator(true, outMessagePort, HandleOutMessage));
					Arbiter.Activate(outMessageQueue,
									 Arbiter.ReceiveWithIterator(true, outMessagesPort, HandleOutMessages));
						
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
				outMessagePort = null;
				outMessagesPort = null;
				if (oldOutDispatcher != null) oldOutDispatcher.Dispose();
				if (oldOutMessageQueue != null) oldOutMessageQueue.Dispose();
			}
		}

		#endregion

		#region Private Members

		private IEnumerator<ITask> HandleOutMessage(RelayMessageAsyncResult asyncMessage)
		{
			try
			{
				counters.CountInputBytes(asyncMessage.Message);
				foreach (var task in components.HandleOutMessage(asyncMessage))
				{
					yield return task;
				}
			}
			finally
			{
				counters.CountOutMessage(asyncMessage.Message);
				const bool wasSynchronous = false;
				asyncMessage.CompleteOperation(wasSynchronous);
			}
		}

		private void HandleOutMessage(RelayMessage message)
		{
			try
			{
				counters.CountInputBytes(message);
				components.HandleOutMessage(message);
				counters.CountOutMessage(message);
				if (message.AllowsReturnPayload == false) message.Payload = null;
			}
			catch (Exception exc)
			{
				message.Payload = null;
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error handling message {0}: {1}", message, exc);
			}
		}

		private void HandleInMessage(RelayMessage message)
		{
			try
			{
				counters.CountInputBytes(message);
				components.HandleInMessage(message);
				counters.CountInMessage(message);
			}
			catch (Exception exc)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error handling message {0}: {1}", message, exc);
			}
		}
		private void HandleInMessage(RelayMessageWithContext messageWithContext)
		{
			try
			{
				counters.CountInputBytes(messageWithContext.RelayMessage);
				components.HandleInMessage(messageWithContext);
				countersInternal.CountInMessage(messageWithContext.RelayMessage);
			}
			catch (Exception exc)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error handling message {0}: {1}", messageWithContext.RelayMessage, exc);				
			}
		}

		private void HandleOutMessage(RelayMessageWithContext messageWithContext)
		{
			try
			{
				counters.CountInputBytes(messageWithContext.RelayMessage);
				components.HandleOutMessage(messageWithContext);
				countersInternal.CountOutMessage(messageWithContext.RelayMessage);
				if (messageWithContext.RelayMessage.AllowsReturnPayload == false) messageWithContext.RelayMessage.Payload = null;
			}
			catch (Exception exc)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error handling message {0}: {1}", messageWithContext.RelayMessage, exc);				
			}
		}

		private void HandleInMessages(IList<RelayMessage> messages)
		{
			try
			{
				counters.CountInputBytes(messages);
				components.HandleInMessages(messages);
				counters.CountInMessages(messages);
			}
			catch (Exception ex)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error handling in message list: {0}", ex);				                
			}
		}

		private IEnumerator<ITask> HandleOutMessages(RelayMessageListAsyncResult asyncMessages)
		{
			try
			{
				counters.CountInputBytes(asyncMessages.Messages);
				foreach (var task in components.HandleOutMessages(asyncMessages))
				{
					yield return task;
				}
			}
			finally
			{
				counters.CountOutMessages(asyncMessages.Messages);
				const bool wasSynchronous = false;
				asyncMessages.CompleteOperation(wasSynchronous);
			}
		}

		private void HandleOutMessages(IList<RelayMessage> messages)
		{
			try
			{
				counters.CountInputBytes(messages);
				components.HandleOutMessages(messages);
				counters.CountOutMessages(messages);
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
		}

		private void CountQueuedMessages(object state)
		{
			if (counters != null)
			{
				int count = inDispatcher.PendingTaskCount;
				var od = outDispatcher; //in case of config reload to null
				if (od != null) count += od.PendingTaskCount;
				counters.SetNumberOfQueuedMessages(count);
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
				messageTracer.WriteMessageInfo(message);

				if (components.DoHandleMessagesOfType(message.MessageType))
				{
					#region Assign SourceZone
					if (message.SourceZone == 0)
					{
						message.SourceZone = MyZone;
					}
					#endregion

					if (message.IsTwoWayMessage)
					{
						HandleOutMessage(message);
					}
					else
					{
						//post message to async queue
						inMessagePort.Post(message);
					}
				}
			}
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
			counters.CountMessageList(messages);

			#region Assing SourceZone for each msg
			foreach (RelayMessage message in messages)
			{
				if (message.SourceZone == 0)
				{
					message.SourceZone = MyZone;
				}
			}
			#endregion

			MessageList list = new MessageList(messages);
			
			messageTracer.WriteMessageInfo(messages);
			
			if (list.InMessageCount > 0)
			{
				inMessagesPort.Post(list.InMessages);
			}
			if (list.OutMessageCount > 0)
			{
				HandleOutMessages(list.OutMessages);
			}
		}

		#endregion

		#region AcceptNewConnection

		/// <summary>
		/// Returns a value indicating if the server can accept a new request.
		/// </summary>
		/// <returns></returns>
		public bool AcceptNewConnection()
		{
			int count = inDispatcher.PendingTaskCount;
			var od = outDispatcher; //in case of config reload to null
			if (od != null) count += od.PendingTaskCount;
			return count < queuedTaskThreshold;
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
					if (value && clusterAddresses == null)
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
			var localClusterAddresses = clusterAddresses;
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
			clusterAddresses = localClusterAddresses;
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

				messageTracer.WriteMessageInfo(message);

				if (components.DoHandleMessagesOfType(message.MessageType))
				{
					#region Assign SourceZone
					if (message.SourceZone == 0)
					{
						message.SourceZone = MyZone;
					}
					#endregion

					// Create RelayMessageWithContext                   
					RelayMessageWithContext msgWithContext =
						 new RelayMessageWithContext(
							  message,
							  new RelayMessageProcessingContext(exclusionList));

					//post message to async queue
					inMessageWithContextPort.Post(msgWithContext);
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

				messageTracer.WriteMessageInfo(message);

				if (components.DoHandleMessagesOfType(message.MessageType))
				{
					#region Assign SourceZone
					if (message.SourceZone == 0)
					{
						message.SourceZone = MyZone;
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
					messageTracer.WriteMessageInfo(message);

					if (components.DoHandleMessagesOfType(message.MessageType))
					{
						#region Assign SourceZone
						if (message.SourceZone == 0)
						{
							message.SourceZone = MyZone;
						}
						#endregion

						if (message.IsTwoWayMessage)
						{
							if (outMessagesPort == null)
							{
								throw new InvalidOperationException("DataRelay is misconfigured.  BeginHandleMessages was called without OutMessagesOnRelayThreads enabled.");
							}
							outMessagePort.Post(resultMessage);	
						}
						else
						{
							//post message to async queue
							inMessagePort.Post(message);
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
			
			messageTracer.WriteMessageInfo(messages);

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
				counters.CountMessageList(messages);

				#region Assing SourceZone for each msg
				foreach (RelayMessage message in messages)
				{
					if (message.SourceZone == 0)
					{
						message.SourceZone = MyZone;
					}
				}
				#endregion


				if (list.InMessageCount > 0)
				{
					inMessagesPort.Post(list.InMessages);
				}

				if (list.OutMessageCount > 0)
				{
					if (outMessagesPort == null)
					{
						throw new InvalidOperationException("DataRelay is misconfigured.  BeginHandleMessages was called without OutMessagesOnRelayThreads enabled.");
					}
					outMessagesPort.Post(result); 
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
