using System;
using System.Net;
using System.IO;
using MySpace.ResourcePool;

namespace MySpace.SocketTransport
{
	internal abstract class SocketPool
	{
		
		internal SocketPool(
			IPEndPoint destination,
			SocketSettings settings
			)
		{
			this.destination = destination;
			poolSize = settings.PoolSize;
			Settings = settings;			
			TimeSpan socketLifetime = new TimeSpan(0, 0, settings.SocketLifetimeMinutes, 0, 0);			
			socketLifetimeTicks = socketLifetime.Ticks;
			if (SocketClient.config.UseSharedBufferPool)
			{
				rebufferedStreamPool = SocketManager.Instance.SharedBufferPool;
			}
			else
			{
				rebufferedStreamPool = new MemoryStreamPool(settings.InitialMessageSize, settings.BufferReuses);
			}
		}
		protected Logging.LogWrapper log = new Logging.LogWrapper();
		protected IPEndPoint destination;
		protected int poolSize;		
		internal SocketSettings Settings;		
		protected long socketLifetimeTicks;
		internal int socketCount;
		internal int activeSocketCount;
		private readonly MemoryStreamPool rebufferedStreamPool;

		internal IPEndPoint Destination
		{
			get
			{
				return destination;
			}
		}

		internal abstract ManagedSocket GetSocket();
		
		internal abstract void ReleaseSocket(ManagedSocket socket);

		internal abstract void ReleaseAndDisposeAll();

		protected bool SocketAgedOut(ManagedSocket socket)
		{
			long ageTicks = DateTime.UtcNow.Ticks - socket.CreatedTicks;
			return ageTicks > socketLifetimeTicks;
		}
		
		~SocketPool()
		{
			ReleaseAndDisposeAll();
		}
		
		internal ResourcePoolItem<MemoryStream> GetPooledStream()
		{
			return rebufferedStreamPool.GetItem();
		}
	}


}