using System.Xml.Serialization;

namespace MySpace.DataRelay.Common.Schemas
{
	[XmlRoot("TransportSettings", Namespace = "http://myspace.com/RelayTransportSettings.xsd")]
	public class TransportSettings
	{
		[XmlElement("ListenPort")]
		public int ListenPort;

        [XmlElement("PipelinePort")]
        public ushort PipelinePort;

		[XmlElement("HttpListenPort")] 
		public int HttpListenPort;
	}
}
