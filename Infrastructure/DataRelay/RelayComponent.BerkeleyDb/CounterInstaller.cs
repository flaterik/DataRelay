using System;
using System.ComponentModel;
using System.Diagnostics;
using MySpace.Logging;
using System.Configuration.Install;

namespace MySpace.DataRelay.RelayComponent.BerkeleyDb
{
	[RunInstaller(true)]
	public partial class CounterInstaller : Installer
	{
        private static readonly LogWrapper Log = new LogWrapper();
        
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

		public static bool InstallCounters()
		{
			string message;
			try
			{
                if (Log.IsInfoEnabled)
                {
                    Log.InfoFormat("CounterInstaller:InstallCounters() Creating performance counter category {0}"
                        , BerkeleyDbCounters.PerformanceCategoryName);
                }
                Console.WriteLine("Creating performance counter category " + BerkeleyDbCounters.PerformanceCategoryName);
				CounterCreationDataCollection counterDataCollection = new CounterCreationDataCollection();

				for (int i = 0; i < BerkeleyDbCounters.PerformanceCounterNames.Length; i++)
				{
                    counterDataCollection.Add(new CounterCreationData(BerkeleyDbCounters.PerformanceCounterNames[i], 
                        BerkeleyDbCounters.PerformanceCounterHelp[i], BerkeleyDbCounters.PerformanceCounterTypes[i]));
					message = "Creating perfomance counter " + BerkeleyDbCounters.PerformanceCounterNames[i];
					Console.WriteLine(message);
                    if (Log.IsInfoEnabled)
                    {
                        Log.InfoFormat("CounterInstaller:InstallCounters() {0}"
                            , message);
                    }
                }

				PerformanceCounterCategory.Create(BerkeleyDbCounters.PerformanceCategoryName, "Counters for the MySpace Data Relay", PerformanceCounterCategoryType.MultiInstance, counterDataCollection);
				return true;
			}
			catch (Exception ex)
			{
				message = "Error creating Perfomance Counter Category " + BerkeleyDbCounters.PerformanceCategoryName + ": " + ex + ". Counter category will not be used.";
				Console.WriteLine(message);
                if (Log.IsErrorEnabled)
                {   
                    Log.Error(string.Format("CounterInstaller:InstallCounters() {0}", message), ex);
                }
                return false;
			}
		}

		public static void RemoveCounters()
		{
			string message;
			try
			{
				message = "Removing performance counter category " + BerkeleyDbCounters.PerformanceCategoryName;
                if (Log.IsInfoEnabled)
                {
                    Log.InfoFormat("CounterInstaller:InstallCounters() {0}", message);
                }
                Console.WriteLine(message);
				PerformanceCounter.CloseSharedResources();
				PerformanceCounterCategory.Delete(BerkeleyDbCounters.PerformanceCategoryName);
			}
			catch (Exception ex)
			{
				message = "Exception removing counters: " + ex;
				Console.WriteLine(message);
                if (Log.IsErrorEnabled)
                {   
                    Log.Error(string.Format("CounterInstaller:InstallCounters() {0}", message), ex);
                }
            }
		}
	}
}