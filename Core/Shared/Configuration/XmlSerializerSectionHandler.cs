using System;
using System.Configuration;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Web.Configuration;
using System.Web;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using MySpace.Common.HelperObjects;

namespace MySpace.Shared.Configuration
{
	/// <summary>
	/// ConfigutationSectionHandler that uses xml serialization to map config information to class defined in the type attribute of the config section
	/// </summary>
	public class XmlSerializerSectionHandler : IConfigurationSectionHandler
	{
		private static readonly Logging.LogWrapper log = new Logging.LogWrapper();
		public const int ReloadEventDelayMs = 5000;
		private static readonly Dictionary<string, object> configInstances = new Dictionary<string, object>();
		private static readonly List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
		private static readonly List<string> pendingConfigReloads = new List<string>();
		private static readonly object configLoadLock = new object();
		private static readonly Dictionary<Type, List<EventHandler>> reloadDelegates = new Dictionary<Type, List<EventHandler>>();

		private static readonly MsReaderWriterLock reloadDelegatesLock =
			new MsReaderWriterLock(System.Threading.LockRecursionPolicy.NoRecursion);
		private static Timer reloadTimer;

		public object Create(object parent, object configContext, XmlNode section)
		{
			object retVal = GetConfigInstance(section);

			try
			{
				System.Configuration.Configuration config;

				//if the app is hosted you should be able to load a web.config.
				if (System.Web.Hosting.HostingEnvironment.IsHosted)
					config = WebConfigurationManager.OpenWebConfiguration(HttpRuntime.AppDomainAppVirtualPath);
				else
					config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
				//TODO: figure out how to get Configuration in a service

				//SectionInformation info = config.GetSection(section.Name).SectionInformation;
				ConfigurationSection configSection = config.GetSection(section.Name);
				if (configSection.SectionInformation.RestartOnExternalChanges == false)
					SetupWatcher(config, configSection, retVal);
			}
			//if an exception occurs here we simply have no watcher and the app pool must be reset in order to recognize config changes
			catch (Exception exc)
			{
				string Message = "Exception setting up FileSystemWatcher for Section = " + (section != null ? section.Name : "Unknown Section");
				if (log.IsErrorEnabled) log.Error(Message, exc);
			}
			return retVal;
		}

		private static object GetConfigInstance(XmlNode section)
		{
			XPathNavigator nav = section.CreateNavigator();
			string typeName = (string)nav.Evaluate("string(@type)");

