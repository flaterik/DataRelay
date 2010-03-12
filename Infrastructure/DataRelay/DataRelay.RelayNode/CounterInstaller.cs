using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;

namespace MySpace.DataRelay
{
	[RunInstaller(true)]
	public partial class CounterInstaller : Installer
	{
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
			string message = String.Empty;
			try
			{
				if(RelayNode.log.IsInfoEnabled)
                    RelayNode.log.InfoFormat("Creating performance counter category {0}", RelayNodeCounters.PerformanceCategoryName);
				Console.WriteLine("Creating performance counter category " + RelayNodeCounters.PerformanceCategoryName);
				CounterCreationDataCollection counterDataCollection = new CounterCreationDataCollection();

				for (int i = 0; i < RelayNodeCounters.PerformanceCounterNames.Length; i++)
				{
					counterDataCollection.Add(new CounterCreationData(RelayNodeCounters.PerformanceCounterNames[i], RelayNodeCounters.PerformanceCounterHelp[i], RelayNodeCounters.PerformanceCounterTypes[i]));
					message = "Creating perfomance counter " + RelayNodeCounters.PerformanceCounterNames[i];
					Console.WriteLine(message);
					if(RelayNode.log.IsInfoEnabled)
                        RelayNode.log.Info(message);
				}

				PerformanceCounterCategory.Create(RelayNodeCounters.PerformanceCategoryName, "Counters for the MySpace Data Relay", PerformanceCounterCategoryType.MultiInstance, counterDataCollection);
				return true;
			}
			catch (Exception ex)
			{
				message = "Error creating Perfomance Counter Category " + RelayNodeCounters.PerformanceCategoryName + ": " + ex.ToString() + ". Counter category will not be used.";
				Console.WriteLine(message);
                if (RelayNode.log.IsErrorEnabled)
                    RelayNode.log.Error(message);
				return false;
			}




		}

		public static void RemoveCounters()
		{
			string message = string.Empty;
			try
			{
				message = "Removing performance counter category " + RelayNodeCounters.PerformanceCategoryName;
				if(RelayNode.log.IsInfoEnabled)
                        RelayNode.log.Info(message);
				Console.WriteLine(message);
				PerformanceCounter.CloseSharedResources();
				PerformanceCounterCategory.Delete(RelayNodeCounters.PerformanceCategoryName);
			}
			catch (Exception ex)
			{
				message = "Exception removing counters: " + ex.ToString();
				Console.WriteLine(message);
                if (RelayNode.log.IsErrorEnabled)
                    RelayNode.log.Error(message);
			}
		}
	}
}