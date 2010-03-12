using System.Collections.Generic;
using System.Net;
using MySpace.ResourcePool;

namespace MySpace.SocketTransport
{
	internal class SocketManager
	{
		private static SocketManager _instance;
		private static readonly object _padlock = new object();

		internal void GetSocketCounts(out int totalSockets, out int activeSockets)
		{
			lock (_socketPools)
			{
				totalSockets = 0;
				activeSockets = 0;
				foreach (Dictionary<IPEndPoint, SocketPool> pools in _socketPools.Values)
				{
					foreach (SocketPool pool in pools.Values)
					{
						totalSockets += pool.socketCount;
						activeSockets += pool.activeSocketCount;
					}
				}
			}
		}

		internal void GetSocketCounts(IPEndPoint destination, out int totalSockets, out int activeSockets)
		{
			SocketPool pool = GetSocketPool(destination);
			activeSockets = pool.activeSocketCount;
			totalSockets = pool.socketCount;
		}

		internal void GetSocketCounts(IPEndPoint destination, SocketSettings settings, out int totalSockets, out int activeSockets)
		{
			SocketPool pool = GetSocketPool(destination, settings);
			activeSockets = pool.activeSocketCount;
			totalSockets = pool.socketCount;
		}

		internal static SocketManager Instance
		{
			get
			{
				if (_instance == null)
				{
					lock (_padlock)
					{
						if (_instance == null)
						{
							_instance = new SocketManager();
						}
					}
				}

				return _instance;
			}
		}

		private readonly Dictionary<SocketSettings, Dictionary<IPEndPoint, SocketPool>> _socketPools;
		private readonly SocketSettings _defaultSettings;
		private readonly MemoryStreamPool _sharedBufferPool;

		internal MemoryStreamPool SharedBufferPool
		{
			get
			{
				return _sharedBufferPool;
			}
		}

		private SocketManager()
		{
			_socketPools = new Dictionary<SocketSettings, Dictionary<IPEndPoint, SocketPool>>(2);
			_defaultSettings = SocketClient.GetDefaultSettings();
			Dictionary<IPEndPoint, SocketPool> defaultSettingsPools = new Dictionary<IPEndPoint, SocketPool>(50);
			_sharedBufferPool = new MemoryStreamPool(_defaultSettings.InitialMessageSize, _defaultSettings.BufferReuses, SocketClient.Config.SharedPoolMinimumItems);
			_socketPools.Add(_defaultSettings, defaultSettingsPools);
		}

		internal Dictionary<IPEndPoint, SocketPool> GetSocketPools(SocketSettings settings)
		{
			Dictionary<IPEndPoint, SocketPool> poolsForSettings;

			if (!_socketPools.TryGetValue(settings, out poolsForSettings))
			{
				lock (_socketPools)
				{
					if (!_socketPools.TryGetValue(settings, out poolsForSettings))
					{
						poolsForSettings = new Dictionary<IPEndPoint, SocketPool>(50);
						_socketPools.Add(settings, poolsForSettings);
						return poolsForSettings;
					}
				}
			}

			return poolsForSettings;
		}

		internal static SocketPool BuildSocketPool(IPEndPoint destination, SocketSettings settings)
		{
			switch (settings.PoolType)
			{
				case SocketPoolType.Array:
					return new ArraySocketPool(destination, settings);
				case SocketPoolType.Null:
					return new NullSocketPool(destination, settings);
				case SocketPoolType.Linked:
					return new LinkedManagedSocketPool(destination, settings);
				default:
					return new ArraySocketPool(destination, settings);
			}
		}

		internal SocketPool GetSocketPool(IPEndPoint destination, SocketSettings settings)
		{
			Dictionary<IPEndPoint, SocketPool> pools = GetSocketPools(settings);
			SocketPool pool;
			if (!pools.TryGetValue(destination, out pool))
			{
				lock (pools)
				{
					if (!pools.TryGetValue(destination, out pool))
					{
						pool = BuildSocketPool(destination, settings);
						pools.Add(destination, pool);
					}
				}
			}

			return pool;
		}

		private SocketPool GetSocketPool(IPEndPoint destination)
		{
			return GetSocketPool(destination, _defaultSettings);
		}

		internal ManagedSocket GetSocket(IPEndPoint destination)
		{
			return GetSocketPool(destination).GetSocket();
		}

		internal ManagedSocket GetSocket(IPEndPoint destination, SocketSettings settings)
		{
			return GetSocketPool(destination, settings).GetSocket();

		}
	}
}
