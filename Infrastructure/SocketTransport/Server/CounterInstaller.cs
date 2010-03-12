using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;

using System.Diagnostics;
using MySpace.Configuration;

namespace MySpace.SocketTransport
{
	[RunInstaller(true)]
	public partial class CounterInstaller : Installer
	{
        private static readonly MySpace.Logging.LogWrapper log = new MySpace.Logging.LogWrapper();
        
        public CounterInstaller()
		{
			InitializeComponent();
		}

		public override void Install(System.Collections.IDictionary stateSaver)
		{
			InstallCounters();
			base.Install(stateSaver);
		}

		public override void Uninstall(System.Collections.IDictionary savedState)
		{
			RemoveCounters();
			base.Uninstall(savedState);
		}

		public static void RemoveCounters()
		{
			string message = string.Empty;
			try
			{				
				message = "Removing performance counter category " + SocketServer.PerformanceCategoryName;
				if(log.IsInfoEnabled)
                    log.Info(message);
				Console.WriteLine(message);
				PerformanceCounter.CloseSharedResources();
				PerformanceCounterCategory.Delete(SocketServer.PerformanceCategoryName);
			}
			catch (Exception ex)
			{
				message = "Exception removing counters: " + ex.ToString();
				if(log.IsErrorEnabled)
                    log.Error(message);
                Console.WriteLine(message);
			}	
		}
		public static bool InstallCounters()
		{
			string message = String.Empty;
			try
			{
                if (log.IsInfoEnabled)
                    log.Info("Creating performance counter category " + SocketServer.PerformanceCategoryName);
				
                CounterCreationDataCollection counterDataCollection = new CounterCreationDataCollection();
				
				for (int i = 0; i < SocketServer.PerformanceCounterNames.Length; i++)
				{
					counterDataCollection.Add(new CounterCreationData(SocketServer.PerformanceCounterNames[i], SocketServer.PerformanceCounterHelp[i], SocketServer.PerformanceCounterTypes[i]));
					message = "Creating perfomance counter " + SocketServer.PerformanceCounterNames[i];
					Console.WriteLine(message);
					if(log.IsInfoEnabled)
                        log.Info(message);
				}

				PerformanceCounterCategory.Create(SocketServer.PerformanceCategoryName, "Counters for the MySpace Socket Server", PerformanceCounterCategoryType.MultiInstance, counterDataCollection);
				return true;
			}
			catch (Exception ex)
			{
				message = "Error creating Perfomance Counter Category " + SocketServer.PerformanceCategoryName + ": " + ex.ToString() + ". Counter category will not be used.";
				Console.WriteLine(message);
                if (log.IsErrorEnabled)
                    log.Error(message);
				return false;
			}
		}
	}
}