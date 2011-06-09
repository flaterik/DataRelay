using System.Collections.Generic;
using System.Net;
using MySpace.ResourcePool;
using System;

namespace MySpace.SocketTransport
{
	internal class SocketManager
	{
		private static SocketManager _instance;
		private static readonly object _padlock = new object();
		private static readonly Logging.LogWrapper log = new Logging.LogWrapper();

		private readonly Dictionary<SocketSettings, Dictionary<IPEndPoint, SocketPool>> _allSocketPools;
		private SocketSettings _defaultSettings;
		private Dictionary<IPEndPoint, SocketPool> _defaultSocketPools;
		private readonly MemoryStreamPool _sharedBufferPool;

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

		private SocketManager()
		{
			_allSocketPools = new Dictionary<SocketSettings, Dictionary<IPEndPoint, SocketPool>>(2);
			
			_defaultSettings = SocketClient.GetDefaultSettings();
			_defaultSocketPools = new Dictionary<IPEndPoint, SocketPool>(50);
			_allSocketPools.Add(_defaultSettings, _defaultSocketPools);
			
			_sharedBufferPool = new MemoryStreamPool(_defaultSettings.InitialMessageSize, _defaultSettings.BufferReuses, SocketClient.config.SharedPoolMinimumItems);
		}

		internal void SetNewConfig(SocketClientConfig newConfig)
		{
			if (!newConfig.DefaultSocketSettings.SameAs(_defaultSettings))
			{
				if (log.IsInfoEnabled)
					log.Info("Default socket settings changed, updating default socket pool.");

				_defaultSocketPools = GetSocketPools(newConfig.DefaultSocketSettings);
				_defaultSettings = SocketClient.GetDefaultSettings();
			}
		}

		internal MemoryStreamPool SharedBufferPool
		{
			get
			{
				return _sharedBufferPool;
			}
		}

		internal Dictionary<IPEndPoint, SocketPool> DefaultSocketPools
		{
			get { return _defaultSocketPools; }
		}

		internal SocketPool GetSocketPool(
			IPEndPoint destination,
			SocketSettings settings,
			Dictionary<IPEndPoint, SocketPool> socketPools
			)
		{
			if (destination == null)
				throw new ArgumentNullException("destination");

			if (settings == null)
				settings = _defaultSettings;

			if (socketPools == null)
				socketPools = _defaultSocketPools;

			SocketPool pool;

			if (socketPools.ContainsKey(destination))
			{
				pool = socketPools[destination];
			}
			else
			{
				lock (socketPools)
				{
					if (!socketPools.TryGetValue(destination, out pool))
					{
						pool = BuildSocketPool(destination, settings);
						socketPools.Add(destination, pool);
					}
				}
			}

			return pool;
		}

		internal Dictionary<IPEndPoint, SocketPool> GetSocketPools(SocketSettings settings)
		{
			Dictionary<IPEndPoint, SocketPool> poolsForSettings;

			if (!_allSocketPools.TryGetValue(settings, out poolsForSettings))
			{
				lock (_allSocketPools)
				{
					if (!_allSocketPools.TryGetValue(settings, out poolsForSettings))
					{
						poolsForSettings = new Dictionary<IPEndPoint, SocketPool>(50);
						_allSocketPools.Add(settings, poolsForSettings);
						return poolsForSettings;
					}
				}
			}

			return poolsForSettings;
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

		internal SocketPool GetSocketPool(IPEndPoint destination)
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

		internal void GetSocketCounts(out int totalSockets, out int activeSockets)
		{
			lock (_allSocketPools)
			{
				totalSockets = 0;
				activeSockets = 0;
				foreach (Dictionary<IPEndPoint, SocketPool> pools in _allSocketPools.Values)
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

		
	}
}
