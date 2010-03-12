using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;

using System.Configuration;
using System.Threading;
using MySpace.ResourcePool;

using System.Collections.Generic;
using MySpace.Shared.Configuration;

namespace MySpace.SocketTransport
{

	/// <summary>
	/// Provides a simple, lightweight socket-level transport. 
	/// Use MySpace.SocketTransport.Server on the other end.
	/// </summary>
	public class SocketClient
	{
        private static readonly MySpace.Logging.LogWrapper log = new MySpace.Logging.LogWrapper();
        private BinaryFormatter replyFormatter = new BinaryFormatter();

		private SocketSettings defaultMessageSettings;

		private SocketSettings mySettings = null;
		private Dictionary<IPEndPoint, SocketPool> mySocketPools = null;
		private IPEndPoint myEndPoint = null;
		private SocketPool mySocketPool = null;

		#region Constructors

		/// <summary>
		/// Create a new SocketClient for connecting to any number of servers with any settings.
		/// </summary>
		public SocketClient()
		{
			LoadConfig();
			mySettings = defaultMessageSettings;
			this.mySocketPools = SocketManager.Instance.GetSocketPools(defaultMessageSettings);
		}

		/// <summary>
		/// Create a new SocketClient that will use the supplied settings for all messages.
		/// </summary>
		/// <param name="settings"></param>
		public SocketClient(SocketSettings settings)
		{
			LoadConfig();
			mySettings = settings;
			this.mySocketPools = SocketManager.Instance.GetSocketPools(settings);
		}

		/// <summary>
		/// Create a new SocketClient with a default connection to destination, using the default settings.
		/// </summary>		
		public SocketClient(IPEndPoint destination)
			: this()
		{
			myEndPoint = destination;
			mySocketPool = GetSocketPool(destination);
		}

		/// <summary>
		/// Create a new SocketClient with a default connection to destination, using the supplied settings.
		/// </summary>		
		public SocketClient(IPEndPoint destination, SocketSettings settings)
		{
			LoadConfig();
			mySettings = settings;
			myEndPoint = destination;
			this.mySocketPools = SocketManager.Instance.GetSocketPools(settings);
			this.mySocketPool = GetSocketPool(destination, settings);
		}

		#region Old Constructors
		/// <summary>
		/// Create a new SocketClient using the supplied message provider. This can be used to create a custom message envelope.		
		/// </summary>		
		//public SocketClient(ISocketMessagingProvider socketMessagingProvider) 
		//{
		//    LoadConfig();
		//    oneWayCallback = new AsyncCallback(OneWaySendCallback);
		//    this.socketMessagingProvider = socketMessagingProvider;
		//    this.createSyncMessageDelegate = new CreateSyncMessageDelegate(socketMessagingProvider.CreateSyncMessage);
		//    this.mySocketPools = SocketManager.Instance.GetSocketPoolHash(defaultMessageSettings, syncUseReceive);

		//}

		//public SocketClient(ISocketMessagingProvider socketMessagingProvider, SocketSettings settings)
		//{
		//    LoadConfig();
		//    oneWayCallback = new AsyncCallback(OneWaySendCallback);
		//    this.socketMessagingProvider = socketMessagingProvider;
		//    this.createSyncMessageDelegate = new CreateSyncMessageDelegate(socketMessagingProvider.CreateSyncMessage);
		//    this.defaultMessageSettings = settings;
		//    this.mySocketPools = SocketManager.Instance.GetSocketPoolHash(settings, syncUseReceive);			
		//}

		//public SocketClient(ISocketMessagingProvider socketMessagingProvider, SocketSettings settings, IPEndPoint endPoint)
		//{
		//    LoadConfig();
		//    oneWayCallback = new AsyncCallback(OneWaySendCallback);
		//    this.socketMessagingProvider = socketMessagingProvider;
		//    this.createSyncMessageDelegate = new CreateSyncMessageDelegate(socketMessagingProvider.CreateSyncMessage);
		//    this.defaultMessageSettings = settings;
		//    this.mySocketPools = SocketManager.Instance.GetSocketPoolHash(settings, syncUseReceive);
		//    this.mySocketPool = GetSocketPool(endPoint);
		//}

		//public SocketClient(SocketSettings settings, IPEndPoint endPoint) : this(new SocketMessagingProvider(),settings,endPoint)
		//{

		//}
		#endregion
		#endregion

