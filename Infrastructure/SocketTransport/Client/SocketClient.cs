using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
		private static readonly Logging.LogWrapper log = new Logging.LogWrapper();
		internal static SocketClientConfig config { get; private set; }

		private readonly SocketSettings mySettings;
		private readonly Dictionary<IPEndPoint, SocketPool> mySocketPools;
		
		private readonly SocketPool mySocketPool;

		/// <summary>
		/// Fired when the config changes or is modified.
		/// </summary>
		public static event SocketClientConfigChangeMethod ConfigChanged;

		private const int envelopeSize = 13; //size in bytes of the non-message information transmitted with each message
		private static readonly byte[] doSendReply = BitConverter.GetBytes(true);
		private static readonly byte[] dontSendReply = BitConverter.GetBytes(false);
		private static readonly byte[] messageStarterHost = BitConverter.GetBytes(Int16.MaxValue);
		private static readonly byte[] messageTerminatorHost = BitConverter.GetBytes(Int16.MinValue);
		private static readonly byte[] messageStarterNetwork = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Int16.MaxValue));
		private static readonly byte[] messageTerminatorNetwork = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Int16.MinValue));

		internal const short SendAckMessageId = Int16.MinValue;
		
		#region Constructors

		/// <summary>
		/// Create a new SocketClient for connecting to any number of servers with any settings.
		/// </summary>
		public SocketClient()
		{
		}

		/// <summary>
		/// Create a new SocketClient that will use the supplied settings for all messages.
		/// </summary>
		/// <param name="settings"></param>
		public SocketClient(SocketSettings settings)
		{
			mySettings = settings;
			mySocketPools = SocketManager.Instance.GetSocketPools(settings);
		}

		/// <summary>
		/// Create a new SocketClient with a default connection to destination, using the default settings.
		/// </summary>		
		public SocketClient(IPEndPoint destination)
		{
			//ideally if the default settings are changed, then this reference should be as well
			//but there is no way to do this without a delegate, and previous users of socket client
			//can't be expected to start using a dispose method, so this functionality will have to
			//not exist. 
			mySocketPool = SocketManager.Instance.GetSocketPool(destination);
		}

		/// <summary>
		/// Create a new SocketClient with a default connection to destination, using the supplied settings.
		/// </summary>		
		public SocketClient(IPEndPoint destination, SocketSettings settings)
		{
			mySettings = settings;
			mySocketPools = SocketManager.Instance.GetSocketPools(settings);
			mySocketPool = SocketManager.Instance.GetSocketPool(destination, settings);
		}
		
		#endregion

		private static SocketClientConfig GetConfig()
		{
			SocketClientConfig newConfig = null;
			try
			{
				newConfig = (SocketClientConfig)ConfigurationManager.GetSection("SocketClient");
			}
			catch (Exception ex)
			{
				if (log.IsErrorEnabled)
					log.ErrorFormat("Exception getting socket client config: {0}", ex);
			}

			if (newConfig == null)
			{
				if (log.IsWarnEnabled)
					log.Warn("No Socket Client Config Found. Using defaults.");
				newConfig = new SocketClientConfig(new SocketSettings());
			}

			return newConfig;
		}
		
		static SocketClient()
		{
			config = GetConfig();
			XmlSerializerSectionHandler.RegisterReloadNotification(typeof(SocketClientConfig), ReloadConfig);
		}

		/// <summary>
		/// Called by XmlSerializationSectionHandler when the config is reloaded.
		/// </summary>
		public static void ReloadConfig(object sender, EventArgs args)
		{
			SocketClientConfig newConfig = GetConfig();
			
			SocketManager.Instance.SetNewConfig(newConfig);
			
			config = newConfig;

			if (ConfigChanged != null)
				ConfigChanged(newConfig);
		}

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
			return config.DefaultSocketSettings.Copy();
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
			//we have the destination, we want to use any settings supplied at instantiation, or if none were
			//supplied then, the defaults

			SocketPool socketPool = SocketManager.Instance.GetSocketPool(destination, mySettings, mySocketPools);
			SendOneWay(socketPool, commandId, messageStream);
		}

		/// <summary>
		/// Sends a message to a server that does not expect a reply using the process wide default message settings.
		/// </summary>
		/// <param name="destination">The server's EndPoint</param>
		/// <param name="commandId">The Command Identifier to send to the server. The server's IMessageHandler should know about all possible CommandIDs</param>
		/// <param name="messageStream">The contents of the message for the server to process.</param>
		public static void SendOneWayDefault(IPEndPoint destination, int commandId, MemoryStream messageStream)
		{
			//we have the destination, we want to use any settings supplied at instantiation, or if none were
			//supplied then, the defaults

			SocketPool socketPool = SocketManager.Instance.GetSocketPool(destination);
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
			//we have both destination and settings so just use them
			SocketPool socketPool = SocketManager.Instance.GetSocketPool(destination, messageSettings);
			SendOneWay(socketPool, commandID, messageStream);
		}

		private static void SendOneWay(SocketPool pool, int commandID, MemoryStream messageStream)
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

				socket.Send(messageBuffer, (int) rebufferedStream.Length, SocketFlags.None);
				if (socket.ServerSupportsAck && pool.Settings.RequestOneWayAck)
					try
					{
						socket.GetReply(); //make sure we got the ack
					}
					catch (SocketException sex)
					{
						log.ErrorFormat("Failed to receive ack from {0} with error {1}", pool.Destination, sex.SocketErrorCode);
						throw;
					}
					catch (Exception ex)
					{
						log.ErrorFormat("Failed to receive ack from {0} with error {1}", pool.Destination, ex.Message);
						throw;
					}
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
				SocketException backgroundException = null;
				if (socket != null)
				{
					if (socket.LastError != SocketError.Success)
					{
						backgroundException = new SocketException((int)socket.LastError);
					}
					socket.Release();
				}
				rebufferedStreamItem.Release();
				
				if (backgroundException != null)
					throw backgroundException;
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
			SocketPool socketPool = SocketManager.Instance.GetSocketPool(destination, mySettings, mySocketPools);
			MemoryStream replyStream = SendSync(socketPool, commandID, messageStream);
			return replyStream;
		}

		/// <summary>
		/// Sends a message to the server that expects a response, using the process wide default message settings.
		/// </summary>
		/// <param name="destination">The server's EndPoint</param>
		/// <param name="commandID">The Command Identifier to send to the server. The server's IMessageHandler should know about all possible CommandIDs</param>
		/// <param name="messageStream">The contents of the message for the server to process.</param>
		/// <returns>The object returned by the server, if any.</returns>
		public static MemoryStream SendSyncDefault(IPEndPoint destination, int commandID, MemoryStream messageStream)
		{
			SocketPool socketPool = SocketManager.Instance.GetSocketPool(destination);
			MemoryStream replyStream = SendSync(socketPool, commandID, messageStream);
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

		private static MemoryStream SendSync(SocketPool pool, int commandID, MemoryStream messageStream)
		{
			const short messageId = 1; //new async scheme doesn't currently need these.
			ResourcePoolItem<MemoryStream> rebufferedStreamItem = CreateSyncMessage((short)commandID, messageId, messageStream, pool);
			MemoryStream rebufferedStream = rebufferedStreamItem.Item;

			ManagedSocket socket = null;
			MemoryStream replyStream;

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
		
		#region Message Creation

		internal static ResourcePoolItem<MemoryStream> CreateOneWayMessage(int commandId, MemoryStream messageStream, SocketPool pool)
		{
			return CreateMessage((short)commandId, pool.Settings.RequestOneWayAck ? SendAckMessageId : (short)0, messageStream, false, pool);
		}

		internal static ResourcePoolItem<MemoryStream> CreateSyncMessage(Int16 commandId, Int16 messageId, MemoryStream messageStream, SocketPool pool)
		{
			return CreateMessage(commandId, messageId, messageStream, true, pool);
		}

		private static ResourcePoolItem<MemoryStream> CreateMessage(short commandId, short messageId, MemoryStream messageStream, bool isSync, SocketPool pool)
		{
			bool useNetworkOrder = pool.Settings.UseNetworkOrder;
			ResourcePoolItem<MemoryStream> rebufferedStreamItem = pool.GetPooledStream();
			WriteMessageToStream(commandId, messageId, messageStream, isSync, useNetworkOrder, rebufferedStreamItem.Item);
			return rebufferedStreamItem;
		}

		internal static void WriteMessageToStream(short commandId, short messageId, MemoryStream messageStream, bool isSync, bool useNetworkOrder, MemoryStream rebufferedStream)
		{
			int messageLength;

			if (messageStream != null)
				messageLength = (int)messageStream.Length;
			else
				messageLength = 0;

			

			byte[] length = BitConverter.GetBytes(GetNetworkOrdered(messageLength + envelopeSize, useNetworkOrder));
			byte[] commandIdBytes;
			byte[] messageIdBytes = null;
			
			if (messageId != 0)
			{
				commandIdBytes = BitConverter.GetBytes(GetNetworkOrdered(commandId, useNetworkOrder));
				messageIdBytes = BitConverter.GetBytes(GetNetworkOrdered(messageId, useNetworkOrder));
			}
			else
			{
				commandIdBytes = BitConverter.GetBytes(GetNetworkOrdered((int)commandId, useNetworkOrder));
			}

			rebufferedStream.Write(GetMessageStarter(useNetworkOrder), 0, 2);
			rebufferedStream.Write(length, 0, 4);
			
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
		}



		private static byte[] GetMessageStarter(bool useNetworkOrder)
		{
			return (useNetworkOrder ? messageStarterNetwork : messageStarterHost);
		}

		private static byte[] GetMessageTerminator(bool useNetworkOrder)
		{
			return (useNetworkOrder ? messageTerminatorNetwork : messageTerminatorHost);
		}

		#region Network Ordering Methods

		private static Int16 GetNetworkOrdered(Int16 number, bool useNetworkOrder)
		{
			if (useNetworkOrder)
			{
				return IPAddress.HostToNetworkOrder(number);
			}
			
			return number;
		}

		private static Int32 GetNetworkOrdered(Int32 number, bool useNetworkOrder)
		{
			if (useNetworkOrder)
			{
				return IPAddress.HostToNetworkOrder(number);
			}
			
			return number;
		}

		#endregion
		#endregion


	}


}
