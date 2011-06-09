using System;
using System.Runtime.InteropServices;
using System.IO;

namespace MySpace.DataRelay
{
	/// <summary>
	/// 	Responsible for hosting an <see cref="IRelayNode"/> instance and
	///     providing <see cref="Start"/>, <see cref="Stop"/> and <see cref="AssemblyChanged">Reload</see>
	///     services.
	/// </summary>
	/// <remarks>
	///     <para>The <see cref="RelayServer"/> provides a container for an instance of 
	///     <see cref="IRelayNode"/> that supports dynamic loading/reloading of the assembly containing
	///     the instance.  To use a different <see cref="IRelayNode"/> instance, replace the existing
	///     assembly with the new one.
	///     </para>
	/// </remarks>
	public class RelayServer
	{

		#region Fields

		private IRelayNode _relayNode = null;
		private string _instanceName = string.Empty;
		private LoadedAssemblyChangeDelegate _nodeChangedDelegate = null;
		private static readonly MySpace.Logging.LogWrapper _log = new MySpace.Logging.LogWrapper();
		private string _assemblyPath;

		#endregion

		#region extern

		[DllImport("kernel32", SetLastError = true)]
		static extern bool SetDllDirectory(string path);

		#endregion

		#region Ctor

		/// <summary>
		/// Initializes the <see cref="RelayServer"/>.
		/// </summary>
		public RelayServer()
			: this(null)
		{
		}

		/// <summary>
		/// Initializes the <see cref="RelayServer"/>.
		/// </summary>
		public RelayServer(string assemblyPath)
		{
			if (assemblyPath == null)
			{
				assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AssemblyLoader.AssemblyFolderName);
			}
			this._assemblyPath = assemblyPath;
			_nodeChangedDelegate = new LoadedAssemblyChangeDelegate(AssemblyChanged);
		}

		#endregion

		#region Start

		/// <summary>
		/// Starts the server and loads the <see cref="IRelayNode"/> instance's assembly.
		/// </summary>
		public void Start()
		{
			Start(null);
		}

		/// <summary>
		/// Starts the server and loads the <see cref="IRelayNode"/> instance's assembly.
		/// </summary>
		/// <param name="runStates">State information to start the instance with.</param>
		/// <exception cref="Exception">Thrown when an error occurs, caller should call <see cref="Stop"/> in this cass.</exception>
		public void Start(ComponentRunState[] runStates)
		{
			bool setDllDirectorySuccess = SetDllDirectory(_assemblyPath);

			if (setDllDirectorySuccess)
			{
				if (_log.IsInfoEnabled)
					_log.InfoFormat("Set DllDirectory to {0}. Unmanaged dlls will be imported from this folder.", _assemblyPath);
			}
			else
			{
				if (_log.IsErrorEnabled)
					_log.ErrorFormat("Failed to set DllDirectory to {0}. Components that rely on unmanaged DLLs will not work.", _assemblyPath);
			}

			if (_log.IsInfoEnabled) 
				_log.Info("Getting new node.");

			//enable this manually after the server is up an running because on server startup
			//code that modifies the directory will cause the domain to reload.
			AssemblyLoader.Instance.EnableRaisingEvents = false;
			_relayNode = AssemblyLoader.Instance.GetRelayNode(_nodeChangedDelegate);

			if (_relayNode != null)
			{
				if (_log.IsInfoEnabled)
				{
					_log.Info("New node created.");
					_log.Info("Initializing Relay Node Instance");
				}
				_relayNode.Initialize(runStates);

				if (_log.IsInfoEnabled)
					_log.Info("Relay Node Initialized, Starting");
				_relayNode.Start();
				if (_log.IsInfoEnabled)
					_log.Info("Relay Node Started");

				AssemblyLoader.Instance.EnableRaisingEvents = true;
			}
			else
			{
				if (_log.IsErrorEnabled)
					_log.Error("Error starting Relay Server: No Relay Node implemenation found!");
			}
		}

		#endregion

		#region Stop

		/// <summary>
		/// Stops the server and unloads the <see cref="IRelayNode"/> instance's assembly.
		/// </summary>
		/// <exception cref="Exception">Thrown when an error occurs.</exception>
		public void Stop()
		{
			if (_relayNode != null)
			{
				try
				{
					if (_log.IsInfoEnabled)
						_log.Info("Stopping Relay Node.");
					_relayNode.Stop();
					if (_log.IsInfoEnabled)
					{
						_log.Info("Relay Node Stopped.");
						_log.Info("Releasing old domain.");
					}
					AssemblyLoader.Instance.ReleaseRelayNode();
					if (_log.IsInfoEnabled)
						_log.Info("Old domain released.");
					_relayNode = null;
				}
				catch (Exception ex)
				{
					if (_log.IsErrorEnabled)
						_log.ErrorFormat("Error shutting down relay node: {0}", ex);
				}
			}
			else
			{
				if (_log.IsErrorEnabled)
					_log.Error("No Node To Stop.");
			}
		}

		/// <summary>
		/// Fires before handling a message or batch of messages.
		/// </summary>
		public event EventHandler BeforeMessagesHandled
		{
			add { _relayNode.BeforeMessagesHandled += value; }
			remove { _relayNode.BeforeMessagesHandled -= value; }
		}

		/// <summary>
		/// Fires after handling a message or batch of messages.
		/// </summary>
		public event EventHandler AfterMessagesHandled
		{
			add { _relayNode.AfterMessagesHandled += value; }
			remove { _relayNode.AfterMessagesHandled -= value; }
		}

		#endregion

		#region AssemblyChanged (Reload Assembly)

		private ComponentRunState[] GetRunState()
		{
			ComponentRunState[] runStates = null;
			if (_relayNode != null)
			{
				try
				{
					runStates = _relayNode.GetComponentRunStates();
				}
				catch (Exception ex)
				{
					if (_log.IsErrorEnabled)
						_log.ErrorFormat("Exception getting run states: {0}", ex);
					runStates = null;
				}
			}
			return runStates;
		}

		/// <summary>
		/// Stops the server, reloads the assembly and restarts the server.
		/// </summary>
		public void AssemblyChanged() //should rename to ReloadAssembly 
		{
			try
			{
				//preserve state information between assembly reloads
				ComponentRunState[] runStates = GetRunState();
				Stop();
				Start(runStates);
			}
			catch (Exception ex)
			{
				if (_log.IsErrorEnabled)
					_log.Error("Exception recycling Relay Node Domain: " + ex.ToString() + Environment.NewLine + "Trying again with no runstate.");
				_relayNode = AssemblyLoader.Instance.GetRelayNode(_nodeChangedDelegate);
				_relayNode.Initialize(null);
				_relayNode.Start();
			}
		}

		#endregion
	}
}
