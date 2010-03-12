using System;

namespace MySpace.DataRelay
{
	/// <summary>
	/// 	<para>Encapsulates hydration settings for a relay type.</para>
	/// </summary>
	public interface IRelayHydrationPolicy
	{
		/// <summary>
		/// Gets the key type used for hydrating objects.
		/// </summary>
		/// <value>The relay key type to use for hydration.</value>
		RelayKeyType KeyType { get; }

		/// <summary>
		/// 	<para>Gets a set of options that indicate how and under what conditions to hydrate relay cache misses.</para>
		/// </summary>
		/// <value>
		/// 	<para>A set of options that indicate how and under what conditions to hydrate relay cache misses.</para>
		/// </value>
		RelayHydrationOptions Options { get; }
	}
}