		private static SocketClientConfig config = null;
		internal static SocketClientConfig Config
		{
			get
			{
				if (config == null)
				{
					config = GetConfig();
				}
				return config;
			}
		}

		private static SocketClientConfig GetConfig()
		{
			SocketClientConfig config = null;
			try
			{
				config = (SocketClientConfig)ConfigurationManager.GetSection("SocketClient");
			}
			catch (Exception ex)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Exception getting socket client config: {0}", ex);
			}

			if (config == null)
			{
                if (log.IsWarnEnabled)
                    log.Warn("No Socket Client Config Found. Using defaults.");
				config = new SocketClientConfig();
				config.DefaultSocketSettings = new SocketSettings();
			}

			return config;
		}

		private void LoadConfig()
		{
			SocketClientConfig config = Config;
			this.defaultMessageSettings = config.DefaultSocketSettings;
			XmlSerializerSectionHandler.RegisterReloadNotification(typeof(SocketClientConfig), new EventHandler(ReloadConfig));
		}

		static SocketClient()
		{
			GetConfig(); //required to load the config so that the event fires.
			XmlSerializerSectionHandler.RegisterReloadNotification(typeof(SocketClientConfig), (obj, args) =>
				{
					var cc = ConfigChanged;
					if (cc != null)
					{
						cc(GetConfig());
					}
				});
		}

		/// <summary>
		/// Called by XmlSerializationSectionHandler when the config is reloaded.
		/// </summary>
		public void ReloadConfig(object sender, EventArgs args)
		{
			SocketClientConfig newConfig = GetConfig();
			if (mySettings == defaultMessageSettings) //current using defaults, if defaults change we want to change the active socket pool
			{
				if (!newConfig.DefaultSocketSettings.SameAs(defaultMessageSettings)) //settings have changed, need to change "mySocketPool(s)" reference
				{
                    if (log.IsInfoEnabled)
                        log.Info("Default socket settings changed, updating default socket pool.");

					mySocketPools = SocketManager.Instance.GetSocketPools(newConfig.DefaultSocketSettings);
					if (mySocketPool != null && myEndPoint != null)
					{
						SocketPool oldDefault = mySocketPool;
						mySocketPool = GetSocketPool(myEndPoint, newConfig.DefaultSocketSettings);
						oldDefault.ReleaseAndDisposeAll();
					}
				}
				mySettings = newConfig.DefaultSocketSettings;
			}

			defaultMessageSettings = newConfig.DefaultSocketSettings;
			config = newConfig;
		}

		/// <summary>
		/// Fired when the config changes or is modified.
		/// </summary>
		public static event SocketClientConfigChangeMethod ConfigChanged;

		/// <summary>
		/// 	<para>Get a copy of the default socket settings. 
		///		Useful for creating a settings object based on the default.</para>
		/// </summary>
		/// <returns>
		///		<para>A <see cref="SocketSettings"/> object that is the copy
		///		of the default socket settings; never <see langword="null"/>.</para>
		/// </returns>
		public static SocketSettings GetDefaultSettings()
		{
			return Config.DefaultSocketSettings.Copy();
		}

		/// <summary>
		/// Get the total number of sockets created, and the number currently in use.
		/// </summary>
		/// <param name="totalSockets">The total number of sockets created.</param>
		/// <param name="activeSockets">The number of sockets currently in use.</param>
		public void GetSocketCounts(out int totalSockets, out int activeSockets)
		{
			SocketManager.Instance.GetSocketCounts(out totalSockets, out activeSockets);
		}

		/// <summary>
		/// Get the number of sockets created and in use for a given destination using the default socket settings.
		/// </summary>
		/// <param name="destination">The server endpoint to check for.</param>
		/// <param name="totalSockets">The number of sockets created.</param>
		/// <param name="activeSockets">The number of active sockets.</param>
		public void GetSocketCounts(IPEndPoint destination, out int totalSockets, out int activeSockets)
		{
			SocketManager.Instance.GetSocketCounts(destination, out totalSockets, out activeSockets);
		}

		/// <summary>
		/// Get the number of sockets created and in use for a given destination and settings combination. 
		/// </summary>
		/// <param name="destination">The server endpoint to check for.</param>
		/// <param name="settings">The settings object portion of the pool key.</param>
		/// <param name="totalSockets">The number of sockets created.</param>        
		/// <param name="activeSockets">The number of active sockets.</param>
		public void GetSocketCounts(IPEndPoint destination, SocketSettings settings, out int totalSockets, out int activeSockets)
		{
			SocketManager.Instance.GetSocketCounts(destination, settings, out totalSockets, out activeSockets);
		}


