using System.ComponentModel;
using System.Configuration.Install;

namespace MySpace.DataRelay.RelayComponent.FlexForwarding
{
	/// <summary>
	/// Installs the performance counters for FlexForwarder.
	/// </summary>
	[RunInstaller(true)]
	public partial class CountersInstaller : Installer
	{
		/// <summary>
		/// The Install handler.
		/// </summary>		
		public override void Install(System.Collections.IDictionary stateSaver)
		{
			Counters.Install();
			base.Install(stateSaver);
		}

		/// <summary>
		/// The uninstall handler.
		/// </summary>
		public override void Uninstall(System.Collections.IDictionary savedState)
		{
			Counters.Uninstall();
			base.Uninstall(savedState);
		}
	}
}