using System;
using System.Collections.Generic;
using System.Configuration;
using System.Xml.Serialization;
using MySpace.Shared.Configuration;

namespace MySpace.Configuration
{
	/// <summary>
	/// Represents a method that is called when the <see cref="SocketClientConfig"/> is changed
	/// or modified.
	/// </summary>
	/// <param name="newConfig">The new config, after the change.</param>
	public delegate void EnvironmentMappingConfigChangeMethod(EnvironmentMappingsConfig newConfig);

	/// <summary>
	/// Represents a configuration file containing mappings of environment names to computers 
	/// via a regular expressions representing either ip address or host names.
	/// </summary>
	[XmlRoot("EnvironmentMappingsConfig", Namespace = "http://myspace.com/EnvironmentMappingsConfig.xsd")]
	public class EnvironmentMappingsConfig
	{
		private const string _configSectionName = "EnvironmentMappingsConfig";
		private static bool _forceNoConfig = false;

		/// <summary>
		/// Fired when the config changes or is modified.
		/// </summary>
		public static event EnvironmentMappingConfigChangeMethod ConfigChanged;

		static EnvironmentMappingsConfig()
		{
			GetConfig(); //required to load the config so that the event fires.
			XmlSerializerSectionHandler.RegisterReloadNotification(typeof(EnvironmentMappingsConfig), (obj, args) =>
			{
				var cc = ConfigChanged;
				if (cc != null)
				{
					cc(GetConfig());
				}
			});
		}

		/// <summary>
		/// Gets or sets the default environment to use when no other environments apply.
		/// </summary>
		[XmlElement("DefaultEnvironment")]
		public string DefaultEnvironment { get; set; }

		/// <summary>
		/// Gets or sets the list of available environments delimited by commas.
		/// </summary>
		[XmlArray("AvailableEnvironments")]
		[XmlArrayItem("Environment")]
		public List<EnvironmentName> AvailableEnvironments { get; set; }

		/// <summary>
		/// Gets or sets the list of mappings.
		/// </summary>
		[XmlArray("EnvironmentMappings")]
		[XmlArrayItem("EnvironmentMapping")]
		public List<EnvironmentMappingConfig> Mappings { get; set; }

		/// <summary>
		/// Validates that the data in the config is correct and consistent. Relies on <see cref="Clean()"/> to be run first.
		/// </summary>
		/// <exception cref="ConfigurationErrorsException">Thrown when an environment outside of the available environments exists in the config.</exception>
		private void Validate()
		{
			IList<EnvironmentName> environments = AvailableEnvironments ?? new List<EnvironmentName>(0);

			string defaultEnv = DefaultEnvironment;
			if (string.IsNullOrEmpty(defaultEnv))
			{
				throw new ConfigurationErrorsException("the default environment was empty, this value is required");
			}
			bool defaultFound = false;
			foreach (EnvironmentName available in environments)
			{
				if (string.IsNullOrEmpty(available.Environment))
				{
					throw new ConfigurationErrorsException(string.Format("cannot have empty available environment"));
				}
				if (defaultEnv == available.Environment)
				{
					defaultFound = true;
					//don't break when found cause we're check all to ensure none are blank
				}
			}

			if (defaultFound == false)
			{
				throw new ConfigurationErrorsException(string.Format("the default environment {0} was not listed in the available environments", defaultEnv));
			}

			if (Mappings != null)
			{
				foreach (EnvironmentMappingConfig mapping in Mappings)
				{
					bool match = false;
					string env = mapping.Environment;

					if (string.IsNullOrEmpty(env))
					{
						throw new ConfigurationErrorsException(string.Format("cannot have empty environment in Mappings"));
					}

					foreach (EnvironmentName available in environments)
					{
						if (available.Environment == env)
						{
							match = true;
							break;
						}
					}
					if (match == false)
					{
						throw new ConfigurationErrorsException(string.Format("The following environment, {0}, isn't list in the config file as an 'Available Environment'", env));
					}
				}
			}
		}

		private void Clean()
		{
			DefaultEnvironment = Clean(DefaultEnvironment);
			if (AvailableEnvironments != null)
			{
				for (int i = 0; i < AvailableEnvironments.Count; i++)
				{
					AvailableEnvironments[i].Environment = Clean(AvailableEnvironments[i].Environment);
				}
			}
			if (Mappings != null)
			{
				for (int i = 0; i < Mappings.Count; i++)
				{
					Mappings[i].Environment = Clean(Mappings[i].Environment);
				}
			}
		}

		/// <summary>
		/// Used for testing only.
		/// </summary>
		internal static void ForceNoConfig(bool noConfig)
		{
			_forceNoConfig = noConfig;
		}

		/// <summary>
		/// Gets and validates the config.  Will throw if there's an error in the config.
		/// </summary>
		/// <returns>Returns the config file, or null, or may throw.</returns>
		/// <exception cref="ConfigurationErrorsException">Thrown when an environment outside of the available environments exists in the config.</exception>
		public static EnvironmentMappingsConfig GetConfig()
		{
			if (_forceNoConfig) return null;
			if (_config != null)
			{
				if (ForceValidate)
				{
					_config.Clean();
					_config.Validate();
				}
				ForceValidate = false;
				return _config;
			}
			EnvironmentMappingsConfig config = (EnvironmentMappingsConfig)ConfigurationManager.GetSection(_configSectionName);
			if (config != null)
			{
				config.Clean();
				config.Validate();
				_config = config;
			}
			return config;
		}

		/// <summary>
		/// Forces the next <see cref="GetConfig"/> to <see cref="Validate"/> the config.
		/// </summary>
		internal static bool ForceValidate { get; set; }

		/// <summary>
		/// Forces to the next read to refetch from disk.
		/// </summary>
		internal static void Refresh()
		{
			ConfigurationManager.RefreshSection(_configSectionName); //do first
			_config = null;
		}

		private static EnvironmentMappingsConfig _config;

		private static string Clean(string stringToClean)
		{
			return (stringToClean ?? string.Empty).Trim().ToLower();
		}
	}

	/// <summary>
	/// An instance of mapping an <see cref="Environment"/> to single or set of hosts via
	/// a regular expression set in <see cref="HostPattern"/>.
	/// </summary>
	public class EnvironmentMappingConfig
	{
		public EnvironmentMappingConfig()
		{
			EnvironmentMappingsConfig.ForceValidate = true;
		}

		/// <summary>
		/// Gets or sets the Regular Expression matching a computer name or ip address.
		/// </summary>
		[XmlAttribute("hostPattern")]
		public string HostPattern { get; set; }

		/// <summary>
		/// Gets or sets the environment name that applies to the <see cref="HostPattern"/>.
		/// </summary>
		[XmlAttribute("environment")]
		public string Environment { get; set; }
	}

	/// <summary>
	/// Represents an Environment name.
	/// </summary>
	public class EnvironmentName
	{
		/// <summary>
		/// Gets or sets the environment name.
		/// </summary>
		[XmlText]
		public string Environment { get; set; }
	}
}
