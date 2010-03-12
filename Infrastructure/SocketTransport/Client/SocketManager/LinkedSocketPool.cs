using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using Wintellect.PowerCollections;

namespace MySpace.SocketTransport
{

	internal class LinkedManagedSocket : ManagedSocket
	{
		internal LinkedManagedSocket Next;
		
		internal LinkedManagedSocket(SocketSettings settings, SocketPool pool)
			: base(settings, pool)
		{			
		}		
	}

	internal class LinkedSocketPortOnlyComparer : IEqualityComparer<LinkedManagedSocket>
	{
		#region IEqualityComparer<LinkedManagedSocket> Members

		public bool Equals(LinkedManagedSocket x, LinkedManagedSocket y)
		{
			return ((IPEndPoint)x.LocalEndPoint).Port == ((IPEndPoint)y.LocalEndPoint).Port;
		}

		public int GetHashCode(LinkedManagedSocket obj)
		{
			return ((IPEndPoint)obj.LocalEndPoint).Port;
		}

		#endregion
	}
	
	internal class LinkedManagedSocketPool : SocketPool
	{
		private bool useLimiter;
		private Semaphore socketLimiter;
		private int socketLimiterValue;

		private Set<LinkedManagedSocket> sockets;
		private int connectTimeout = 5000;

		private readonly object setLock = new object();
		private readonly object padLock = new object();

		private LinkedManagedSocket nextSocket;
		
		
		

		internal LinkedManagedSocketPool(IPEndPoint destination, SocketSettings settings)
			: base(destination, settings)
		{
			sockets = new Set<LinkedManagedSocket>(new LinkedSocketPortOnlyComparer());
			
			if (settings.PoolSize > 0)
			{
				useLimiter = true;
				socketLimiter = new Semaphore(settings.PoolSize, settings.PoolSize);
				socketLimiterValue = settings.PoolSize;
			}			
			connectTimeout = settings.ConnectTimeout;
		}

		private LinkedManagedSocket BuildSocket()
		{
			LinkedManagedSocket socket = new LinkedManagedSocket(this.Settings, this);
			socket.Connect(this.destination, this.Settings.ConnectTimeout);
			Interlocked.Increment(ref socketCount);
			return socket;
		}		

		internal override ManagedSocket GetSocket()
		{
			LinkedManagedSocket socket = null;
			if(EnterLimiter())			
			{
				try
				{
					lock(padLock)
	              	{
	              		LinkedManagedSocket foundSocket = null;
						if (nextSocket != null)
						{
							while (foundSocket == null)
							{
								if (nextSocket == null) //might be upon second iteration of this loop
								{
									foundSocket = BuildSocket();
									lock (setLock)
									{
										sockets.Add(foundSocket);
									}
									Interlocked.Increment(ref activeSocketCount);
									break;
									//return socket;
								}

								if (nextSocket.LastError == SocketError.Success) //async receive could set an error at any time.
								{
									foundSocket = nextSocket;
								}
								else //not null, has error
								{
									LinkedManagedSocket badSocket = nextSocket;
									nextSocket = nextSocket.Next; //look at the next one
									badSocket.Next = null;
									DisposeSocket(badSocket, false);
								}
							}
							//here we would've grabbed the first non-errored socket off of the linked list. 
							foundSocket.Idle = false;
							nextSocket = foundSocket.Next;
							foundSocket.Next = null;
							Interlocked.Increment(ref activeSocketCount);
							socket = foundSocket;
							//return socket;
						}
					}//next socket was null. we no longer need a lock
					if(socket != null) //found a good one on the list or built one in the loop
						return socket;

					//else, make a new one. which.. probably should not be able to happen.
					socket = BuildSocket();
					lock (setLock)
					{
						sockets.Add(socket);
					}
					Interlocked.Increment(ref activeSocketCount);
					return socket;
				}
				catch
				{
					ExitLimiter();
					throw;
				}
			}

			throw new SocketException((int)SocketError.TooManyOpenSockets);
		}

