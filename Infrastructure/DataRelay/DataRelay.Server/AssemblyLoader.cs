using System;
using System.Configuration;
using System.Reflection;
using System.IO;
using System.Threading;
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
		private static readonly MySpace.Logging.LogWrapper log = new MySpace.Logging.LogWrapper();

		private FileSystemWatcher watcher;
		private string appPath;
		private string assemblyPath;
		private string shadowCacheFolder;
		private object resourceLock = new object();
		private Set<string> pendingAssemblyReloadMinute = new Set<string>(StringComparer.OrdinalIgnoreCase);
		private Set<string> pendingAssemblyFileNames = new Set<string>(StringComparer.OrdinalIgnoreCase);
		private static AssemblyLoader instance;
		private static readonly object padlock = new object();
		private AppDomain nodeDomain;
		private readonly object nodeLock = new object();
		private string nodeFileName;
		private LoadedAssemblyChangeDelegate nodeChanged;
		private static bool reloadOnAssemblyChanges = true;
		private static System.Threading.Timer _reloadTimer;
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
						reloadOnAssemblyChanges = reload;
					}
					else
					{
						log.WarnFormat("Invalid appSetting value for key 'ReloadOnFileChanges': {0}", value ?? "null");
					}
				}
			}
			catch (Exception ex)
			{
				log.Error("Couldn't access app settings", ex);
			}
		}


		/// <summary>
		/// Private Ctor for Singleton class.
		/// </summary>
		private AssemblyLoader()
		{
			appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			shadowCacheFolder = Path.Combine(appPath, "ShadowCopy");
			
			if (!Directory.Exists(shadowCacheFolder))
			{
				Directory.CreateDirectory(shadowCacheFolder);
			}
			
			assemblyPath = Path.Combine(appPath, AssemblyFolderName);
			
			if (!Directory.Exists(assemblyPath))
			{
				Directory.CreateDirectory(assemblyPath);
			}
		
			watcher = new FileSystemWatcher(assemblyPath);
			watcher.Changed += new FileSystemEventHandler(AssemblyDirChanged);
			watcher.Created += new FileSystemEventHandler(AssemblyDirChanged);
			watcher.Deleted += new FileSystemEventHandler(AssemblyDirChanged);
			watcher.EnableRaisingEvents = true; 
		}

		#endregion

		#region Private Methods

		/// <summary>
		/// Handles the case when the Assembly Directory's contents changes.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void AssemblyDirChanged(object sender, FileSystemEventArgs e)
		{
			if (!reloadOnAssemblyChanges)
			{
				if(log.IsDebugEnabled)
					log.DebugFormat("File {0} changed but reloads are disabled.", e.Name);
				return;
			}
			
			if (!FileCausesRestart(e.Name))
			{
				if(log.IsDebugEnabled)
					log.DebugFormat("Ignored file {0} changed", e.Name);
				return;
			}
			
			string thisMinute = System.DateTime.Now.Hour.ToString() + ":" + System.DateTime.Now.Minute.ToString();

			lock (resourceLock)
			{
				//checks to see if a reload was already scheduled keyed by hh:mm, if so
				//checks to see if it contained the specific file 
				if (pendingAssemblyReloadMinute.Contains(thisMinute))
				{
					if (!pendingAssemblyFileNames.Contains(e.Name) && FileCausesRestart(e.Name))
					{
						pendingAssemblyFileNames.Add(e.Name);
						if (log.IsInfoEnabled)
							log.InfoFormat("Got change for {0}. Processing with other changes made during {1}",
										   e.Name, thisMinute);
					}
					return;
				}


				//store this reload to ensure we only reload once for multiple files being changed.

				if (log.IsInfoEnabled)
					log.InfoFormat("Got change for {0} during {1}. Processing in five seconds.", e.Name,
								   thisMinute);
				pendingAssemblyReloadMinute.Add(thisMinute);
				pendingAssemblyFileNames.Add(e.Name);

			}

			//setup a timer to defer the reload to ensure all files in the directory have changed
			//this would allow time for files being copied to complete
			//assigning to a static variable because you need to keep a reference to timers to keep them from being GC'd and breaking
			_reloadTimer = new Timer(ProcessAssemblyChange, thisMinute, 5000, Timeout.Infinite); 
		}

		private static bool FileCausesRestart(string fileName)
		{
			return (fileName.EndsWith(".dll") && !fileName.Contains("XmlSerializers"));
		}


		/// <summary>
		/// Handles the <see cref="AssemblyDirChanged"/> event, to signal for the assembly change.
		/// </summary>
		/// <param name="ar"></param>
		private void ProcessAssemblyChange(object ar)
		{
			string thisMinute = (string)ar;

			lock (resourceLock)
			{
				pendingAssemblyReloadMinute.Remove(thisMinute);
				pendingAssemblyFileNames.Clear();
				if (nodeChanged != null)
				{
					try
					{
						nodeChanged();
					}
					catch (Exception ex)
					{
						if (log.IsErrorEnabled)
							log.ErrorFormat("Error Changing Node Assembly: {0}", ex);
					}
				}
			}
		}

		/// <summary>
		/// Ensures the AppDomain has been loaded.
		/// </summary>
		private void EnsureDomainIsLoaded()
		{
			if (nodeDomain == null) //double checked locking pattern
			{
				lock (nodeLock)
				{
					if (nodeDomain == null)
					{
						AppDomainSetup ads = new AppDomainSetup();
						ads.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;
						ads.CachePath = shadowCacheFolder;
						ads.ShadowCopyFiles = "true";
						ads.ConfigurationFile = @"ConfigurationFiles\RelayNode.app.config";
						ads.PrivateBinPath = AssemblyFolderName;
						nodeDomain = AppDomain.CreateDomain("RelayNode", null, ads);
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
				if (instance == null)
				{
					lock (padlock)
					{
						if (instance == null)
						{
							instance = new AssemblyLoader();
						}
					}
				}
				return instance;
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
				Factory nodeFactory = (Factory)nodeDomain.CreateInstanceFromAndUnwrap(
					"MySpace.DataRelay.NodeFactory.dll", "MySpace.DataRelay.Factory"
					);								
				nodeChanged = changedDelegate;
				if (log.IsInfoEnabled)
					log.Info("Loaded relay node domain.");
				return (IRelayNode)nodeFactory.LoadClass("MySpace.DataRelay.RelayNode", "MySpace.DataRelay.RelayNode", out nodeFileName);
			}
			catch (Exception ex)
			{
				if (log.IsErrorEnabled)
					log.ErrorFormat("Error loading relay node: {0}", ex);
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
			get { return watcher.EnableRaisingEvents; }
			set { watcher.EnableRaisingEvents = value; }
		}

		#region ReleaseRelayNode

		/// <summary>
		/// Unloads the <see cref="AppDomain"/> that the <see cref="IRelayNode"/> instance
		/// was loaded into.
		/// </summary>
		internal void ReleaseRelayNode()
		{
			if (nodeDomain != null)
			{
				AppDomain.Unload(nodeDomain);
				nodeDomain = null;
				if (log.IsInfoEnabled)
					log.Info("Unloaded relay node domain.");
			}
		}

		#endregion
	}
}
