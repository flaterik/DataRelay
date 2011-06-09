using System;
using System.Collections.Generic;
using System.Text;
using MySpace.SocketTransport;

namespace MySpace.DataRelay
{	
    /// <summary>
    /// Provides helper functionality for the <see cref="RelayNode"/> to work with Sockets.
    /// </summary>
	internal class SocketServerAdapter
	{
		private static SocketServer _socketServer = null;
		private static RelayNode _myRelayNode = null;
		private static readonly object _syncRoot = new object();
        private static readonly MySpace.Logging.LogWrapper log = new MySpace.Logging.LogWrapper();
    	private static ConnectionWhitelist _connectionWhitelist;
		private static bool? _cachedWhiteListValue;

        /// <summary>
        /// Binds the server to a port and starts the server to accept new connections.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="portNumber"></param>
        /// <param name="relayNode">The node to handle new message requests.</param>
		/// <param name="useAsyncHandler">A value indicating if out messages should be asynchronous.</param>
		/// <param name="connectionWhitelist">A delegate that tells if a given
		/// remote endpoint is allowable to connect.</param>
		/// <param name="whitelistOnly">Whether to only allow whitelisted connections.</param>
		public static void Initialize(string instanceName, int portNumber, RelayNode relayNode,
			bool useAsyncHandler, ConnectionWhitelist connectionWhitelist,
			bool whitelistOnly)
        {
			lock (_syncRoot)
			{
        		_myRelayNode = relayNode;
				_connectionWhitelist = connectionWhitelist;
				_setupNewSocketServer(relayNode, instanceName, portNumber,
					useAsyncHandler, _connectionWhitelist, whitelistOnly);
			}
        }

		/// <summary>
		/// Gets or sets a value indicating whether or not the adapter should refuse
		/// new or terminate existing connections that don't satisfy the whitelist
		/// function passed in <see cref="Initialize"/>.
		/// </summary>
		public static bool WhitelistOnly
    	{
			get {
				if (_socketServer == null) return _cachedWhiteListValue.GetValueOrDefault(false);
				return _socketServer.WhitelistOnly; 
			}
			set 
			{
				if (_socketServer == null)
				{
					_cachedWhiteListValue = value;
				}
				else
				{
					_socketServer.WhitelistOnly = value;
				}
			}
    	}

		private static void _setupNewSocketServer(RelayNode relayNode, string instanceName, int portNumber, bool useAsyncHandler, ConnectionWhitelist connectionWhitelist, bool whitelistOnly) 
		{
			//all public method should lock(_syncRoot) so we should be ok
			SocketServer socketServer = new SocketServer(instanceName, portNumber);

			IMessageHandler messageHandler;
			if (useAsyncHandler)
			{
				messageHandler = new SocketServerAsyncMessageHandler(relayNode, relayNode);
			}
			else
			{
				messageHandler = new SocketServerRelayMessageHandler(relayNode, relayNode);
			}

			socketServer.MessageHandler = messageHandler;
			socketServer.AcceptingConnectionsDelegate = relayNode.AcceptNewRequest;
			socketServer.AcceptingRequestsDelegate = relayNode.AcceptNewRequest;
			socketServer.WhitelistOnly = whitelistOnly;
			socketServer.Start(connectionWhitelist);

			_socketServer = socketServer;
			if (_cachedWhiteListValue != null)
			{
				_socketServer.WhitelistOnly = _cachedWhiteListValue.Value;
				_cachedWhiteListValue = null;
			}
    	}

    	/// <summary>
        /// Changes the port that the server is listening on to the given port.
        /// </summary>
        /// <param name="newPort">The given port.</param>
		public static void ChangePort(int newPort)
		{
			lock (_syncRoot)
			{
				if (_socketServer == null) return; //not initialized

				//if there was not port change return.
				if (_socketServer.PortNumber == newPort) return;

				try
				{
					SocketServer oldServer = _socketServer;
					bool useAsyncHandler = _socketServer.MessageHandler is SocketServerAsyncMessageHandler;
					bool whitelistOnly = _socketServer.WhitelistOnly;
					_setupNewSocketServer(_myRelayNode, oldServer.InstanceName,
						newPort, useAsyncHandler, _connectionWhitelist, whitelistOnly);
					oldServer.Stop();
				}
				catch (Exception ex)
				{
                    if (log.IsErrorEnabled)
                        log.Error("Error changing listen port: {0}", ex);
				}
			}
		}
		
        /// <summary>
        /// Stops the server from listening on a port.
        /// </summary>
		public static void Shutdown()
		{
			lock (_syncRoot)
			{
				if (_socketServer != null && _socketServer.IsRunning)
				{
					_socketServer.Stop();
					_socketServer = null;
				}
			}
		}
	}

   
}
