using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;

namespace MySpace.DataRelay.WindowsService
{
	[RunInstaller(true)]
	public partial class RelayInstaller : Installer
	{
		private System.ServiceProcess.ServiceProcessInstaller serviceProcessInstaller;
		private System.ServiceProcess.ServiceInstaller serviceInstaller;

	

		public RelayInstaller()
		{
			InitializeComponent();
			this.serviceProcessInstaller = new System.ServiceProcess.ServiceProcessInstaller();
			this.serviceInstaller = new System.ServiceProcess.ServiceInstaller();
			
			this.serviceProcessInstaller.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
			this.serviceProcessInstaller.Password = null;
			this.serviceProcessInstaller.Username = null;

			try
			{
				if (EventLog.Exists("MySpace.DataRelay"))
					EventLog.Delete("MySpace.DataRelay");
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}
			try
			{
				string ConfigFile = AppDomain.CurrentDomain.GetData("APP_CONFIG_FILE").ToString();
				Console.WriteLine("Reading configuration from file: " + ConfigFile);
			}
			catch { }

			string instanceNumber = Environment.GetEnvironmentVariable("DataRelayInstanceName");
			
			

			Console.WriteLine("Instance number:" + instanceNumber);
			if (instanceNumber != null && instanceNumber != String.Empty)
			{
				this.serviceInstaller.ServiceName = "MySpace.DataRelay." + instanceNumber;
			}
			else
			{
				this.serviceInstaller.ServiceName = "MySpace.DataRelay";
			}
			this.serviceInstaller.DisplayName = "MySpace DataRelay";
			if (instanceNumber != null && instanceNumber != String.Empty)
			{
				this.serviceInstaller.DisplayName += " Instance " + instanceNumber;
			}
			this.serviceInstaller.Description = "Shuffles data around real good. Don't taunt the relay.";
			this.serviceInstaller.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
			// 
			// ProjectInstaller
			// 
			this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.serviceProcessInstaller,
            this.serviceInstaller});

		}
	}
}