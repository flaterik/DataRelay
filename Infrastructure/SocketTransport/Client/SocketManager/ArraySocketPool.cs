using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;


namespace MySpace.SocketTransport
{
	internal class ArrayManagedSocket : ManagedSocket
	{
		internal int Index = -1;

		internal ArrayManagedSocket(SocketSettings settings, SocketPool pool, int index)
			: base(settings, pool)
		{
			this.Index = index;
		}		
	}

	
	internal class ArraySocketPool : SocketPool
	{
		private ArrayManagedSocket[] sockets = null;
		private object padlock = new object();
		private object growlock = new object();
		private bool growing = false;
		private static readonly int MaximumPoolSize = 10000;
		
		internal ArraySocketPool(IPEndPoint destination, SocketSettings settings)
			: base(destination, settings)
		{
			sockets = new ArrayManagedSocket[this.poolSize];						
		}

		private ArrayManagedSocket BuildSocket(int index)
		{
			ArrayManagedSocket socket = new ArrayManagedSocket(this.Settings, this, index);
			socket.Connect(this.destination, this.Settings.ConnectTimeout);
			Interlocked.Increment(ref socketCount);
			return socket;

		}

		internal override ManagedSocket GetSocket()
		{
			ArrayManagedSocket socket = null;
			lock(padlock)
			{
				while(socket == null)
				{
					for(int i = 0 ; i < sockets.Length ; i++)
					{
						if(sockets[i] == null)
						{
							sockets[i] = BuildSocket(i);
							Interlocked.Increment(ref activeSocketCount);
							socket = sockets[i];
							break;
						}
						
						if(sockets[i].Idle)
						{
							try
							{
								if (sockets[i].LastError == SocketError.Success)
								{
									sockets[i].Idle = false;
									Interlocked.Increment(ref activeSocketCount);
									socket = sockets[i];
								}
								else
								{
									ReleaseAndDisposeSocket(sockets[i]);
								}
							}
							catch(ObjectDisposedException)
							{
								sockets[i] = BuildSocket(i);
								Interlocked.Increment(ref activeSocketCount);
								socket = sockets[i];
							}
					
							break;
						}
					}
					if (socket == null)
					{
						if (log.IsWarnEnabled)
							log.WarnFormat("All sockets in a socket pool for {0} are in use.", destination);
						if (sockets.Length < ArraySocketPool.MaximumPoolSize)						
						{						
							GrowPool();
						}
					}
				}
			}
			return socket;
		}

		private void GrowPool()
		{
			if (log.IsInfoEnabled)
				log.InfoFormat("Growing socket pool for {0} to size {1}.", destination, sockets.Length * 2);
			ArrayManagedSocket[] newSockets = new ArrayManagedSocket[sockets.Length * 2];
			lock (growlock)
			{
				growing = true;
				for (int i = 0; i < sockets.Length; i++)
				{
					newSockets[i] = sockets[i];
				}
				sockets = newSockets;
				growing = false;
			}
		}

		internal override void ReleaseSocket(ManagedSocket socket)
		{
			if (socket.LastError != SocketError.Success || SocketAgedOut(socket))			
			{
				ReleaseAndDisposeSocket(socket);
			}
			else
			{
				Interlocked.Decrement(ref activeSocketCount);
				socket.Idle = true;
			}
		}
		
		internal void ReleaseAndDisposeSocket(ManagedSocket socket)
		{
			if (socket != null)
			{
				try
				{
					socket.Idle = false;
					Interlocked.Decrement(ref activeSocketCount);
					Interlocked.Decrement(ref socketCount);
				
					socket.Shutdown(SocketShutdown.Both);
					socket.Close();
					if (growing)
					{
						lock (growlock)
						{
							sockets[((ArrayManagedSocket)socket).Index] = null;
						}
					}
					else
					{
						sockets[((ArrayManagedSocket)socket).Index] = null;
					}
					socket = null;
				}
				catch (SocketException)
				{ }
				catch (ObjectDisposedException)
				{ }
			}

		}

		internal override void ReleaseAndDisposeAll()
		{
			lock(this)
			{
				for(int i = 0 ; i < sockets.Length ; i++)
				{
					if(sockets[i] != null)
					{
						try
						{
							if(sockets[i].Connected)
							{
								sockets[i].Shutdown(SocketShutdown.Both);
							}
							sockets[i].Close();
							sockets[i] = null;
						}
						catch(SocketException)
						{}
						catch(ObjectDisposedException)
						{}
					}
				}
				socketCount = 0;
				activeSocketCount = 0;
			}
		}

	}
	
}
