using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MySpace.SocketTransport
{
	internal class NullSocketPool : SocketPool
	{
		internal NullSocketPool(
			IPEndPoint destination,
			SocketSettings settings
			)
			: base(destination, settings)
		{
		}

		internal override ManagedSocket GetSocket()
		{
			ManagedSocket socket = new ManagedSocket(Settings, this);
			socket.Connect(destination);
			Interlocked.Increment(ref activeSocketCount);
			Interlocked.Increment(ref socketCount);
			return socket;
		}

		internal override void ReleaseSocket(ManagedSocket socket)
		{
			ReleaseAndDisposeSocket(socket);
		}

		internal void ReleaseAndDisposeSocket(ManagedSocket socket)
		{
			try
			{
				socket.Shutdown(SocketShutdown.Both);
				socket.Close();
				Interlocked.Decrement(ref activeSocketCount);
				Interlocked.Decrement(ref socketCount);
			}
			catch (SocketException)
			{ }
			catch (ObjectDisposedException)
			{ }
		}

		internal override void ReleaseAndDisposeAll()
		{
			activeSocketCount = 0;
			socketCount = 0;
			return;
		}
	}
}
