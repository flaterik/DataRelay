using System;
using System.Configuration;
using System.IO;
using MySpace.Logging;

namespace MySpace.Configuration
{
	/// <summary>
	///		<para>The base class for all configuration sections.  This base class
	///		provides built-in capabilities such as providing a <see cref="GetSection"/>
	///		method and the ability to auto-refresh the section upon file change.</para>
	/// </summary>
	/// <typeparam name="Implementation">
	///		<para>The non-abstract descendent of this class.</para>
	///		<para>This class must set the <see cref="_getSectionName"/> in a static constructor.</para>
	/// </typeparam>
	public abstract class ConfigurationSectionBase<Implementation> : ConfigurationSection
		where Implementation : ConfigurationSectionBase<Implementation>
	{
		//Because this class is generic, the static members below apply to the specific
		//type of generic so ConfigurationSectionBase<Dog> will have different static
		//members than ConfigurationSectionBase<Cat> 
		private static readonly LogWrapper _log = new LogWrapper();
		private static readonly object _syncRoot = new object();
		private static string _sectionName = null;
		private static FileSystemWatcher _fileWatcher;
		private static volatile bool _watcherInitialized = false;
		private static Implementation _section = null;

		/// <summary>
		/// 	<para>Initializes an instance of the <see cref="ConfigurationSectionBase{Implementation}"/> class.</para>
		/// </summary>
		protected ConfigurationSectionBase()
		{
		}

		private static string _getSectionName()
		{
			if (_sectionName == null)
			{
				object[] attributes =
					typeof(Implementation).GetCustomAttributes(typeof(ConfigurationSectionNameAttribute), false);
				if (attributes != null && attributes.Length > 0)
				{
					ConfigurationSectionNameAttribute attribute = (ConfigurationSectionNameAttribute)attributes[0];
					_sectionName = attribute.Name;
				}
			}
			return _sectionName;
		}

		private static void _loadSection(bool throwOnSectionNotFound)
		{
			try
			{
				_section = (Implementation)ConfigurationManager.GetSection(_getSectionName());
			}
			catch (Exception x)
			{
				if (throwOnSectionNotFound)
				{
					throw new ApplicationException(
						String.Format("Exception encountered when opening configuration section {0}.", _getSectionName()), x);
				}
			}

			if (_section == null)
			{
				if (throwOnSectionNotFound)
				{
					throw new ApplicationException(
						String.Format("Configuration Section {0} is expected but not found.", _getSectionName()));
				}
			}
		}

		/// <summary>
		/// 	<para>Loads the <typeparamref name="Implementation"/> from the
		/// 	app config file.</para>
		/// </summary>
		/// <param name="throwOnSectionNotFound">
		/// 	<para><see langword="true"/> to throw an exception rather than 
		///		returning <see langword="null"/> when a valid configuration section
		///		is not found; <see langword="false"/> to return <see langword="null"/>
		///		when the section is not found.</para>
		/// </param>
		/// <returns>
		/// 	<para>The <typeparamref name="Implementation"/> read from the app config file;
		/// 	<see langword="null"/> if no such config section is found.</para>
		/// </returns>
		/// <exception cref="ApplicationException">
		/// 	<para>The implementation class is not marked with a <see cref="ConfigurationSectionNameAttribute"/>.</para>
		///		<para>-or-</para>
		///		<para>A configuration section is not found in the configuration file and
		///		<paramref name="throwOnSectionNotFound"/> is <see langword="true"/>.</para>
		///		<para>-or-</para>
		///		<para>A configuration section is not found in the configuration file 
		///		but is of the wrong type.</para>
		/// </exception>
		public static Implementation GetSection(bool throwOnSectionNotFound)
		{
			if (string.IsNullOrEmpty(_getSectionName()))
			{
				throw new ApplicationException(
					"The implementation class must be marked with a ConfigurationSectionName attribute and the name must not be empty.");
			}

			if (_section == null)
			{
				lock (_syncRoot)
				{
					if (_section == null)
					{
						_loadSection(throwOnSectionNotFound);
						if (_section == null) return null;
					}
				}
			}

			if (!_watcherInitialized)
			{
				lock (_syncRoot)
				{
					if (!_watcherInitialized)
					{
						var sectionPath = _section.ElementInformation.Source;
						if (!String.IsNullOrEmpty(sectionPath))
						{
							var sectionFile = new FileInfo(sectionPath);
							_fileWatcher = new FileSystemWatcher();
							_fileWatcher.NotifyFilter = NotifyFilters.CreationTime
													   | NotifyFilters.LastWrite | NotifyFilters.Security
													   | NotifyFilters.Size;
							_fileWatcher.Path = sectionFile.Directory.FullName;
							_fileWatcher.Filter = sectionFile.Name;
							_fileWatcher.Changed += _handleFileChange;
							_fileWatcher.EnableRaisingEvents = true;
						}
						_watcherInitialized = true;
					}
				}
			}

			return _section;
		}

		/// <summary>
		/// 	<para>Causes the <see cref="Refreshed"/> event to be manually raised.
		///		Should be called whenever a configuration element is modified.</para>
		/// </summary>
		public static void TriggerRefresh()
		{
			if (Refreshed != null)
			{
				Refreshed();
			}
		}

		/// <summary>
		///		<para>Invoked when this configuration section has been modified and refreshed.</para>
		/// </summary>
		public static event ConfigurationSectionRefreshedEventHandler Refreshed;

		private static void _handleFileChange(Object sender, FileSystemEventArgs e)
		{
			_log.InfoFormat("Configuration section '{0}' is being refreshed...", _getSectionName());


			ConfigurationManager.RefreshSection(_getSectionName());
			//clear to cause a reload
			_section = null;
			TriggerRefresh();
		}

		private bool? _isReadOnlyOverride;

		/// <summary>
		/// 	<para>Overriden. Gets a value indicating whether the
		///		<see cref="ConfigurationElement"/> object is read-only.</para>
		/// </summary>
		/// <returns>
		/// 	<para>true if the <see cref="ConfigurationElement"/> object is
		///		read-only; otherwise, false.</para>
		/// </returns>
		public override bool IsReadOnly()
		{
			return _isReadOnlyOverride.HasValue ? _isReadOnlyOverride.Value :
				base.IsReadOnly();
		}

		/// <summary>
		/// Allows modifications of this instance for test purposes.
		/// </summary>
		/// <param name="modifications">Delegate containing modifications to this
		/// instance.</param>
		public void ModifyForTest(System.Action modifications)
		{
			if (modifications == null) throw new ArgumentNullException("modifications");
			try
			{
				_isReadOnlyOverride = false;
				modifications();
			}
			finally
			{
				_isReadOnlyOverride = null;
			}
		}
	}
}