		#region SendOneWay

		/// <summary>
		/// Sends a message to the default server that does not expect a reply, using the default message settings and the default destination.
		/// </summary>
		/// <param name="commandID">The Command Identifier to send to the server. The server's IMessageHandler should know about all possible CommandIDs</param>
		/// <param name="messageStream">The contents of the message for the server to process.</param>
		public void SendOneWay(int commandID, MemoryStream messageStream)
		{
			if (mySocketPool == null)
			{
				throw new ApplicationException("Attempt to use default-destination send without a default destination.");
			}

			SendOneWay(mySocketPool, commandID, messageStream);
		}

		/// <summary>
		/// Sends a message to a server that does not expect a reply using the default message settings.
		/// </summary>
		/// <param name="destination">The server's EndPoint</param>
		/// <param name="commandId">The Command Identifier to send to the server. The server's IMessageHandler should know about all possible CommandIDs</param>
		/// <param name="messageStream">The contents of the message for the server to process.</param>
		public void SendOneWay(IPEndPoint destination, int commandId, MemoryStream messageStream)
		{
			SocketPool socketPool = GetSocketPool(destination);
			SendOneWay(socketPool, commandId, messageStream);
		}

		/// <summary>
		/// Sends a message to a server that does not expect a reply.
		/// </summary>
		/// <param name="destination">The server's EndPoint</param>
		/// <param name="messageSettings">Settings for the transport.</param>
		/// <param name="commandID">The Command Identifier to send to the server. The server's IMessageHandler should know about all possible CommandIDs</param>
		/// <param name="messageStream">The contents of the message for the server to process.</param>		
		public void SendOneWay(IPEndPoint destination, SocketSettings messageSettings, int commandID, MemoryStream messageStream)
		{
			SocketPool socketPool = GetSocketPool(destination, messageSettings);
			SendOneWay(socketPool, commandID, messageStream);
		}

		private void SendOneWay(SocketPool pool, int commandID, MemoryStream messageStream)
		{
			ManagedSocket socket = null;
			ResourcePoolItem<MemoryStream> rebufferedStreamItem = CreateOneWayMessage(commandID, messageStream, pool);

			try
			{
				MemoryStream rebufferedStream = rebufferedStreamItem.Item;
				socket = pool.GetSocket();
				// GetBuffer() should be used in preference to ToArray() where possible
				// as it does not allocate a new byte[] like ToArray does().
				byte[] messageBuffer = rebufferedStream.GetBuffer();
				socket.Send(messageBuffer, (int)rebufferedStream.Length, SocketFlags.None);
			}
			catch (SocketException sex)
			{
				if (socket != null)
				{
					socket.LastError = sex.SocketErrorCode;
				}
				throw;
			}
			finally
			{
				if (socket != null)
				{
					socket.Release();
				}
				rebufferedStreamItem.Release();
			}
		}

		#endregion

		#region SendSync
		/// <summary>
		/// Sends a message to the default server that expects a response, using the default message settings. To use this function you must have used a constructor with an IPEndPoint.
		/// </summary>
		/// <param name="commandID">The Command Identifier to send to the server. The server's IMessageHandler should know about all possible CommandIDs</param>
		/// <param name="messageStream">The contents of the message for the server to process.</param>
		/// <returns>The object returned by the server, if any.</returns>
		public MemoryStream SendSync(int commandID, MemoryStream messageStream)
		{
			if (mySocketPool == null)
			{
				throw new ApplicationException("Attempt to use default-destination send without a default destination.");
			}
			MemoryStream replyStream = SendSync(mySocketPool, commandID, messageStream);

			return replyStream;
		}

		/// <summary>
		/// Sends a message to the server that expects a response, using the default message settings.
		/// </summary>
		/// <param name="destination">The server's EndPoint</param>
		/// <param name="commandID">The Command Identifier to send to the server. The server's IMessageHandler should know about all possible CommandIDs</param>
		/// <param name="messageStream">The contents of the message for the server to process.</param>
		/// <returns>The object returned by the server, if any.</returns>
		public MemoryStream SendSync(IPEndPoint destination, int commandID, MemoryStream messageStream)
		{

			MemoryStream replyStream = null;
			SocketPool socketPool = GetSocketPool(destination);

			replyStream = SendSync(socketPool, commandID, messageStream);


			return replyStream;
		}

