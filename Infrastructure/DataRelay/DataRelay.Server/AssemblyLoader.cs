using System;
using System.Configuration;
using System.Reflection;
using System.IO;
using System.Threading;
using MySpace.Logging;
using Wintellect.PowerCollections;

namespace MySpace.DataRelay
{
	internal delegate void LoadedAssemblyChangeDelegate();

	/// <summary>
	/// 	<para>Provides services to load and unload an instance of 
	///     <see cref="IRelayNode"/> into a seperate application domain.</para>
	/// </summary>
	/// <remarks>
	/// <para>When <see cref="GetRelayNode"/> is called, a new AppDomain is created where
	/// the <see cref="IRelayNode"/> is referenced.</para>
	/// </remarks>
	internal class AssemblyLoader
	{
		#region Fields

		/// <summary>
		/// The name of the folder where the assemblies are loaded from.
		/// </summary>
		internal readonly static string AssemblyFolderName = "RelayAssemblies";
		private static readonly LogWrapper _log = new LogWrapper();

		private FileSystemWatcher _watcher;
		private string _appPath;
		private string _assemblyPath;
		private string _shadowCacheFolder;
		private object _resourceLock = new object();
		private static AssemblyLoader _instance;
		private static readonly object _padlock = new object();
		private AppDomain _nodeDomain;
		private readonly object _nodeLock = new object();
        private Set<string> _pendingAssemblyFileNames = new Set<string>(StringComparer.OrdinalIgnoreCase);
		private string _nodeFileName;
		private LoadedAssemblyChangeDelegate _nodeChanged;
		private static bool _reloadOnAssemblyChanges = true;
		private static Timer _reloadTimer;
	    private static DateTime? _currentFileSetChangeTime;
	    private static int _reloadWindowSeconds = 10; //not const to allow test to change

		#endregion

		#region Ctor

		static AssemblyLoader()
		{
			try
			{
				string value = ConfigurationManager.AppSettings["ReloadOnFileChanges"];
				if (value != null)
				{
					bool reload;
					if (bool.TryParse(value, out reload))
					{
						_reloadOnAssemblyChanges = reload;
					}
					else
					{
						_log.WarnFormat("Invalid appSetting value for key 'ReloadOnFileChanges': {0}", value ?? "null");
					}
				}
			}
			catch (Exception ex)
			{
				_log.Error("Couldn't access app settings", ex);
			}
		}


		/// <summary>
		/// Private Ctor for Singleton class.
		/// </summary>
		private AssemblyLoader()
		{
			_appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			_shadowCacheFolder = Path.Combine(_appPath, "ShadowCopy");
			
			if (!Directory.Exists(_shadowCacheFolder))
			{
				Directory.CreateDirectory(_shadowCacheFolder);
			}
			
			_assemblyPath = Path.Combine(_appPath, AssemblyFolderName);
			
			if (!Directory.Exists(_assemblyPath))
			{
				Directory.CreateDirectory(_assemblyPath);
			}
		
			_watcher = new FileSystemWatcher(_assemblyPath);
			_watcher.Changed += new FileSystemEventHandler(AssemblyDirChanged);
			_watcher.Created += new FileSystemEventHandler(AssemblyDirChanged);
			_watcher.Deleted += new FileSystemEventHandler(AssemblyDirChanged);
			_watcher.EnableRaisingEvents = true;

		    int reloadMs = _reloadWindowSeconds * 1000;
             
            //setup a timer to defer the reload to ensure all files in the directory have changed
            //this would allow time for files being copied to complete
            //assigning to a static variable because you need to keep a reference to timers to keep them from being GC'd and breaking
            _reloadTimer = new Timer(CheckForReload, null, reloadMs, reloadMs); 
		}

		#endregion

		#region Private Methods

        /// <summary>
        /// Determines if based on when files were last changed if we should execute our Assembly Changed logic. 
        /// </summary>
        /// <param name="notUsed"></param>
        private void CheckForReload(object notUsed)
        {
            DateTime? lastChange;
            lock (_resourceLock)
            {
                lastChange = _currentFileSetChangeTime;
            }

            DateTime now = DateTime.UtcNow; //the cost of this should be low, because this method is called infrequently
            int elapsed = (int)(now - lastChange.GetValueOrDefault(now)).TotalSeconds;

            if (elapsed >= _reloadWindowSeconds)
            {
                ProcessAssemblyChange();
            }
        }

