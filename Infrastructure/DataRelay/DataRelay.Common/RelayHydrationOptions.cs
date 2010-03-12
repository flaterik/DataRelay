using System;
using System.Xml.Serialization;

namespace MySpace.DataRelay
{
	/// <summary>
	/// A set of options related to data relay hydration that may be enabled or disabled.
	/// </summary>
	[Flags]
	public enum RelayHydrationOptions
	{
		/// <summary>
		/// Disable everything.
		/// </summary>
		[XmlEnum("None")]
		None = 0,
		/// <summary>
		/// Hydrate on cache misses when getting a single item.
		/// </summary>
		[XmlEnum("HydrateOnMiss")]
		HydrateOnMiss = 0x1,
		/// <summary>
		/// Hydrate on cache misses when getting multiple items.
		/// </summary>
		[XmlEnum("HydrateOnBulkMiss")]
		HydrateOnBulkMiss = 0x2,
		/// <summary>
		/// Hydrate all cache misses.
		/// </summary>
		[XmlEnum("HydrateAll")]
		HydrateAll = HydrateOnMiss | HydrateOnBulkMiss
	}
}
