using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace MySpace.DataRelay.RelayComponent.Bdb
{
    [XmlRoot("BerkeleyDbConfig", Namespace = "http://myspace.com/BerkeleyDbConfig.xsd")]
    public class BerkeleyDbConfig
    {
        private short minTypeId;
        private short maxTypeId;
        private int bufferSize;
        private long shutdownWindow;
        private EnvironmentConfig envConfig;

        [XmlElement("MinTypeId")]
        public short MinTypeId { get { return minTypeId; } set { minTypeId = value; } }

        [XmlElement("MaxTypeId")]
        public short MaxTypeId { get { return maxTypeId; } set { maxTypeId = value; } }

        [XmlElement("BufferSize")]
        public int BufferSize { get { return bufferSize; } set { bufferSize = value; } }

        [XmlElement("ShutdownWindow")]
        public long ShutdownWindow { get { return shutdownWindow; } set { shutdownWindow = value; } }

        [XmlElement("EnvironmentConfig", Namespace = "http://myspace.com/EnvironmentConfig.xsd")]
        public EnvironmentConfig EnvironmentConfig { get { return envConfig; } set { envConfig = value; } }

    }
}
