using System;
using System.Configuration;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Web.Configuration;
using System.Web;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace MySpace.Shared.Configuration
{
	/// <summary>
	/// ConfigutationSectionHandler that uses xml serialization to map config information to class defined in the type attribute of the config section
	/// </summary>
	public class XmlSerializerSectionHandler : IConfigurationSectionHandler
	{
		private static readonly MySpace.Logging.LogWrapper log = new MySpace.Logging.LogWrapper();
		public const int ReloadEventDelayMs = 5000;
		private static readonly Dictionary<string, object> configInstances = new Dictionary<string, object>();
		private static readonly List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
		private static readonly List<string> pendingConfigReloads = new List<string>();
		private static readonly object configLoadLock = new object();
		private static Dictionary<Type, List<EventHandler>> reloadDelegates = new Dictionary<Type, List<EventHandler>>();
		private static readonly object reloadDelegatesLock = new object();
		private static System.Threading.Timer reloadTimer;

		public object Create(object parent, object configContext, XmlNode section)
		{
			object retVal = GetConfigInstance(section);

			try
			{
				System.Configuration.Configuration config = null;

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

			try
			{
				XmlRootAttribute[] attributes = t.GetCustomAttributes(typeof(XmlRootAttribute), false) as XmlRootAttribute[];

				if (null == attributes || attributes.Length == 0)
				{
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
				/*
				 * This log statement was added by Jeremy Custenborder to flush out all of the XmlSerialization based config files. 
				 * XmlSerialized configs are a big part of our startup problem. This should not cause issues but if it does contact 
				 * Jeremy before removing. Again this should not cause issues unless the log file is created once per request or 
				 * something like that. If that is the case we have an issue anyway and this will hopefully flush it out under a StageDev1. 
				 * */



				XmlSerializer ser = null;
				watch = System.Diagnostics.Stopwatch.StartNew();
				if (configAndXmlAttributeMatch)
				{
					if (log.IsInfoEnabled) log.InfoFormat("Creating XmlSerializer for Type = \"{0}\" inferring namespace from Type", t.AssemblyQualifiedName);
					ser = new XmlSerializer(t);
				}
				else
				{
					if (log.IsInfoEnabled) log.InfoFormat("Creating XmlSerializer for Type = \"{0}\" with Namespace =\"{1}\"", t.AssemblyQualifiedName, nav.NamespaceURI);
					ser = new XmlSerializer(t, nav.NamespaceURI);
				}

				return ser.Deserialize(new XmlNodeReader(section));
			}
			catch (Exception ex)
			{
				if (ex.InnerException != null)
					throw ex.InnerException;
				throw ex;
			}
			finally
			{
				watch.Stop();
				if (log.IsInfoEnabled) log.InfoFormat("Took {0} to Create XmlSerializer and Deserialize for Type = \"{1}\"", watch.Elapsed, t.AssemblyQualifiedName);
			}
		}


		private static string GetConfigFilePath(System.Configuration.Configuration confFile, ConfigurationSection section)
		{
			string configSource = section.SectionInformation.ConfigSource;
			if (configSource == String.Empty)
			{
				return Path.GetFullPath(confFile.FilePath);
			}
			else
			{
				return Path.Combine(Path.GetDirectoryName(confFile.FilePath), configSource);
			}
		}

		private static void SetupWatcher(System.Configuration.Configuration config, ConfigurationSection configSection, object configInstance)
		{
			string filePath = GetConfigFilePath(config, configSection);
			string fileName = Path.GetFileName(filePath);

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
			XmlDocument doc = new XmlDocument();
			doc.Load(configFilePath);

			//refresh the section in case anyone else uses it
			ConfigurationManager.RefreshSection(doc.DocumentElement.Name);

			object newSettings = GetConfigInstance(doc.DocumentElement);
			object configInstance = configInstances[Path.GetFileName(configFilePath)];

			if (newSettings.GetType() != configInstance.GetType())
				return;
			Type newSettingsType = newSettings.GetType();
			PropertyInfo[] props = newSettingsType.GetProperties();
			foreach (PropertyInfo prop in props)
			{
				if (prop.CanRead && prop.CanWrite)
					prop.SetValue(configInstance, prop.GetValue(newSettings, null), null);
			}

			List<EventHandler> delegateMethods;

			if (reloadDelegates.TryGetValue(newSettingsType, out delegateMethods))
			{
				if (delegateMethods != null)
				{
					foreach (EventHandler delegateMethod in delegateMethods)
					{
						delegateMethod(newSettings, EventArgs.Empty);
					}
				}
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
			lock (reloadDelegatesLock)
			{
				// We have to re-build everything w/ new collections
				// because other code in this class reads
				// reloadDelegates and the EventHandler lists
				// contained by reloadDelegates without
				// aquiring reloadDelegatesLock

				var newReloadDelegates = new Dictionary<Type, List<EventHandler>>();
				bool added = false;

				foreach (var pair in reloadDelegates)
				{
					var newHandlers = new List<EventHandler>(pair.Value);
					newReloadDelegates.Add(pair.Key, newHandlers);
					if (pair.Key == type)
					{
						newHandlers.Add(delegateMethod);
						added = true;
					}
				}

				if (!added)
				{
					newReloadDelegates.Add(type, new List<EventHandler> { delegateMethod });
				}
				Thread.MemoryBarrier();
				reloadDelegates = newReloadDelegates;
			}
		}
	}

}