		private bool EnterLimiter()
		{
			if (useLimiter)
			{
				//enter the semaphore. It starts at MaxValue, and is incremented when a socket is released or disposed.
				if (socketLimiter.WaitOne(connectTimeout, false))
				{
					Interlocked.Decrement(ref socketLimiterValue);
					return true;
				}				
				return false;		
			}
			
			return true;
		}

		private void ExitLimiter()
		{
			if (useLimiter)
			{
				try
				{
					socketLimiter.Release();
					Interlocked.Increment(ref socketLimiterValue);
				}
				catch (SemaphoreFullException)
				{
					if(log.IsErrorEnabled)
                    log.ErrorFormat("Socket pool for {0} released a socket too many times.", destination);
				}
			}
		}
		
		internal override void ReleaseSocket(ManagedSocket socket)
		{
			try
			{
				if (socket.LastError != SocketError.Success || SocketAgedOut(socket))
				{
					DisposeSocket(socket);
				}
				else
				{
					if (!socket.Idle)
					{
						lock(padLock)
						{
							socket.Idle = true;
							if (nextSocket == null)
							{
								nextSocket = (LinkedManagedSocket)socket;
							}
							else
							{
								LinkedManagedSocket newNextSocket = (LinkedManagedSocket)socket;
								LinkedManagedSocket currentNextSocket = nextSocket;
								newNextSocket.Next = currentNextSocket;
								nextSocket = newNextSocket;
							}
							ExitLimiter();
						}
						Interlocked.Decrement(ref activeSocketCount);
					}
				}
			}
			catch (Exception ex)
			{
				if(log.IsErrorEnabled)
                    log.ErrorFormat("Exception releasing socket: {0}", ex);
			}
		}

		private void DisposeSocket(ManagedSocket socket)
		{
			DisposeSocket(socket, true);
		}
		
		private void DisposeSocket(ManagedSocket socket, bool pullFromRotation)
		{
			bool exitLimiter = false;
			if (socket != null)
			{
				try
				{
					if (!socket.Idle)
					{
						exitLimiter = true;
						Interlocked.Decrement(ref activeSocketCount);
					}
					if (pullFromRotation)
					{
						PullSocketFromRotation((LinkedManagedSocket)socket);
					}
					socket.Idle = false;
					Interlocked.Decrement(ref socketCount);

					lock (setLock)
					{
						sockets.Remove((LinkedManagedSocket)socket);
					}
					
					if (socket.Connected)
					{
						socket.Shutdown(SocketShutdown.Both);
					}
					socket.Close();
				}
				catch (SocketException)
				{ }
				catch (ObjectDisposedException)
				{
					exitLimiter = false;
                    if (log.IsErrorEnabled)
                        log.ErrorFormat("Attempt to release and dispose disposed socket by pool for {0}", destination);
				}
				finally
				{
					if (exitLimiter)
					{
						ExitLimiter();
					}
				}
			}

		}

		private void PullSocketFromRotation(LinkedManagedSocket socket)
		{
			lock(padLock)
          	{
          		if (nextSocket != null)
          		{
          			if (socket == nextSocket)
          			{
          				nextSocket = nextSocket.Next;
          			}
          			else
          			{
          				LinkedManagedSocket pointer = nextSocket.Next;
          				LinkedManagedSocket prevPointer = null;
          				while (pointer != null && pointer != socket)
          				{
          					prevPointer = pointer;
          					pointer = pointer.Next;
          				}
          				if (pointer == socket && prevPointer != null && pointer != null)
          				{
          					prevPointer.Next = pointer.Next; //skip over it!
          				}
          			}
          		}
          	}

		}

		internal override void ReleaseAndDisposeAll()
		{
			lock(padLock)
          	{
          		foreach (LinkedManagedSocket socket in sockets)
          		{
          			try
          			{
          				if (socket.Connected)
          				{
          					socket.Shutdown(SocketShutdown.Both);
          				}
          				socket.Close();
          				Interlocked.Decrement(ref activeSocketCount);
          				Interlocked.Decrement(ref socketCount);
          			}
          			catch (SocketException)
          			{
          			}
          			catch (ObjectDisposedException)
          			{
          			}
          		}
          		nextSocket = null;
          	}
		}

	}
	
}