		/// <summary>
		/// Sends a message to the server that expects a response.
		/// </summary>
		/// <param name="destination">The server's EndPoint</param>
		/// <param name="messageSettings">The settings to use for the transport.</param>
		/// <param name="commandID">The Command Identifier to send to the server. The server's IMessageHandler should know about all possible CommandIDs</param>
		/// <param name="messageStream">The contents of the message for the server to process.</param>
		/// <returns>The object returned by the server, if any.</returns>
		public MemoryStream SendSync(IPEndPoint destination, SocketSettings messageSettings, int commandID, MemoryStream messageStream)
		{

			SocketPool pool = SocketManager.Instance.GetSocketPool(destination, messageSettings);
			MemoryStream replyStream = SendSync(pool, commandID, messageStream);

			return replyStream;
		}

		private MemoryStream SendSync(SocketPool pool, int commandID, MemoryStream messageStream)
		{
			short messageId = (short)1; //new async scheme doesn't currently need these.
			ResourcePoolItem<MemoryStream> rebufferedStreamItem = CreateSyncMessage((short)commandID, messageId, messageStream, pool);
			MemoryStream rebufferedStream = rebufferedStreamItem.Item;

			ManagedSocket socket = null;
			MemoryStream replyStream = null;

			try
			{
				socket = pool.GetSocket();

				// GetBuffer() should be used in preference to ToArray() where possible
				// as it does not allocate a new byte[] like ToArray does().
				socket.Send(rebufferedStream.GetBuffer(), (int)rebufferedStream.Length, SocketFlags.None);
				replyStream = socket.GetReply();
			}
			catch (ThreadAbortException)
			{
				if (socket != null)
				{
					socket.LastError = SocketError.TimedOut;
				}
				log.Warn("Thread aborted on SocketClient.");
				throw;
			}
			catch (SocketException ex)
			{
				if (socket != null)
				{
					socket.LastError = ex.SocketErrorCode;
				}
				throw;
			}
			finally
			{
				rebufferedStreamItem.Release();
				if (socket != null) //getting the socket can throw a timedout exception due to limiting, in which case the socket will be null
				{
					socket.Release();
				}
			}

			return replyStream;
		}

		#endregion

		#region Socket Pools
		private SocketPool GetSocketPool(IPEndPoint destination)
		{
			SocketPool pool;
			if (mySocketPools.ContainsKey(destination))
			{
				pool = mySocketPools[destination];
			}
			else
			{
				lock (mySocketPools)
				{
					if (!mySocketPools.ContainsKey(destination))
					{
						pool = BuildSocketPool(destination, mySettings);
						mySocketPools.Add(destination, pool);
					}
					else
					{
						pool = mySocketPools[destination];
					}
				}
			}

			return pool;
		}

		private static SocketPool GetSocketPool(IPEndPoint destination, SocketSettings settings)
		{
			return SocketManager.Instance.GetSocketPool(destination, settings);
		}

		private static SocketPool BuildSocketPool(IPEndPoint destination, SocketSettings settings)
		{
			return SocketManager.BuildSocketPool(destination, settings);
		}
		#endregion

