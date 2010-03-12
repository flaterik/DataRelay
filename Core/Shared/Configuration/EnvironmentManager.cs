using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using MySpace.Logging;

namespace MySpace.Configuration
{
	/// <summary>
	/// Responsible for providing functionality for retrieving information about the 
	/// execution and deployment environment.
	/// </summary>
	public static class EnvironmentManager
	{
		private readonly static LogWrapper _log = new LogWrapper();

		private static EnvironmentMappingsConfig GetConfig()
		{
			var config = EnvironmentMappingsConfig.GetConfig();
			if (config == null)
			{
				_log.Warn("No Config found");
			}
			return config;
		}

		static EnvironmentManager()
		{
			EnvironmentMappingsConfig.ConfigChanged += ConfigReload;
		}

		private static void ConfigReload(EnvironmentMappingsConfig newConfig)
		{
			Reset();
		}

		private static string _currentEnvironment = null;
		private static readonly object _syncEnv = new object();

		/// <summary>
		/// Forces the use of the given environment name.  Call <see cref="Reset"/> to undo.
		/// </summary>
		/// <param name="environmentName">The name of the environment to use.</param>
		/// <exception cref="ArgumentNullException">Thrown when environment name is <see langword="null"/>
		/// or <see cref="string.Empty"/>.</exception>
		public static void OverrideCurrentEnvironment(string environmentName)
		{
			if (string.IsNullOrEmpty(environmentName)) throw new ArgumentNullException("environmentName");
			lock (_syncEnv)
			{
				string newEnv = Clean(environmentName);
				if (newEnv == _currentEnvironment) return; //no change
				string oldEnv = _currentEnvironment;
				_currentEnvironment =  newEnv;
				_fireEnvironmentChangedEvent(oldEnv, newEnv);
			}
		}

		private static void _fireEnvironmentChangedEvent(string oldEnvironment, string newEnvironment)
		{
			var ch = EnvironmentChanged;
			if (ch != null)
			{
				ch(oldEnvironment ?? string.Empty, newEnvironment);
			}
		}

		/// <summary>
		/// Undoes <see cref="OverrideCurrentEnvironment"/> and 
		/// reevaluates the mappings to determine <see cref="CurrentEnvironment"/>. 
		/// Does not cause the config to be reread from disk.
		/// </summary>
		public static void Reset()
		{
			string old = CurrentEnvironment;
			_currentEnvironment = null;
			if (old != CurrentEnvironment)
			{
				_fireEnvironmentChangedEvent(old, CurrentEnvironment);
			}
			
		}

		/// <summary>
		/// A delegate for the <see cref="EnvironmentChanged"/> event.
		/// </summary>
		/// <param name="oldEnvironment">The old environment.</param>
		/// <param name="newEnvironment">The new environment.</param>
		public delegate void DelegateEnvironmentChanged(string oldEnvironment, string newEnvironment);

		/// <summary>
		/// Is fired when the environment has been changed by <see cref="OverrideCurrentEnvironment"/>
		/// or <see cref="Reset"/>.
		/// </summary>
		public static event DelegateEnvironmentChanged EnvironmentChanged;

		/// <summary>
		/// Returns the name of the current environment in all lower case letters with no white space.
		/// </summary>
		/// <returns>The current environment, or an empty string which means no environment is defined.</returns>
		public static string CurrentEnvironment
		{
			get
			{
				if (_currentEnvironment != null) return _currentEnvironment;

				string currentEnvironment = null;
				lock (_syncEnv)
				{
					if (_currentEnvironment != null) return _currentEnvironment;

					var config = GetConfig();
					if (config == null) return _currentEnvironment = string.Empty; 

					string machineName = Environment.MachineName;

					List<IPAddress> myAddresses = new List<IPAddress>();

					IPAddress environmentDefinedAddress = GetEnvironmentDefinedIPAddress();
					if (environmentDefinedAddress != null)
					{
						myAddresses.Add(environmentDefinedAddress);
					}

					NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
					foreach (NetworkInterface networkInterface in interfaces)
					{
						IPInterfaceProperties props = networkInterface.GetIPProperties();

						foreach (UnicastIPAddressInformation addressInfo in props.UnicastAddresses)
						{
							myAddresses.Add(addressInfo.Address);
						}
					}

					foreach (EnvironmentMappingConfig mapping in config.Mappings)
					{
						Regex regex = new Regex(mapping.HostPattern);
						if (regex.IsMatch(machineName))
						{
							currentEnvironment = Clean(mapping.Environment);
							break;
						}

						foreach (IPAddress address in myAddresses)
						{
							if (regex.IsMatch(address.ToString()))
							{
								currentEnvironment = Clean(mapping.Environment);
							}
						}
						if (currentEnvironment != null) break;
					}

					if (currentEnvironment == null)
					{
						//not found, use default environment
						currentEnvironment = Clean(config.DefaultEnvironment);
					}
					return _currentEnvironment = currentEnvironment;
				}
			}
		}

		/// <summary>
		/// Gets a list of all available environments.
		/// </summary>
		/// <returns>A list instance; never null.</returns>
		public static IList<string> GetAllAvailableEnvironments()
		{
			var config = GetConfig();
			if (config == null || config.AvailableEnvironments == null || config.AvailableEnvironments.Count == 0)
			{
				return new string[0];
			}

			IList<string> environments = new List<string>(config.AvailableEnvironments.Count);
			config.AvailableEnvironments.ForEach(en => environments.Add(en.Environment));
			return environments;
		}

		private static string Clean(string stringToClean)
		{
			return (stringToClean ?? string.Empty).Trim().ToLower();
		}

		/// <summary>
		/// Our Xen hosted machines all think they have the same IP address at the NIC level. When 
		/// they boot, an Environment variable "IPADDRESS" is set to give us a locally visible
		/// copy of their external IP address.
		/// </summary>
		/// <returns>Returns <see langword="null"/> if no environment address is defined; otherwise, returns the IP address.</returns>
		public static IPAddress GetEnvironmentDefinedIPAddress()
		{
			IPAddress environmentIP = null;

			try
			{
				string environmentIPstring = Environment.GetEnvironmentVariable("IPADDRESS", EnvironmentVariableTarget.Machine);
				if (String.IsNullOrEmpty(environmentIPstring) == false)
				{
					if (IPAddress.TryParse(environmentIPstring, out environmentIP))
					{
						if (_log.IsInfoEnabled)
						{
							_log.InfoFormat("Got IPAddress {0} from environment variable \"IPADDRESS\"", environmentIP);
						}
					}
					else
					{
						if (_log.IsWarnEnabled)
						{
							_log.WarnFormat("Could not parse address {0} from environment variable \"IPADDRESS\"", environmentIPstring);
						}
					}
				}
			}
			catch (Exception e)
			{
				_log.ErrorFormat("Exception getting IP address from environment variable \"IPAddress\": {0}", e);
				throw;
			}

			return environmentIP;
		}
	}
}
