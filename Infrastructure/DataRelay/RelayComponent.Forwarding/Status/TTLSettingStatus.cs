using System;
using System.Xml.Serialization;
using MySpace.DataRelay.Common.Schemas;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// Hydration policy information for this type.
	/// </summary>
	[XmlRoot("TTLSettingStatus")]
	public class TTLSettingStatus
	{
		private bool _isSpecified = false;
		/// <summary>
		/// Gets or sets whether TTL is enabled.
		/// </summary>
		[XmlElement("Enabled")]
		public bool Enabled { set; get; }

		/// <summary>
		/// Gets or sets the default TTL. 
		/// </summary>
		[XmlElement("DefaultTTLSeconds")]
		public int DefaultTTLSeconds { set; get; }
	
		/// <summary>
		/// Creates a clone of this <see cref="TTLSettingStatus"/>.
		/// </summary>
		/// <returns>
		/// <para>A cloned <see cref="TTLSettingStatus"/> object that shares no object
		///		references as this instance; never <see langword="null"/>.
		/// </para>
		/// </returns>
		internal TTLSettingStatus Clone()
		{
			TTLSettingStatus status = new TTLSettingStatus();

			status.Enabled = Enabled;
			status.DefaultTTLSeconds = DefaultTTLSeconds;
			status._isSpecified = _isSpecified;

			return status;
		}
		/// <summary>
		/// This method updates the <see cref="TTLSettingStatus"/> with information
		/// provided by the <see cref="TTLSetting"/>.
		/// </summary>
		/// <param name="ttlSetting">The <see cref="TTLSetting"/> to update the 
		/// <see cref="TTLSettingStatus"/> with.</param>
		internal void Update(TTLSetting ttlSetting)
		{
			if (ttlSetting != null)
			{
				_isSpecified = true;
				Enabled = ttlSetting.Enabled;
				DefaultTTLSeconds = ttlSetting.DefaultTTLSeconds;
			}
			return;
		}

		/// <summary>
		/// Returns a copy of the <see cref="TTLSettingStatus"/> or null if it is not enabled.
		/// </summary>
		/// <returns>A copy of the <see cref="TTLSettingStatus"/> or null if if it is not enabled.</returns>
		internal TTLSettingStatus GetStatus()
		{
			TTLSettingStatus status = null;
			if (_isSpecified == true)
			{
				status = this.Clone();
			}
			return status;
		}
	}
}
