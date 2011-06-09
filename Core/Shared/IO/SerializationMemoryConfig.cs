using System;
using System.Linq;
using System.Xml.Serialization;

namespace MySpace.Common.IO
{
	[XmlRoot("SerializationMemory", Namespace = "http://myspace.com/SerializationMemoryConfig.xsd")]
	public class SerializationMemoryConfig : PoolConfig
	{
		[XmlAttribute("enablePooling")]
		public bool EnablePooling { get; set; }
	}
}
