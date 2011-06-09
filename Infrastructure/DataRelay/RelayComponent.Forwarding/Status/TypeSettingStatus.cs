using System;
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
		/// Initializes a new instance of the <see cref="TypeSettingStatus"/> class.
		/// </summary>
		public TypeSettingStatus()
		{
			MessageInfo = new TypeSpecificMessageCountInfo();
			GatherStatistics = true;
			BulkInMessageInfo = new BulkMessageInfo();
			BulkOutMessageInfo = new BulkMessageInfo();
			TTLSettingStatus = new TTLSettingStatus();
			HydrationPolicyStatus = new HydrationPolicyStatus();
		}

		/// <summary>
		/// Gets or sets the fully qualified name of the class.
		/// </summary>
		[XmlElement("TypeName")]
		public string TypeName { set; get; }
		/// <summary>
		/// Gets or sets the unique identifier.
		/// </summary>
        [XmlElement("TypeId")]
		public short TypeId { set; get; }
		/// <summary>
		/// Gets or sets the status of whether messages should be sent.
		/// If <see langword="true"/>, it indicates that no messages 
		/// of this type will get sent if the command originates 
		/// from the RelayClient.
		/// </summary>
		[XmlElement("Disabled")]
		public bool Disabled { set; get; }
		/// <summary>
		/// Gets or sets the status of whether the <see cref="RelayMessage.Payload"/>
		/// should be compressed.
		/// If <see langword="true"/> it indicates that if the 
		/// RelayClient is used the payload for a <see cref="RelayMessage"/> 
		/// of type <see cref="MessageType.Query"/> is compressed.
		/// </summary>
		[XmlElement("Compress")]
		public bool Compress { set; get; }
		/// <summary>
		/// Gets or sets the caching group to which the class belongs.
		/// </summary>
		[XmlElement("GroupName")]
		public string GroupName { set; get; }
		/// <summary>
		/// Gets or sets the status value that links two <see cref="TypeId"/>s together.  
		/// This is primarily used for IndexCache.
		/// </summary>
		[XmlElement("RelatedIndexTypeId")]
		public short RelatedIndexTypeId { set; get; }
		/// <summary>
		/// Gets or sets a value that is not used.
		/// </summary>
		[XmlElement("CheckRaceCondition")]
		public bool CheckRaceCondition { set; get; }
		/// <summary>
		/// Gets or sets the status for the expiration settings for the class.
		/// </summary>
		[XmlElement("TTLSettingStatus")]
		public TTLSettingStatus TTLSettingStatus { set; get; }
		/// <summary>
		/// Gets or sets whether the client will block.
		/// If <see langword="true"/> it indicates that 
		/// the client will block until the in message 
		/// has been sent to the data relay server.
		/// </summary>
        [XmlElement("SyncInMessages")]
		public bool SyncInMessages { set; get; }
		/// <summary>
		/// Gets or sets a value that is not used.
		/// </summary>
		[XmlElement("ThrowOnSyncFailure")]
		public bool ThrowOnSyncFailure { set; get; }

		/// <summary>
		/// Gets or sets whether type specific statistics will 
		/// be gathered.
		/// True if statistics are to be gathered for the
		/// forwarder on a type specific basis; otherwise false.
		/// </summary>
		[XmlElement("GatherStatistics")]
		public bool GatherStatistics { set; get; }

		/// <summary>
		/// Gets or sets descriptive useful information regarding the type.
		/// Who is responsible for the type is also relevant.  
		/// </summary>
		[XmlElement("Description")]
		public string Description { set; get; }

		/// <summary>
		/// Gets or sets the Hydration policy information for this type.
		/// </summary>
        [XmlElement("HydrationPolicyStatus")]
		public HydrationPolicyStatus HydrationPolicyStatus { set; get; }

		/// <summary>
		/// Gets or sets the type specific message count information.
		/// </summary>
		[XmlElement("MessageInfo")]
		public TypeSpecificMessageCountInfo MessageInfo { set; get; }

		/// <summary>
		/// Gets or sets the <see cref="BulkMessageInfo"/> for in Messages. 
		/// Bulk In refers to saves, deletes, and other updates messages sent as lists
		/// against the relay server (the message is putting data “in” the server. 
		/// </summary>
		[XmlElement("BulkInMessageInfo")]
		public BulkMessageInfo BulkInMessageInfo { set; get; }

		/// <summary>
		/// Gets or sets the <see cref="BulkMessageInfo"/> for out messages.
		/// Bulk Out refers to gets and queries sent as lists against the 
		/// relay server (the message is getting data “out” of the server. 
		/// </summary>
		[XmlElement("BulkOutMessageInfo")]
		public BulkMessageInfo BulkOutMessageInfo { set; get; }
		/// <summary>
		/// Creates a clone of this <see cref="TypeSettingStatus"/>.
		/// </summary>
		/// <returns>
		/// <para>A cloned <see cref="TypeSettingStatus"/> object that shares no object
		///		references as this instance; never <see langword="null"/>.
		/// </para>
		/// </returns>
		public TypeSettingStatus Clone()
		{
			TypeSettingStatus typeSettingStatus = new TypeSettingStatus();

			typeSettingStatus.TypeName = TypeName;
			typeSettingStatus.TypeId = TypeId;
			typeSettingStatus.Disabled = Disabled;
			typeSettingStatus.Compress = Compress;
			typeSettingStatus.GroupName = GroupName;
			typeSettingStatus.RelatedIndexTypeId = RelatedIndexTypeId;
            typeSettingStatus.CheckRaceCondition = CheckRaceCondition;
			typeSettingStatus.GatherStatistics = GatherStatistics;
			typeSettingStatus.Description = Description;
			typeSettingStatus.TTLSettingStatus = this.TTLSettingStatus.Clone();
			typeSettingStatus.SyncInMessages = SyncInMessages;
			typeSettingStatus.ThrowOnSyncFailure = ThrowOnSyncFailure;
			typeSettingStatus.HydrationPolicyStatus = HydrationPolicyStatus.Clone();
			typeSettingStatus.MessageInfo = this.MessageInfo.Clone();
			typeSettingStatus.BulkInMessageInfo = this.BulkInMessageInfo.Clone();
			typeSettingStatus.BulkOutMessageInfo = this.BulkOutMessageInfo.Clone();
			
            return typeSettingStatus;
		}
		/// <summary>
		/// Returns a copy of the <see cref="TypeSettingStatus"/> without items that are not used.
		/// This is to ensure XML output will exclude unused items.
		/// </summary>
		/// <returns>A copy of the <see cref="TypeSettingStatus"/> without items that are not used.</returns>
		internal TypeSettingStatus GetStatus()
		{
			TypeSettingStatus typeSettingStatus = new TypeSettingStatus();

			typeSettingStatus.TypeName = TypeName;
			typeSettingStatus.TypeId = TypeId;
			typeSettingStatus.Disabled = Disabled;
			typeSettingStatus.Compress = Compress;
			typeSettingStatus.GroupName = GroupName;
			typeSettingStatus.RelatedIndexTypeId = RelatedIndexTypeId;
			typeSettingStatus.CheckRaceCondition = CheckRaceCondition;
			typeSettingStatus.TTLSettingStatus = TTLSettingStatus.GetStatus();
			typeSettingStatus.SyncInMessages = SyncInMessages;
			typeSettingStatus.ThrowOnSyncFailure = ThrowOnSyncFailure;
			typeSettingStatus.GatherStatistics = GatherStatistics;
            typeSettingStatus.Description = Description;
			typeSettingStatus.HydrationPolicyStatus = HydrationPolicyStatus.GetStatus();
            typeSettingStatus.BulkInMessageInfo = BulkInMessageInfo.GetStatus();
			typeSettingStatus.BulkOutMessageInfo = BulkOutMessageInfo.GetStatus();
			typeSettingStatus.MessageInfo = MessageInfo.GetStatus();

			return typeSettingStatus;
		}
		/// <summary>
		/// This method populates the <see cref="TypeSettingStatus"/> with
		/// <see cref="TypeSetting"/> information.  If <see cref="TypeSetting"/> is
		/// <see langword="null"/> then nothing is updated.
		/// </summary>
		/// <param name="ts">The <see cref="TypeSetting"/> to convert.</param>
		internal void Update(TypeSetting ts)
		{
			if(ts == null) return;

			TypeName = ts.TypeName;
			TypeId = ts.TypeId;
			Disabled = ts.Disabled;
			Compress = ts.Compress;
			GroupName = ts.GroupName;
			RelatedIndexTypeId = ts.RelatedIndexTypeId;
			CheckRaceCondition = ts.CheckRaceCondition;
			TTLSettingStatus = new TTLSettingStatus();
			TTLSettingStatus.Update(ts.TTLSetting);

			SyncInMessages = ts.SyncInMessages;
			ThrowOnSyncFailure = ts.ThrowOnSyncFailure;
			GatherStatistics = ts.GatherStatistics;
			Description = ts.Description;
			HydrationPolicyStatus = new HydrationPolicyStatus();
			HydrationPolicyStatus.Update(ts.HydrationPolicy);
		}
	}
}
