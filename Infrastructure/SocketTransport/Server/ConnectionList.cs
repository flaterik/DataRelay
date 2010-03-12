using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Diagnostics;
using MySpace.Common.HelperObjects;
using System.Threading;

namespace MySpace.SocketTransport
{
	public class ConnectionList
	{
		private readonly Dictionary<IPEndPoint,ConnectionState> connections;
		private readonly PerformanceCounter socketCountCounter;
		int socketCount;
		private readonly Byte[] emptyMessage = new Byte[0] { };
		private readonly MsReaderWriterLock connectionsLock =
			new MsReaderWriterLock(System.Threading.LockRecursionPolicy.NoRecursion);

		public ConnectionList(PerformanceCounter socketCounter)
		{
			socketCountCounter = socketCounter;			
			connections = new Dictionary<IPEndPoint, ConnectionState>(2000);			
		}

		public int SocketCount
		{
			get
			{
				return socketCount;
			}
		}

		public ConnectionState this[IPEndPoint endPoint]
		{
			get
			{
				ConnectionState state = null;
				connectionsLock.Read(() =>
              		connections.TryGetValue(endPoint, out state)
              	);
				return state;
			}
		}

		public Socket GetSocket(IPEndPoint endPoint)
		{
			ConnectionState state = null;
			connectionsLock.Read(delegate { state = this[endPoint]; });
			if (state != null)
			{
				return state.WorkSocket;
			}
			else
			{
				return null;
			}
		}

		public void Add(ConnectionState connection)
		{
			connectionsLock.Write(delegate {
			    connections.Add(connection.remoteEndPoint, connection);
				IncrementCount();
			});
		}

		public bool Remove(ConnectionState connection)
		{
			bool removed = false;
			connectionsLock.Write(delegate
          	{
          		removed = connections.Remove(connection.remoteEndPoint);
				if (removed)
				{
					DecrementCount();
				}
			});			
			return removed;
		}

		public bool Remove(IPEndPoint endpoint)
		{
			bool removed = false;
			connectionsLock.Write(delegate
			{
				removed = connections.Remove(endpoint);
				if (removed)
				{
					DecrementCount();
				}
			});
			return removed;
		}
		

		public void CheckConnections()
		{
			IPEndPoint[] keys = null;			
			ConnectionState state;
			Socket socket;

			if (connections.Count > 0)
			{
				connectionsLock.Read(delegate
              	{
					keys = new IPEndPoint[connections.Count];
					connections.Keys.CopyTo(keys, 0);
				});
				for (var i = 0; i < keys.Length; i++)
				{
					var key = keys[i];
					state = this[key];
					if (state == null) continue;
					lock (state)
					{
						socket = state.WorkSocket;
						if (socket == null) continue;
						try
						{
							socket.Send(emptyMessage, 0, 0);
							if (!socket.Connected)
							{
								if(SocketServer.log.IsWarnEnabled)
                                    SocketServer.log.Warn("Connection check disposing of non-responsive socket.");
								Close(state);
								Remove(key);
							}
						}
						catch (SocketException sex)
						{
                            if (SocketServer.log.IsErrorEnabled)
                                SocketServer.log.ErrorFormat("Connection check disposing of non-responsive socket - Socket Exception: {0}. Socket Error: {1}.",sex,sex.SocketErrorCode);
							Close(state);
							Remove(key);
						}
						catch (ObjectDisposedException)
						{
							Close(state);
							Remove(key);
						}
						catch (Exception ex)
						{
							if (SocketServer.log.IsErrorEnabled)
                                SocketServer.log.ErrorFormat("Unexpected exception while checking SocketServer connections: {0}",ex);
							Close(state);
							Remove(key);
						}
					}
					Thread.Sleep(2); //be gentle to the processor
				}

			}
		}

		private void ZeroCount()
		{
			socketCount = 0;
			if (socketCountCounter != null)
			{
				socketCountCounter.RawValue = 0;
			}
		}

		private void IncrementCount()
		{
			++socketCount;
			if (socketCountCounter != null)
			{
				socketCountCounter.Increment();
			}
			
		}

		private void DecrementCount()
		{
			--socketCount;
			if (socketCountCounter != null)
			{
				socketCountCounter.Decrement();
			}
		}

		private static void Close(ConnectionState state)
		{
			lock(state)
			{
				var socket = state.WorkSocket;
				if (socket != null)
				{
					state.WorkSocket = null;
					try
					{
						if (socket.Connected)
						{
							socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
							socket.Shutdown(SocketShutdown.Both);
							socket.Close();
						}
					}
					catch (ObjectDisposedException)
					{
					} // ignore for already closed outside this code
				}
			}
		}

		public void Purge()
		{
			ConnectionState[] values = null;
			connectionsLock.ReadUpgradable(delegate
         	{
         		values = new ConnectionState[connections.Count];
				connections.Values.CopyTo(values, 0);
         		connectionsLock.Write(delegate
              	{
              		connections.Clear();
					ZeroCount();
              	});
         	});
      		foreach (var state in values)
      		{
      			try
      			{
      				Close(state);
      			}
      			catch (Exception ex)
      			{
                    if (SocketServer.log.IsErrorEnabled)
                        SocketServer.log.ErrorFormat("Exception closing socket during shutdown: {0}",ex.ToString());
      			}
      		}
		}

		/// <summary>
		/// Closes out any connections that don't satisfy a supplied callback.
		/// </summary>
		/// <param name="connectionWhitelist"><see cref="ConnectionWhitelist"/>
		/// callback that takes the remote <see cref="IPEndPoint"/> and
		/// returns a <see cref="Boolean"/> specifying whether to retain
		/// the connection.</param>
		/// <remarks>If the callback returns <see langword="true"/> then
		/// the connection is retained; otherwise it is closed.</remarks>
		public void PurgeNotWhitelisted(ConnectionWhitelist connectionWhitelist)
		{
			var purgedStates = new List<ConnectionState>();
			connectionsLock.ReadUpgradable(delegate
         	{
				var count = connections.Count;
         		var keys = new IPEndPoint[count];
         		connections.Keys.CopyTo(keys, 0);
         		var values = new ConnectionState[count];
         		connections.Values.CopyTo(values, 0);
				for(var idx = count - 1; idx >= 0; --idx)
				{
					var key = keys[idx];
					if (!connectionWhitelist(key))
					{
						var value = values[idx];
						connectionsLock.Write(delegate
                      	{
                      		connections.Remove(key);
                      		DecrementCount();
                      	});
						purgedStates.Add(value);
					}
				}
			});
			foreach(var state in purgedStates)
			{
				try
				{
					Close(state);
				}
				catch (Exception ex)
				{
                    if (SocketServer.log.IsErrorEnabled)
                        SocketServer.log.ErrorFormat("Exception doing whitelist check of sockets for closing: {0}",ex.ToString());
				}				
			}
		}
	}



}
