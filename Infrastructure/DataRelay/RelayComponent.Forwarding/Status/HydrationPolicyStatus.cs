using System.Xml.Serialization;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// Hydration policy information for this type.
	/// </summary>
	[XmlRoot("HydrationPolicyStatus")]
	public class HydrationPolicyStatus
	{
		/// <summary>
		/// The <see cref="RelayKeyType"/> used for hydrating objects.
		/// </summary>
		[XmlElement("KeyType")]
		public string KeyType { set; get; }

		/// <summary>
		/// If <see langword="true"/> it indicates that objects will
		/// hydrate on cache misses when getting a single item.
		/// </summary>
		[XmlElement("HydrateMisses")]
		public bool HydrateMisses { set; get; }
		/// <summary>
		/// If <see langword="true"/> it indicates that objects will 
		/// hydrate on cache misses when getting multiple items.
		/// </summary>
		[XmlElement("HydrateBulkMisses")]
		public bool HydrateBulkMisses { set; get; }
	}
}
