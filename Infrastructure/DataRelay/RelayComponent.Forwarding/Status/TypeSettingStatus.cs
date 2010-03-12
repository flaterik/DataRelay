using System.Xml.Serialization;
using MySpace.DataRelay.Common.Schemas;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// <see cref="TypeSetting"/> information related to statistical information 
	/// collected by the <see cref="Forwarder"/>.
	/// </summary>
	[XmlRoot("TypeSettingStatus")]
	public class TypeSettingStatus
	{
		/// <summary>
		/// Fully qualified name of the class.
		/// </summary>
		[XmlElement("TypeName")]
		public string TypeName { set; get; }
		/// <summary>
		/// Unique integer identifier.
		/// </summary>
        [XmlElement("TypeId")]
		public short TypeId { set; get; }
		/// <summary>
		/// If <see langword="true"/>, it indicates that no messages 
		/// of this type will get sent if the command originates 
		/// from the RelayClient.
		/// </summary>
		[XmlElement("Disabled")]
		public bool Disabled { set; get; }
		/// <summary>
		/// If <see langword="true"/> it indicates that if the 
		/// RelayClient is used the payload for a <see cref="RelayMessage"/> 
		/// of type <see cref="MessageType.Query"/> is compressed.
		/// </summary>
		[XmlElement("Compress")]
		public bool Compress { set; get; }
		/// <summary>
		/// Caching group to which the class belongs.
		/// </summary>
		[XmlElement("GroupName")]
		public string GroupName { set; get; }
		/// <summary>
		/// A value that links two <see cref="TypeId"/>s together.  
		/// This is primarily used for IndexCache.
		/// </summary>
		[XmlElement("RelatedIndexTypeId")]
		public short RelatedIndexTypeId { set; get; }
		/// <summary>
		/// Not used.
		/// </summary>
		[XmlElement("CheckRaceCondition")]
		public bool CheckRaceCondition { set; get; }
		/// <summary>
		/// Expiration settings for the class.
		/// </summary>
		[XmlElement("TTLSetting")]
		public TTLSetting TTLSetting { set; get; }
		/// <summary>
		/// If <see langword="true"/> it indicates that 
		/// the client will block until the in message 
		/// has been sent to the data relay server.
		/// </summary>
        [XmlElement("SyncInMessages")]
		public bool SyncInMessages { set; get; }
		/// <summary>
		/// Not used.
		/// </summary>
		[XmlElement("ThrowOnSyncFailure")]
		public bool ThrowOnSyncFailure { set; get; }
		/// <summary>
		/// Hydration policy information for this type.
		/// </summary>
        [XmlElement("HydrationPolicyStatus")]
		public HydrationPolicyStatus HydrationPolicyStatus { set; get; }
	}
}
