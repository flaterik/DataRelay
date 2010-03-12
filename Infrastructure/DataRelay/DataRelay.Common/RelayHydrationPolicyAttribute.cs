using System;
using System.Diagnostics;

namespace MySpace.DataRelay
{
	/// <summary>
	/// 	<para>Encapsulates hydration settings for the target type.</para>
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
	public class RelayHydrationPolicyAttribute : Attribute, IRelayHydrationPolicy
	{
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="RelayHydrationPolicyAttribute"/> class.</para>
		/// </summary>
		/// <param name="relayTypeName">
		///	<para>The relay type name used to cache objects of this type.</para>
		/// </param>
		public RelayHydrationPolicyAttribute(string relayTypeName)
		{
			RelayTypeName = relayTypeName;
		}

		/// <summary>
		/// Gets or sets the type name entry used to resolve the type id for this type in the relay type settings config.
		/// </summary>
		/// <value>The name of the relay type.</value>
		public string RelayTypeName { get; private set; }

		/// <summary>
		/// Gets or sets the key type used for hydrating objects.
		/// </summary>
		/// <value>The key type.</value>
		public RelayKeyType KeyType { get; set; }

		/// <summary>
		/// Gets a set of options that indicate how and under what conditions to hydrate relay cache misses.
		/// </summary>
		/// <value>
		/// A set of options that indicate how and under what conditions to hydrate relay cache misses.
		/// </value>
		public RelayHydrationOptions Options { get; set; }

		/// <summary>
		/// Returns a <see cref="T:System.String"/> that represents the current <see cref="RelayHydrationPolicyAttribute"/>.
		/// </summary>
		/// <returns>
		/// A <see cref="T:System.String"/> that represents the current <see cref="RelayHydrationPolicyAttribute"/>.
		/// </returns>
		public override string ToString()
		{
			return string.Format(
				"TypeName=\"{0}\", Misses=\"{1}\", BulkMisses=\"{2}\", KeyType=\"{3}\"",
				RelayTypeName ?? "(null)",
				(Options & RelayHydrationOptions.HydrateOnMiss) == RelayHydrationOptions.HydrateOnMiss,
				(Options & RelayHydrationOptions.HydrateOnBulkMiss) == RelayHydrationOptions.HydrateOnBulkMiss,
				KeyType);
		}
	}
}