		#region Message Creation
		private volatile int envelopeSize = 13; //size in bytes of the non-message information transmitted with each message
		private byte[] doSendReply = BitConverter.GetBytes(true);
		private byte[] dontSendReply = BitConverter.GetBytes(false);
		private byte[] messageStarterHost = BitConverter.GetBytes(Int16.MaxValue);
		private byte[] messageTerminatorHost = BitConverter.GetBytes(Int16.MinValue);
		private byte[] messageStarterNetwork = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Int16.MaxValue));
		private byte[] messageTerminatorNetwork = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Int16.MinValue));

		internal ResourcePoolItem<MemoryStream> CreateOneWayMessage(int commandId, MemoryStream messageStream, SocketPool pool)
		{
			return CreateMessage((short)commandId, 0, messageStream, false, pool);
		}

		internal ResourcePoolItem<MemoryStream> CreateSyncMessage(Int16 commandId, Int16 messageId, MemoryStream messageStream, SocketPool pool)
		{
			return CreateMessage(commandId, messageId, messageStream, true, pool);
		}

		private ResourcePoolItem<MemoryStream> CreateMessage(short commandId, short messageId, MemoryStream messageStream, bool isSync, SocketPool pool)
		{
			int messageLength = 0;

			if (messageStream != null)
				messageLength = (int)messageStream.Length;
			else
				messageLength = 0;

			bool useNetworkOrder = pool.Settings.UseNetworkOrder;

			byte[] length = BitConverter.GetBytes(GetNetworkOrdered(messageLength + envelopeSize, useNetworkOrder));
			byte[] commandIdBytes;
			byte[] messageIdBytes = null;
			//byte[] code = BitConverter.GetBytes(GetNetworkOrdered(commandId, useNetworkOrder));
			if (messageId != 0)
			{
				commandIdBytes = BitConverter.GetBytes(GetNetworkOrdered(commandId, useNetworkOrder));
				messageIdBytes = BitConverter.GetBytes(GetNetworkOrdered(messageId, useNetworkOrder));
			}
			else
			{
				commandIdBytes = BitConverter.GetBytes(GetNetworkOrdered((int)commandId, useNetworkOrder));
			}

			//MemoryStream rebufferedStream = new MemoryStream(envelopeSize + messageLength);			

			ResourcePoolItem<MemoryStream> rebufferedStreamItem = pool.GetPooledStream();

			MemoryStream rebufferedStream = rebufferedStreamItem.Item;

			rebufferedStream.Write(GetMessageStarter(useNetworkOrder), 0, 2);
			rebufferedStream.Write(length, 0, 4);
			//rebufferedStream.Write(code, 0, 4);
			if (messageId != 0)
			{
				if (useNetworkOrder)
				{
					rebufferedStream.Write(messageIdBytes, 0, 2);
					rebufferedStream.Write(commandIdBytes, 0, 2);
				}
				else
				{
					rebufferedStream.Write(commandIdBytes, 0, 2);
					rebufferedStream.Write(messageIdBytes, 0, 2);
				}
			}
			else //backwards compatible, just send the command as an int
			{
				rebufferedStream.Write(commandIdBytes, 0, 4);
			}

			if (isSync)
				rebufferedStream.Write(doSendReply, 0, doSendReply.Length);
			else
				rebufferedStream.Write(dontSendReply, 0, dontSendReply.Length);

			if (messageStream != null)
			{
				messageStream.WriteTo(rebufferedStream);
			}
			rebufferedStream.Write(GetMessageTerminator(useNetworkOrder), 0, 2);

			return rebufferedStreamItem;
		}



		private byte[] GetMessageStarter(bool useNetworkOrder)
		{
			return (useNetworkOrder ? messageStarterNetwork : messageStarterHost);
		}

		private byte[] GetMessageTerminator(bool useNetworkOrder)
		{
			return (useNetworkOrder ? messageTerminatorNetwork : messageTerminatorHost);
		}

		#region Network Ordering Methods

		private Int16 GetNetworkOrdered(Int16 number, bool useNetworkOrder)
		{
			if (useNetworkOrder)
			{
				return IPAddress.HostToNetworkOrder(number);
			}
			else
			{
				return number;
			}
		}

		private Int32 GetNetworkOrdered(Int32 number, bool useNetworkOrder)
		{
			if (useNetworkOrder)
			{
				return IPAddress.HostToNetworkOrder(number);
			}
			else
			{
				return number;
			}
		}

		private Int64 GetNetworkOrdered(Int64 number, bool useNetworkOrder)
		{
			if (useNetworkOrder)
			{
				return IPAddress.HostToNetworkOrder(number);
			}
			else
			{
				return number;
			}
		}

		private Int16 GetHostOrdered(Int16 number, bool useNetworkOrder)
		{
			if (useNetworkOrder)
			{
				return IPAddress.NetworkToHostOrder(number);
			}
			else
			{
				return number;
			}
		}

		private Int32 GetHostOrdered(Int32 number, bool useNetworkOrder)
		{
			if (useNetworkOrder)
			{
				return IPAddress.NetworkToHostOrder(number);
			}
			else
			{
				return number;
			}
		}

		private Int64 GetHostOrdered(Int64 number, bool useNetworkOrder)
		{
			if (useNetworkOrder)
			{
				return IPAddress.NetworkToHostOrder(number);
			}
			else
			{
				return number;
			}
		}

		#endregion
		#endregion


	}


}
