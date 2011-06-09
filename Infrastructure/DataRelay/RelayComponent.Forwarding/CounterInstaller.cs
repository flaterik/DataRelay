using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	[RunInstaller(true)]
	public partial class CounterInstaller : Installer
	{

        private static readonly MySpace.Logging.LogWrapper log = new MySpace.Logging.LogWrapper();

        /// <summary>
		/// Responsible for installing forwarding counters.
		/// </summary>
        public CounterInstaller()
		{
			InitializeComponent();
		}



		/// <summary>
		/// Installs forwarding counters
		/// </summary>		
        public override void Install(System.Collections.IDictionary stateSaver)
		{
			InstallCounters();
			base.Install(stateSaver);
		}

        /// <summary>
        /// Uninstalls forwarding counters
        /// </summary>
		public override void Uninstall(System.Collections.IDictionary savedState)
		{
			RemoveCounters();
			base.Uninstall(savedState);
		}

        /// <summary>
        /// Installs forwarding counters
        /// </summary>		
		public static bool InstallCounters()
		{
			string message = String.Empty;
			try
			{   
                log.InfoFormat("Creating performance counter category {0}", ForwardingCounters.PerformanceCategoryName);
				Console.WriteLine("Creating performance counter category " + ForwardingCounters.PerformanceCategoryName);
				CounterCreationDataCollection counterDataCollection = new CounterCreationDataCollection();

				for (int i = 0; i < ForwardingCounters.PerformanceCounterNames.Length; i++)
				{
					counterDataCollection.Add(new CounterCreationData(ForwardingCounters.PerformanceCounterNames[i], ForwardingCounters.PerformanceCounterHelp[i], ForwardingCounters.PerformanceCounterTypes[i]));
					message = "Creating perfomance counter " + ForwardingCounters.PerformanceCounterNames[i];
					Console.WriteLine(message);
                    if (log.IsDebugEnabled)
                        log.Debug(message);
				}

				PerformanceCounterCategory.Create(ForwardingCounters.PerformanceCategoryName, "Counters for the MySpace Data Relay", PerformanceCounterCategoryType.MultiInstance, counterDataCollection);
				return true;
			}
			catch (System.Security.SecurityException)
			{
				message = "Cannot automatically create Performance Counters for Relay Forwarder. Please run installutil against MySpace.DataRelay.RelayComponent.Forwarding.dll";
				Console.WriteLine(message);
                if (log.IsWarnEnabled)
                    log.Warn(message);
				return false;
			}
			catch (Exception ex)
			{
				message = "Error creating Perfomance Counter Category " + ForwardingCounters.PerformanceCategoryName + ": " + ex.ToString() + ". Counter category will not be used.";
				Console.WriteLine(message);
                if (log.IsErrorEnabled)
                    log.Error(message);
				return false;
			}




		}

        /// <summary>
        /// Removes forwarding counters
        /// </summary>		
		public static void RemoveCounters()
		{
			string message = string.Empty;
			try
			{
				message = "Removing performance counter category " + ForwardingCounters.PerformanceCategoryName;
                if (log.IsInfoEnabled)
                    log.Info(message);
				Console.WriteLine(message);
				PerformanceCounter.CloseSharedResources();
				PerformanceCounterCategory.Delete(ForwardingCounters.PerformanceCategoryName);
			}
			catch (Exception ex)
			{
				message = "Exception removing counters: " + ex.ToString();
				Console.WriteLine(message);
                if (log.IsInfoEnabled)
                    log.Info(message);
			}
		}
	}
}