		/// <summary>
		/// Handles the case when the Assembly Directory's contents changes.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void AssemblyDirChanged(object sender, FileSystemEventArgs e)
		{
			if (!_reloadOnAssemblyChanges)
			{
				if(_log.IsDebugEnabled)
					_log.DebugFormat("File {0} changed but reloads are disabled.", e.Name);
				return;
			}
			
			if (!FileCausesRestart(e.Name))
			{
				if(_log.IsDebugEnabled)
					_log.DebugFormat("Ignored file {0} changed", e.Name);
				return;
			}
			
			string thisMinute = DateTime.Now.Hour.ToString() + ":" + DateTime.Now.Minute.ToString();

			lock (_resourceLock)
			{
                if (_log.IsInfoEnabled)
                {
                    if (_pendingAssemblyFileNames.Add(e.Name) == false) //returns false if the item didn't already exist
                    {
                        _log.InfoFormat(
                            "Got change for {0} during {1}. Processing {2} seconds after the last file change hasn't occured for {2} seconds",
                            e.Name,
                            thisMinute,
                            _reloadWindowSeconds);
                    }
                }
			    _currentFileSetChangeTime = DateTime.UtcNow;
			}
		}

		private static bool FileCausesRestart(string fileName)
		{
			return (fileName.EndsWith(".dll") && !fileName.Contains("XmlSerializers"));
		}


		/// <summary>
		/// Handles the <see cref="AssemblyDirChanged"/> event, to signal for the assembly change.
		/// </summary>
		private void ProcessAssemblyChange()
		{
			lock (_resourceLock)
			{
                _currentFileSetChangeTime = null;
                _pendingAssemblyFileNames.Clear();
				if (_nodeChanged != null)
				{
					try
					{
						_nodeChanged();
					}
					catch (Exception ex)
					{
						if (_log.IsErrorEnabled)
							_log.ErrorFormat("Error Changing Node Assembly: {0}", ex);
					}
				}
			}
		}

		/// <summary>
		/// Ensures the AppDomain has been loaded.
		/// </summary>
		private void EnsureDomainIsLoaded()
		{
			if (_nodeDomain == null) //double checked locking pattern
			{
				lock (_nodeLock)
				{
					if (_nodeDomain == null)
					{
						AppDomainSetup ads = new AppDomainSetup();
						ads.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;
						ads.CachePath = _shadowCacheFolder;
						ads.ShadowCopyFiles = "true";
						ads.ConfigurationFile = @"ConfigurationFiles\RelayNode.app.config";
						ads.PrivateBinPath = AssemblyFolderName;
						_nodeDomain = AppDomain.CreateDomain("RelayNode", null, ads);
					}
				}
			}
		}

		#endregion

		#region Instance (Singleton)

		/// <summary>
		/// Gets the Singleton instance of this class.
		/// </summary>
		internal static AssemblyLoader Instance
		{
			get
			{
				if (_instance == null)
				{
					lock (_padlock)
					{
						if (_instance == null)
						{
							_instance = new AssemblyLoader();
						}
					}
				}
				return _instance;
			}
		}

		#endregion

		#region GetRelayNode

		/// <summary>
		/// Loads an implementation of <see cref="IRelayNode"/> into a new <see cref="AppDomain"/>.
		/// </summary>
		/// <param name="changedDelegate">The delegate that is called when the assembly is changed.</param>
		/// <returns>Returns an instance of an implementation of <see cref="IRelayNode"/></returns>
		internal IRelayNode GetRelayNode(LoadedAssemblyChangeDelegate changedDelegate)
		{
			EnsureDomainIsLoaded();

			try
			{
				Factory nodeFactory = (Factory)_nodeDomain.CreateInstanceFromAndUnwrap(
					"MySpace.DataRelay.NodeFactory.dll", "MySpace.DataRelay.Factory"
					);
                _nodeChanged = changedDelegate;
				if (_log.IsInfoEnabled)
					_log.Info("Loaded relay node domain.");
				return (IRelayNode)nodeFactory.LoadClass("MySpace.DataRelay.RelayNode", "MySpace.DataRelay.RelayNode", out _nodeFileName);
			}
			catch (Exception ex)
			{
				if (_log.IsErrorEnabled)
					_log.ErrorFormat("Error loading relay node: {0}", ex);
				return null;
			}
		}

	    #endregion

		/// <summary>
		/// Gets or set a value that indicates if events are raised, most
		/// notably the directory changed event to reload the assembly.
		/// </summary>
		internal bool EnableRaisingEvents
		{
			get { return _watcher.EnableRaisingEvents; }
			set { _watcher.EnableRaisingEvents = value; }
		}

		#region ReleaseRelayNode

		/// <summary>
		/// Unloads the <see cref="AppDomain"/> that the <see cref="IRelayNode"/> instance
		/// was loaded into.
		/// </summary>
		internal void ReleaseRelayNode()
		{
			if (_nodeDomain != null)
			{
				AppDomain.Unload(_nodeDomain);
				_nodeDomain = null;
				if (_log.IsInfoEnabled)
					_log.Info("Unloaded relay node domain.");
			}
		}

		#endregion
	}
}
