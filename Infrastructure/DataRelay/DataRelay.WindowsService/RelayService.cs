using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace MySpace.DataRelay.WindowsService
{

    [StructLayout(LayoutKind.Sequential)]
    public struct SERVICE_STATUS
    {
        public int serviceType;
        public int currentState;
        public int controlsAccepted;
        public int win32ExitCode;
        public int serviceSpecificExitCode;
        public int checkPoint;
        public int waitHint;
    }

    public enum State
    {
        SERVICE_STOPPED = 0x00000001,
        SERVICE_START_PENDING = 0x00000002,
        SERVICE_STOP_PENDING = 0x00000003,
        SERVICE_RUNNING = 0x00000004,
        SERVICE_CONTINUE_PENDING = 0x00000005,
        SERVICE_PAUSE_PENDING = 0x00000006,
        SERVICE_PAUSED = 0x00000007,
    }

	public partial class RelayService : ServiceBase
	{

        private static readonly MySpace.Logging.LogWrapper log = new MySpace.Logging.LogWrapper();
        [DllImport("ADVAPI32.DLL", EntryPoint = "SetServiceStatus")]
        public static extern bool SetServiceStatus(
                        IntPtr hServiceStatus,
                        ref SERVICE_STATUS lpServiceStatus
                        );
        private SERVICE_STATUS serviceStatus;

		RelayServer server = null;
        
		public RelayService()
		{
			InitializeComponent();
		}

		protected override void OnStart(string[] args)
		{
            try
            {
                IntPtr handle = this.ServiceHandle;
                serviceStatus.currentState = (int)State.SERVICE_START_PENDING;
                SetServiceStatus(handle, ref serviceStatus);

                string baseDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                Directory.SetCurrentDirectory(baseDir);
                ThreadPool.QueueUserWorkItem(new WaitCallback(StartRelayServer));

                serviceStatus.currentState = (int)State.SERVICE_RUNNING;
                SetServiceStatus(handle, ref serviceStatus);
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Exception starting DataRelay Service: {0}", ex);
            }
		}

		private void StartRelayServer(object state)
		{
			server = new RelayServer();
            if (log.IsInfoEnabled)
                log.InfoFormat("Starting Relay Server at {0} ", DateTime.Now);
			try
			{
				server.Start();
				if (log.IsInfoEnabled)
                    log.InfoFormat("Relay Server Started  at {0}", DateTime.Now);
			}
			catch (Exception ex)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Exception starting DataRelay Service: {0}. Stopping.", ex);
				OnStop();
			}
		}
		
		protected override void OnStop()
		{
			try
			{
				if (server != null)
				{
                    IntPtr handle = this.ServiceHandle;
                    serviceStatus.currentState = (int)State.SERVICE_STOP_PENDING;
                    SetServiceStatus(handle, ref serviceStatus);

                    if (log.IsInfoEnabled)
                        log.InfoFormat("Stopping service at {0}", DateTime.Now);

                    server.Stop();

                    if (log.IsInfoEnabled)
                        log.InfoFormat("Service stopped at {0}", DateTime.Now);

                    serviceStatus.currentState = (int)State.SERVICE_STOPPED;
                    SetServiceStatus(handle, ref serviceStatus);
				}
            }
			catch (Exception ex)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Exception stopping DataRelay Service: {0}.", ex);				
			}
		}
	}
}
