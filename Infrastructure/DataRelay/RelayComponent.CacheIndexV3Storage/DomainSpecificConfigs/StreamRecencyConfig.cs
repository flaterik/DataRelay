using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.DomainSpecificConfigs
{
    [XmlRoot("StreamRecencyConfig", Namespace = "http://myspace.com/StreamRecencyConfig.xsd")]
    public class StreamRecencyConfig
    {
        [XmlElement("InitialRecencyValue")]
        public double InitialRecencyValue;

        [XmlElement("TimeStampTagName")]
        public string TimeStampTagName;

        [XmlElement("TypeTagName")]
        public string TypeTagName;

        [XmlElement("DefaultHalfLife")]
        public double DefaultHalfLife;

        [XmlArray("TypeDecayMappingCollection")]
        [XmlArrayItem(typeof(TypeDecayMapping))]
        public TypeDecayMappingCollection TypeDecayMappingCollection;
    }

    public class TypeDecayMappingCollection : KeyedCollection<int, TypeDecayMapping>
    {
        protected override int GetKeyForItem(TypeDecayMapping item)
        {
            return item.Type;
        }
    }

    public class TypeDecayMapping
    {
        [XmlElement("Type")]
        public int Type;

        [XmlElement("HalfLife")]
        public double HalfLife;
    }
}