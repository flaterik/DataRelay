using System;
using System.Collections.Generic;
using MySpace.DataRelay.Common.Schemas;

namespace MySpace.DataRelay
{
	/// <summary>
	/// Responsible for managing all aspects of TTL for <see cref="RelayMessage"/>.
	/// </summary>
	/// <remarks>The ttl is primarily used for caching scenarios where if a cached
	/// object expires it gets deleted.</remarks>
	internal class LifetimeTtl
	{
		/// <summary>
		/// The type settings we use.
		/// </summary>
		private readonly TypeSettingCollection typeSettings = null;
		
		/// <summary>
		/// A value indicating if the expired TTL deletes is enabled.
		/// </summary>
		private readonly bool deletesEnabled = false;

		/// <summary>
		/// Initializes the current instance.
		/// </summary>
		/// <param name="deletesEnabled">A value indicating if the expiration deletes are enabled.</param>
		/// <param name="typeSettings">The type settings to use.</param>
		public LifetimeTtl(bool deletesEnabled, TypeSettingCollection typeSettings)
		{
			this.deletesEnabled = deletesEnabled;
			this.typeSettings = typeSettings;

			if (typeSettings == null)
			{
                if (RelayNode.log.IsWarnEnabled)
                    RelayNode.log.Warn("TypeSettingCollection is Null. TTL object expiration and deletion disabled!");
			}
		}
		
		/// <summary>
		/// Applies the default TTL to the given <see cref="RelayMessage"/>.
		/// </summary>
		/// <param name="message"></param>
		public void ApplyDefaultTTL(RelayMessage message)
		{
			if (message.Payload != null)
			{
				int defaultTTL = GetDefaultTTL(message.TypeId);
				if (message.Payload.TTL == -1 && defaultTTL != -1)
				{
					//message has no specified ttl but this type does have a default ttl
					message.Payload.TTL = defaultTTL;
				}
			}
		}

		/// <summary>
		/// Evaluates the <see cref="RelayMessage"/> for an expired TTL and if
		/// the message qualifies for deletion a deletion message is returned.
		/// </summary>
		/// <param name="message">The <see cref="RelayMessage"/> to evaluate.</param>
		/// <returns>Returns a delete message if a delete is required; otherwise, returns null.</returns>
		public RelayMessage ProcessExpiredTTLDelete(RelayMessage message)
		{
			RelayMessage deleteMessage = null;
			
			if (message.Payload != null &&
				UseTTLByType(message.TypeId) &&
				message.Payload.ExpirationTicks != -1 &&
				message.Payload.ExpirationTicks < DateTime.Now.Ticks)
			{
				message.Payload = null;
				if (this.deletesEnabled)
				{
					deleteMessage = new RelayMessage(message, MessageType.Delete);
				}
			}

			return deleteMessage;
		}

		/// <summary>
		/// Evaluates the list of <see cref="RelayMessage"/>s for expired TTLs and if
		/// the messages qualifies for deletion a list of delete messages are returned.
		/// </summary>
		/// <param name="messages">The messages to evaluate.</param>
		/// <returns>Returns deletes if there are items to delete; otherwise, returns null.</returns>
		public IList<RelayMessage> ProcessExpiredTTLDeletes(IList<RelayMessage> messages)
		{
			RelayMessage message;
			List<RelayMessage> deletes = null;
			long expirationTicks = DateTime.Now.Ticks;

			for (int i = 0; i < messages.Count; i++)
			{
				message = messages[i];

				if (message.Payload != null &&
					UseTTLByType(message.TypeId) &&
					message.Payload.ExpirationTicks != -1 &&
					message.Payload.ExpirationTicks < expirationTicks)
				{
					message.Payload = null;
					if (this.deletesEnabled)
					{
						if (deletes == null)
						{
							deletes = new List<RelayMessage>(messages.Count);
						}
						deletes.Add(new RelayMessage(message, MessageType.Delete));
					}
				}
			}

			return deletes;
		}

		/// <summary>
		/// Gets the default the default TTL for the given <paramref name="typeId"/>.
		/// </summary>
		/// <param name="typeId">The type id.</param>
		/// <returns>Returns the TTL.</returns>
		private int GetDefaultTTL(short typeId)
		{
			if (typeSettings == null) return -1;

			TTLSetting ttlSettings = typeSettings.GetTTLSettingForId(typeId);
			if (ttlSettings == null || !ttlSettings.Enabled)
			{
				return -1;
			}
			else
			{
				return ttlSettings.DefaultTTLSeconds;
			}
		}

		/// <summary>
		/// Gets a value used to determine if the TTL system of deleting expired types should be used
		/// for the given <paramref name="typeId"/>.
		/// </summary>
		/// <param name="typeId">The <see cref="RelayMessage.TypeId"/></param>
		/// <returns>Returns true if TTL deleting is enabled for the type; otherwise, returns false.</returns>
		private bool UseTTLByType(short typeId)
		{
			if (typeSettings == null) return false;

			TTLSetting ttlSettings = typeSettings.GetTTLSettingForId(typeId);
			if (ttlSettings != null)
			{
				return ttlSettings.Enabled;
			}
			else
			{
				return false;
			}
		}
	}
}
