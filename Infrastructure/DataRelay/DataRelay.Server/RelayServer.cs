using System;
using System.Collections.Generic;
using System.Text;
using MySpace.DataRelay;
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

		private IRelayNode relayNode = null;
		private string instanceName = string.Empty;
		private LoadedAssemblyChangeDelegate nodeChangedDelegate = null;
		private static readonly MySpace.Logging.LogWrapper log = new MySpace.Logging.LogWrapper();
		private string assemblyPath;

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
			this.assemblyPath = assemblyPath;
			nodeChangedDelegate = new LoadedAssemblyChangeDelegate(AssemblyChanged);
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
			bool setDllDirectorySuccess = SetDllDirectory(assemblyPath);

			if (setDllDirectorySuccess)
			{
				if (log.IsInfoEnabled)
					log.InfoFormat("Set DllDirectory to {0}. Unmanaged dlls will be imported from this folder.", assemblyPath);
			}
			else
			{
				if (log.IsErrorEnabled)
					log.ErrorFormat("Failed to set DllDirectory to {0}. Components that rely on unmanaged DLLs will not work.", assemblyPath);
			}

			if (log.IsInfoEnabled) 
				log.Info("Getting new node.");

			//enable this manually after the server is up an running because on server startup
			//code that modifies the directory will cause the domain to reload.
			AssemblyLoader.Instance.EnableRaisingEvents = false;
			relayNode = AssemblyLoader.Instance.GetRelayNode(nodeChangedDelegate);

			if (relayNode != null)
			{
				if (log.IsInfoEnabled)
				{
					log.Info("New node created.");
					log.Info("Initializing Relay Node Instance");
				}
				relayNode.Initialize(runStates);

				if (log.IsInfoEnabled)
					log.Info("Relay Node Initialized, Starting");
				relayNode.Start();
				if (log.IsInfoEnabled)
					log.Info("Relay Node Started");

				AssemblyLoader.Instance.EnableRaisingEvents = true;
			}
			else
			{
				if (log.IsErrorEnabled)
					log.Error("Error starting Relay Server: No Relay Node implemenation found!");
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
			if (relayNode != null)
			{
				try
				{
					if (log.IsInfoEnabled)
						log.Info("Stopping Relay Node.");
					relayNode.Stop();
					if (log.IsInfoEnabled)
					{
						log.Info("Relay Node Stopped.");
						log.Info("Releasing old domain.");
					}
					AssemblyLoader.Instance.ReleaseRelayNode();
					if (log.IsInfoEnabled)
						log.Info("Old domain released.");
					relayNode = null;
				}
				catch (Exception ex)
				{
					if (log.IsErrorEnabled)
						log.ErrorFormat("Error shutting down relay node: {0}", ex);
				}
			}
			else
			{
				if (log.IsErrorEnabled)
					log.Error("No Node To Stop.");
			}
		}

		#endregion

		#region AssemblyChanged (Reload Assembly)

		private ComponentRunState[] GetRunState()
		{
			ComponentRunState[] runStates = null;
			if (relayNode != null)
			{
				try
				{
					runStates = relayNode.GetComponentRunStates();
				}
				catch (Exception ex)
				{
					if (log.IsErrorEnabled)
						log.ErrorFormat("Exception getting run states: {0}", ex);
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
				if (log.IsErrorEnabled)
					log.Error("Exception recycling Relay Node Domain: " + ex.ToString() + Environment.NewLine + "Trying again with no runstate.");
				relayNode = AssemblyLoader.Instance.GetRelayNode(nodeChangedDelegate);
				relayNode.Initialize(null);
				relayNode.Start();
			}
		}

		#endregion
	}
}
