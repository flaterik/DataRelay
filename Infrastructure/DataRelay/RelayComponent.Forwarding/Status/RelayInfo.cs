using System.Xml.Serialization;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// A class that stores the information collected by the relay system.
	/// </summary>
	[XmlRoot("RelayInfo")]
	public class RelayInfo
	{
		/// <summary>
		/// The statistical information collected by the <see cref="Forwarder"/>.
		/// </summary>
		[XmlElement("ForwarderStatus")]
		public ForwarderStatus ForwarderStatus { set; get; }

	}
}
