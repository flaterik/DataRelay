using System.Xml.Serialization;

namespace MySpace.DataRelay.RelayComponent.FlexForwarding
{
	/// <summary>
	/// XML Configuration for FlexForwarder component.
	/// </summary>
	[XmlRoot("FlexForwarderConfig", Namespace = "http://myspace.com/FlexForwarderConfig.xsd")]
	public class FlexForwarderConfig
	{
		/// <summary>
		/// The Flex Cache group namme that this forwarder will use.
		/// </summary>
		[XmlElement("GroupName")]
		public string GroupName;
	}
}
