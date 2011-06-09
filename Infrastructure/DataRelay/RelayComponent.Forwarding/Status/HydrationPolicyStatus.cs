using System;
using System.Xml.Serialization;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// Hydration policy information for this type.
	/// </summary>
	[XmlRoot("HydrationPolicyStatus")]
	public class HydrationPolicyStatus
	{
		private bool _isSpecified = false;

		/// <summary>
		/// Gets or sets the <see cref="RelayKeyType"/> used for hydrating objects.
		/// </summary>
		[XmlElement("KeyType")]
		public string KeyType { set; get; }

		/// <summary>
		/// Gets or sets the status on whether objects will hydrate.
		/// If <see langword="true"/> it indicates that objects will
		/// hydrate on cache misses when getting a single item.
		/// </summary>
		[XmlElement("HydrateOnMiss")]
		public bool HydrateOnMiss { set; get; }
		/// <summary>
		/// Gets or sets the status on whether objects will hydrate.
		/// If <see langword="true"/> it indicates that objects will 
		/// hydrate on cache misses when getting multiple items.
		/// </summary>
		[XmlElement("HydrateOnBulkMiss")]
		public bool HydrateOnBulkMiss { set; get; }

		/// <summary>
		/// Creates a clone of this <see cref="HydrationPolicyStatus"/>.
		/// </summary>
		/// <returns>
		/// <para>A cloned <see cref="HydrationPolicyStatus"/> object that shares no object
		///		references as this instance; never <see langword="null"/>.
		/// </para>
		/// </returns>
		public HydrationPolicyStatus Clone()
		{
			HydrationPolicyStatus status = new HydrationPolicyStatus();

			status.HydrateOnBulkMiss = HydrateOnBulkMiss;
			status.HydrateOnMiss = HydrateOnMiss;
			status.KeyType = KeyType;
			status._isSpecified = _isSpecified;

			return status;
		}

		/// <summary>
		/// This method updates the <see cref="HydrationPolicyStatus"/> with information
		/// provided by the <see cref="IRelayHydrationPolicy"/>.
		/// </summary>
		/// <param name="hydrationPolicy">The <see cref="IRelayHydrationPolicy"/> to update the 
		/// <see cref="HydrationPolicyStatus"/> with.</param>
		internal void Update(IRelayHydrationPolicy hydrationPolicy)
		{
			//if hydration policy is null create default HydrationPolicyStatus with _isSpecified as false
			if(hydrationPolicy== null) return;

			_isSpecified = true;
			KeyType = hydrationPolicy.KeyType.ToString();
			HydrateOnMiss = (hydrationPolicy.Options &
							RelayHydrationOptions.HydrateOnMiss) ==
							RelayHydrationOptions.HydrateOnMiss;
			HydrateOnBulkMiss = (hydrationPolicy.Options & RelayHydrationOptions.HydrateOnBulkMiss) == 
				RelayHydrationOptions.HydrateOnBulkMiss;

			return;
		}

		/// <summary>
		/// Returns a copy of the <see cref="HydrationPolicyStatus"/> or null if it is not specified.
		/// </summary>
		/// <returns>A copy of the <see cref="HydrationPolicyStatus"/> or null if if it is not specified.</returns>
		internal HydrationPolicyStatus GetStatus()
		{
			HydrationPolicyStatus status = null;
			if(_isSpecified == true)
			{
				status = new HydrationPolicyStatus();
				status = this.Clone();
			}
			return status;
		}
	}
}
