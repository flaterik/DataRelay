using System;
using System.Xml.Serialization;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// Statistical information specific to the <see cref="Forwarder"/>. 
	/// </summary>
	[XmlRoot("RelayStatistics")]
	public class RelayStatistics
	{
		/// <summary>
		/// Current Time of the <see cref="Forwarder"/>.
		/// </summary>
		[XmlElement("CurrentServerTime")]
		public DateTime CurrentServerTime { set; get; }
		/// <summary>
		/// Time of when the <see cref="Forwarder"/> was initialized.
		/// </summary>
		[XmlElement("InitializationTime")]
		public DateTime InitializationTime { set; get; }
	}
}