			if (string.IsNullOrEmpty(typeName))
				throw new ConfigurationErrorsException(
@"Configuration file is missing a type attribute at the root of the document element.
Example: <ConfigurationFile type=""MySpace.Configuration.ConfigurationFile, MySpace.Configuration"">");


			Type t = Type.GetType(typeName);

			if (t == null)
				throw new ConfigurationErrorsException("XmlSerializerSectionHandler failed to create an instance of type '" + typeName +
					"'.  Please ensure this is a valid type string.", section);

			bool configAndXmlAttributeMatch = false;
			bool useDataContractSerialization = false;
			try
			{
				XmlRootAttribute[] attributes = t.GetCustomAttributes(typeof(XmlRootAttribute), false) as XmlRootAttribute[];

				if (null == attributes || attributes.Length == 0)
				{
					useDataContractSerialization = t.GetCustomAttributes(typeof(DataContractAttribute), false)
						.OfType<DataContractAttribute>()
						.Take(1)
						.Count() > 0;

					if (log.IsErrorEnabled) log.ErrorFormat(
@"Type ""{0}"" does not have an XmlRootAttribute applied.
Please declare an XmlRootAttribute with the proper namespace ""{1}""
Please look at http://mywiki.corp.myspace.com/index.php/XmlSerializerSectionHandler_ProperUse", t.AssemblyQualifiedName, nav.NamespaceURI);
				}
				else
				{
					XmlRootAttribute attribute = attributes[0];

					//Only check for namespace compiance if both the config and the attribute have something for their namespace.
					if (!string.IsNullOrEmpty(attribute.Namespace) && !string.IsNullOrEmpty(nav.NamespaceURI))
					{
						if (!string.Equals(nav.NamespaceURI, attribute.Namespace, StringComparison.OrdinalIgnoreCase))
						{
							if (log.IsErrorEnabled) log.ErrorFormat(
	@"Type ""{0}"" has an XmlRootAttribute declaration with an incorrect namespace.
The XmlRootAttribute specifies ""{1}"" for the namespace but the config uses ""{2}""
Please declare an XmlRootAttribute with the proper namespace ""{2}""
Please look at http://mywiki.corp.myspace.com/index.php/XmlSerializerSectionHandler_ProperUse", t.AssemblyQualifiedName, attribute.Namespace, nav.NamespaceURI);
						}
						else
							configAndXmlAttributeMatch = true;
					}
					else
						configAndXmlAttributeMatch = true;
				}
			}
			catch (Exception ex)
			{

				if (log.IsWarnEnabled)
				{
					log.WarnFormat("Exception thrown checking XmlRootAttribute's for \"{0}\". Config will still load normally...", t.AssemblyQualifiedName);
					log.Warn("Exception thrown checking XmlRootAttribute's", ex);
				}
			}

			System.Diagnostics.Stopwatch watch = null;
			try
			{
				if(log.IsDebugEnabled)
					watch = System.Diagnostics.Stopwatch.StartNew();
				
				if (useDataContractSerialization)
				{
					if (log.IsDebugEnabled) log.DebugFormat("Creating DataContractSerializer for Type = \"{0}\" inferring namespace from Type", t.AssemblyQualifiedName);
					return new DataContractSerializer(t).ReadObject(new XmlNodeReader(section));
				}
				else
				{
					XmlSerializer ser;
					if (configAndXmlAttributeMatch)
					{
						if (log.IsDebugEnabled) log.DebugFormat("Creating XmlSerializer for Type = \"{0}\" inferring namespace from Type", t.AssemblyQualifiedName);
						ser = new XmlSerializer(t);
					}
					else
					{
						if (log.IsDebugEnabled) log.DebugFormat("Creating XmlSerializer for Type = \"{0}\" with Namespace =\"{1}\"", t.AssemblyQualifiedName, nav.NamespaceURI);
						ser = new XmlSerializer(t, nav.NamespaceURI);
					}

					return ser.Deserialize(new XmlNodeReader(section));
				}
			}
			catch (Exception ex)
			{
				if (ex.InnerException != null)
					throw ex.InnerException;
				throw;
			}
			finally
			{
				if (log.IsDebugEnabled && watch != null)
				{
					watch.Stop();
					log.DebugFormat("Took {0} to Create and Deserialize Type = \"{1}\"", watch.Elapsed, t.AssemblyQualifiedName);
				}
			}
		}


		private static string GetConfigFilePath(System.Configuration.Configuration confFile, ConfigurationSection section)
		{
			string configSource = section.SectionInformation.ConfigSource;
			if (configSource == String.Empty)
			{
				return Path.GetFullPath(confFile.FilePath);
			}
			string directoryName = Path.GetDirectoryName(confFile.FilePath);
			if (directoryName == null)
			{
				log.ErrorFormat("Could not get directory name from config file path {0}", confFile.FilePath);
				return null;
			}
			return Path.Combine(directoryName, configSource);
			
		}

		private static void SetupWatcher(System.Configuration.Configuration config, ConfigurationSection configSection, object configInstance)
		{
			string filePath = GetConfigFilePath(config, configSection);
			string fileName = Path.GetFileName(filePath);
			if (fileName == null)
			{
				log.ErrorFormat("Could not get file name from config file path {0}", filePath);
				return;
			}
			if (configInstances.ContainsKey(fileName))
				return;

			FileSystemWatcher scareCrow = new FileSystemWatcher();
			scareCrow.Path = Path.GetDirectoryName(filePath);
			scareCrow.EnableRaisingEvents = true;
			scareCrow.IncludeSubdirectories = false;
			scareCrow.Filter = fileName;
			scareCrow.NotifyFilter = NotifyFilters.Attributes | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size;

			scareCrow.Changed += scareCrow_Changed;
			scareCrow.Created += scareCrow_Changed;
			scareCrow.Deleted += scareCrow_Changed;
			scareCrow.Renamed += scareCrow_Changed;
			watchers.Add(scareCrow);
			configInstances.Add(fileName, configInstance);
		}


		private static void scareCrow_Changed(object sender, FileSystemEventArgs e)
		{
			lock (configLoadLock)
			{
				if (pendingConfigReloads.Contains(e.Name) || configInstances.ContainsKey(e.Name) == false)
					return;

				pendingConfigReloads.Add(e.Name);
			}

			reloadTimer = new Timer(DelayedProcessConfigChange, e, ReloadEventDelayMs, Timeout.Infinite);
		}

		private static void DelayedProcessConfigChange(object ar)
		{
			FileSystemEventArgs e = (FileSystemEventArgs)ar;

			lock (configLoadLock)
			{
				pendingConfigReloads.Remove(e.Name);
			}

			ReloadConfig(e.FullPath);
		}

		internal static void ReloadConfig(string configFilePath)
		{
			if (string.IsNullOrEmpty(configFilePath))
			{
				log.Error("Attempted to reload null or empty config file path.");
				return;
			}

			try
			{
				XmlDocument doc = new XmlDocument();
				doc.Load(configFilePath);

				if (doc.DocumentElement == null)
				{
					log.ErrorFormat("Got a null document element when reloading config file with path {0}", configFilePath);
					return;
				}

				//refresh the section in case anyone else uses it
				ConfigurationManager.RefreshSection(doc.DocumentElement.Name);

				object newSettings = GetConfigInstance(doc.DocumentElement);
				string fileName = Path.GetFileName(configFilePath);
				if (fileName == null)
				{
					log.ErrorFormat("Got null file name for config file path {0}", configFilePath);
					return;
				}
				object configInstance = configInstances[fileName];

				if (newSettings.GetType() != configInstance.GetType())
					return;
				Type newSettingsType = newSettings.GetType();
				PropertyInfo[] props = newSettingsType.GetProperties();
				foreach (PropertyInfo prop in props)
				{
					if (prop.CanRead && prop.CanWrite)
						prop.SetValue(configInstance, prop.GetValue(newSettings, null), null);
				}

				reloadDelegatesLock.Read(() =>
				                         	{
				                         		List<EventHandler> delegateMethods;
				                         		if (reloadDelegates.TryGetValue(newSettingsType, out delegateMethods)
				                         		    && delegateMethods != null)
				                         		{
				                         			foreach (EventHandler delegateMethod in delegateMethods)
				                         			{
				                         				delegateMethod(newSettings, EventArgs.Empty);
				                         			}
				                         		}
				                         	});
			}
			catch (Exception e)
			{
				log.ErrorFormat("Exception reloading config with path {0}: {1}", configFilePath, e);
			}
		}

		/// <summary>
		/// Method is used to register for notifications when a particular type has
		/// been reloaded. 
		/// </summary>
		/// <param name="type">Type to monitor for.</param>
		/// <param name="delegateMethod">Delegate method to call.</param>
		public static void RegisterReloadNotification(Type type, EventHandler delegateMethod)
		{
			reloadDelegatesLock.Write(() =>
			{
				List<EventHandler> eventHandlerList;
				if (reloadDelegates.TryGetValue(type, out eventHandlerList) && 
					eventHandlerList != null)
				{
					if (!eventHandlerList.Contains(delegateMethod))
					{
						eventHandlerList.Add(delegateMethod);
					}
				}
				else
				{
					reloadDelegates.Add(type, new List<EventHandler> { delegateMethod });
				}
			});
		}
		/// <summary>
		/// If you have registered for an event, you should call this to unregister when your 
		/// class is ready to be disposed, because otherwise a reference will be held to it by the
		/// delegate and it will never be fully garbage collected.
		/// </summary>
		/// <param name="type">Type used to register for notification</param>
		/// <param name="delegateMethod">Delegate that was registered</param>
		public static void UnregisterReloadNotification(Type type, EventHandler delegateMethod)
		{
			reloadDelegatesLock.Write(() =>
			{
				List<EventHandler> eventHandlerList;
				if (reloadDelegates.TryGetValue(type, out eventHandlerList) && 
					eventHandlerList != null)
				{
					eventHandlerList.Remove(delegateMethod);
				}
			});
		}
	}

}
