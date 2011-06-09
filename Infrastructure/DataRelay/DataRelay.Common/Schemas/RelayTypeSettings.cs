using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using MySpace.Logging;

namespace MySpace.DataRelay.Common.Schemas
{
	#region TypeSettings Classes

	[XmlRoot("TypeSettings", Namespace = "http://myspace.com/RelayTypeSettings.xsd")]
	public class TypeSettings
	{
		internal static readonly Logging.LogWrapper log = new Logging.LogWrapper();
		[XmlArray("TypeSettingCollection")]
		[XmlArrayItem("TypeSetting")]
		public TypeSettingCollection TypeSettingCollection;

		//[XmlAttribute("MaxTypeId")]
		public short MaxTypeId
		{
			get
			{
				if (TypeSettingCollection == null)
				{
					return 0;
				}
				return TypeSettingCollection.MaxTypeId;
			}
		}

		[XmlAttribute("Compressor")]
		public MySpace.Common.IO.CompressionImplementation Compressor = MySpace.Common.IO.Compressor.DefaultCompressionImplementation;
	}

	public class TypeSettingCollection : KeyedCollection<string, TypeSetting>
	{
		//this is used as settings are added or removed to keep track of them...
		private readonly Dictionary<int, TypeSetting> _idMapping = new Dictionary<int, TypeSetting>();
		//but this is actually used for reads because arrays are so much faster than dictionaries
		private TypeSetting[] _typeSettingArray;
		//but we don't want to rebuild it on every change, so we'll keep track of whether it's
		//consistent and check that before reading from it. this first read after a change will
		//trigger building the array. since items tend to be added all at once this should be fine
		//TODO test how this handles config reloads under load!
		private bool _typeSettingArrayConsistent;
		private readonly object _typeSettingArrayLock = new object();

		public short MaxTypeId { get; private set; }

		protected override string GetKeyForItem(TypeSetting item)
		{
			return item.TypeName;
		}

		public new void Add(TypeSetting item)
		{
			if (item.TypeId > MaxTypeId)
			{
				MaxTypeId = item.TypeId;
			}
			_idMapping.Add(item.TypeId, item);
			_typeSettingArrayConsistent = false;
			base.Add(item);
		}

		public new bool Remove(TypeSetting item)
		{
			if (Contains(item))
			{
				_idMapping.Remove(item.TypeId);
				_typeSettingArrayConsistent = false;
			}
			return base.Remove(item);
		}

		public TypeSetting GetTypeMapping(string typeName)
		{
			if (Contains(typeName))
			{
				return this[typeName];
			}
			return null;
		}

		#region Methods for typeId Inputs
		public string GetGroupNameForId(short typeId)
		{
			TypeSetting typeSetting = this[typeId];
			if (typeSetting != null)
			{
				return typeSetting.GroupName;
			}
			return null;
		}

		public TTLSetting GetTTLSettingForId(short typeId)
		{
			TypeSetting typeSetting = this[typeId];
			if (typeSetting != null)
			{
				return typeSetting.TTLSetting;
			}
			return null;
		}

		// Support indexing via TypeId
		public TypeSetting this[short typeId]
		{
			get
			{
				if(typeId > MaxTypeId)
					return null;
				if(!_typeSettingArrayConsistent)
				{
					lock(_typeSettingArrayLock)
					{
						if (!_typeSettingArrayConsistent)
						{
							_typeSettingArray = new TypeSetting[MaxTypeId + 1];
							foreach(TypeSetting typeSetting in _idMapping.Values)
							{
								_typeSettingArray[typeSetting.TypeId] = typeSetting;
							}
							_typeSettingArrayConsistent = true;
						}
					}
				}
				return _typeSettingArray[typeId];
			}
		}

		#endregion
	}

	public class TypeSetting
	{
		private static readonly LogWrapper _log = new LogWrapper();

		private readonly object _syncRoot = new object();
		private string _typeName;
		private string _assemblyQualifiedTypeName;
		private volatile bool _modified;
		private RelayHydrationPolicyAttribute _hydrationPolicy;

		[XmlAttribute("TypeName")]
		public string TypeName
		{
			get
			{
				return _typeName;
			}
			set
			{
				if (_typeName == value) return;
				lock (_syncRoot)
				{
					if (_typeName == value) return;

					_typeName = value;
					_modified = true;
				}
			}
		}

		[XmlElement("TypeId")]
		public short TypeId;
		[XmlElement("Disabled")]
		public bool Disabled;
		[XmlElement("Compress")]
		public bool Compress;
		[XmlElement("LocalCacheTTLSeconds")]
		public int? LocalCacheTTLSeconds;
		[XmlElement("GroupName")]
		public string GroupName;
		[XmlElement("RelatedIndexTypeId")]
		public short RelatedIndexTypeId;
		[XmlElement("CheckRaceCondition")]
		public bool CheckRaceCondition;
		[XmlElement("TTLSetting")]
		public TTLSetting TTLSetting;

        [XmlElement("FlexCacheMode")]
        public FlexCacheMode FlexCacheMode;

		[XmlElement("SyncInMessages")]
		public bool SyncInMessages;
		[XmlElement("ThrowOnSyncFailure")]
		public bool ThrowOnSyncFailure;

		[XmlAttribute("GatherStatistics")]
		public bool GatherStatistics = true;//default to true
		[XmlElement("Description")]
		public string Description;

		/// <summary>
		/// Gets or sets the assembly qualified type name of the target object.
		/// </summary>
		/// <value>The assembly qualified type name of the target object.</value>
		[XmlElement("AssemblyQualifiedTypeName")]
		public string AssemblyQualifiedTypeName
		{
			get
			{
				return _assemblyQualifiedTypeName;
			}
			set
			{
				if (_assemblyQualifiedTypeName == value) return;
				lock (_syncRoot)
				{
					if (_assemblyQualifiedTypeName == value) return;

					_assemblyQualifiedTypeName = value;
					_modified = true;
				}
			}
		}

		/// <summary>
		/// 	<para>Gets the hydration policy for this type.</para>
		/// </summary>
		/// <value>
		/// 	<para>The hydration policy for this type.</para>
		/// </value>
		public IRelayHydrationPolicy HydrationPolicy
		{
			get
			{
				if (!_modified) return _hydrationPolicy;
				lock (_syncRoot)
				{
					if (!_modified) return _hydrationPolicy;

					_hydrationPolicy = null;

					try
					{
						if (string.IsNullOrEmpty(_assemblyQualifiedTypeName)) return _hydrationPolicy;
						Type type;
						try
						{
							type = Type.GetType(_assemblyQualifiedTypeName, false);
						}
						catch (Exception ex)
						{
							type = null;
							_log.Error("Failed to load type. Error = " + ex);
						}

						if (type == null) return _hydrationPolicy;

						var policies = (RelayHydrationPolicyAttribute[])Attribute.GetCustomAttributes(type, typeof(RelayHydrationPolicyAttribute));

						if (policies == null || policies.Length == 0) return _hydrationPolicy;

						foreach (var policy in policies)
						{
							if (policy.RelayTypeName == TypeName)
							{
								return _hydrationPolicy = policy;
							}
						}

						return _hydrationPolicy;
					}
					finally
					{
						_modified = false;
					}
				}
			}
		}

		public override string ToString()
		{
			var hydrationPolicy = HydrationPolicy;
			return String.Format("{0} {1} Id: {2} {3} {4} {5} {6} {7} {8} HydrationPolicy - {9}",
				TypeName,
				GroupName,
				TypeId,
				Disabled ? "Disabled" : String.Empty,
				Compress ? "Compressed" : String.Empty,
				CheckRaceCondition ? "Checking Race Condition" : String.Empty,
				TTLSetting,
				RelatedIndexTypeId,
				LocalCacheTTLSeconds.HasValue ? LocalCacheTTLSeconds.Value.ToString() : "null",
				hydrationPolicy == null
					? "None"
					: string.Format(
					"KeyType=\"{0}\", HydrateMisses=\"{1}\", HydrateBulkMisses=\"{2}\"",
					hydrationPolicy.KeyType,
					(hydrationPolicy.Options & RelayHydrationOptions.HydrateOnMiss) == RelayHydrationOptions.HydrateOnMiss,
					(hydrationPolicy.Options & RelayHydrationOptions.HydrateOnBulkMiss) == RelayHydrationOptions.HydrateOnBulkMiss));
		}
	}

	public class TTLSetting
	{
		[XmlElement("Enabled")]
		public bool Enabled;
		[XmlElement("DefaultTTLSeconds")]
		public int DefaultTTLSeconds;

		public override string ToString()
		{
			if (Enabled)
			{
				return string.Format("Default TTL {0} seconds", DefaultTTLSeconds);
			}
			return "No Default TTL";
		}
	}

    /// <summary>
    /// Controls if a type is stored in Flex Cache, Data Relay, or both.
    /// </summary>
    public enum FlexCacheMode
    {
        /// <summary>
        /// Disabled Flex Cache for this relay type.  This is the default setting if omitted.  Set Disabled to 0 to force the default value.
        /// </summary>
        Disabled = 0, 

        /// <summary>
        /// Enables Flex Cache for this relay type.
        /// </summary>
        Enabled, 

        /// <summary>
        /// Enables Flex Cache for this relay type, and will drain objects from classic relay nodes.
        /// Read Miss will fall back to classic relay nodes, and save the data to Flex Cache, and then delete from Data Relay 
        /// All Updates will update Flex Cache and issue deletes to Data Relay.
        /// </summary>
        EnabledWithRelayDrain
    }
	#endregion

	#region ConfigLoader for Legacy config file

	public static class TypeSettingsConfigLoader
	{
		public static TypeSettings Load(string basePath, XmlNode sectionNode)
		{

			if (TypeSettings.log.IsWarnEnabled)
				TypeSettings.log.WarnFormat("Attempting Load of Legacy 'RelayTypeSettings' config file. Consider updating the file so it conforms to the new RelayTypeSettings Schema. Config basePath: {0}", basePath);

			TypeSettings typeSettings = null;
			string configSource = string.Empty;
			string path = string.Empty;
			try
			{
				configSource = sectionNode.Attributes["configSource"].Value;
				if (!String.IsNullOrEmpty(configSource))
				{
					path = Path.Combine(Path.GetDirectoryName(basePath), configSource);
					XmlDocument TypeSettingsConfig = new XmlDocument();
					TypeSettingsConfig.Load(path);
					typeSettings = CreateTypeSettings(TypeSettingsConfig);
				}
			}
			catch (Exception ex)
			{
				if (TypeSettings.log.IsErrorEnabled)
					TypeSettings.log.ErrorFormat("Error loading config file for source: {0}, path: {1}: {2}", configSource, path, ex);
			}

			return typeSettings;
		}

		private static TypeSettings CreateTypeSettings(XmlDocument TypeSettingsConfig)
		{
			XmlNamespaceManager NamespaceMgr = new XmlNamespaceManager(TypeSettingsConfig.NameTable);
			NamespaceMgr.AddNamespace("ms", "http://myspace.com/RelayTypeSettings.xsd");

			TypeSettings typeSettings = new TypeSettings();
			typeSettings.TypeSettingCollection = new TypeSettingCollection();

			foreach (XmlNode TypeNameMapping in TypeSettingsConfig.SelectSingleNode("//ms:TypeNameMappings", NamespaceMgr))
			{
				// avoid comments
				if (TypeNameMapping is XmlElement)
				{
					TypeSetting typeSetting = new TypeSetting();

					try
					{
						#region TypeName, TypeId (Required)
						typeSetting.TypeName = TypeNameMapping.Attributes["TypeName"].Value;
						typeSetting.TypeId = short.Parse(GetSafeChildNodeVal(TypeNameMapping, "ms:TypeId", NamespaceMgr));
						#endregion

						#region Disabled, Compress (Not Required)
						bool.TryParse(GetSafeChildNodeVal(TypeNameMapping, "ms:Disabled", NamespaceMgr), out typeSetting.Disabled);
						bool.TryParse(GetSafeChildNodeVal(TypeNameMapping, "ms:Compress", NamespaceMgr), out typeSetting.Compress);
						#endregion

						#region GroupName (Required)
						XmlNode TypeIdMapping = TypeSettingsConfig.DocumentElement.SelectSingleNode("//ms:TypeIdMapping[@TypeId=" + typeSetting.TypeId + "]", NamespaceMgr);
						typeSetting.GroupName = TypeIdMapping.SelectSingleNode("ms:GroupName", NamespaceMgr).InnerText;
						#endregion

						#region CheckRaceCondition (Not Required)
						// legacy config file does not provide this value
						bool.TryParse(GetSafeChildNodeVal(TypeNameMapping, "ms:CheckRaceCondition", NamespaceMgr), out typeSetting.CheckRaceCondition);
						#endregion

						#region TTLSetting : Enabled, DefaultTTLSeconds  (Not required)
						typeSetting.TTLSetting = new TTLSetting();
						XmlNode TTLSettingConfig = TypeSettingsConfig.DocumentElement.SelectSingleNode("//ms:TTLSetting[@TypeId=" + typeSetting.TypeId + "]", NamespaceMgr);
						if (TTLSettingConfig != null)
						{
							bool.TryParse(GetSafeChildNodeVal(TTLSettingConfig, "ms:Enabled", NamespaceMgr), out typeSetting.TTLSetting.Enabled);

							if (typeSetting.TTLSetting.Enabled)
							{
								int.TryParse(GetSafeChildNodeVal(TTLSettingConfig, "ms:DefaultTTLSeconds", NamespaceMgr), out typeSetting.TTLSetting.DefaultTTLSeconds);
								typeSetting.TTLSetting.DefaultTTLSeconds = (typeSetting.TTLSetting.DefaultTTLSeconds == 0) ? -1 : typeSetting.TTLSetting.DefaultTTLSeconds;
							}
							else
							{
								typeSetting.TTLSetting.DefaultTTLSeconds = -1;
							}
						}
						else
						{
							// set defaults
							typeSetting.TTLSetting.Enabled = false;
							typeSetting.TTLSetting.DefaultTTLSeconds = -1;
						}
						#endregion

						// add to collection
						typeSettings.TypeSettingCollection.Add(typeSetting);
					}
					catch (Exception ex)
					{
						if (TypeSettings.log.IsErrorEnabled)
							TypeSettings.log.ErrorFormat("Error loading TypeSetting for TypeName {0}, TypeID {1}: {2}", typeSetting.TypeName, typeSetting.TypeId, ex);
					}
				}
			}

			return typeSettings;
		}

		private static string GetSafeChildNodeVal(XmlNode Node, string ChildNodeName, XmlNamespaceManager NamespaceMgr)
		{
			string ChildNodeVal = string.Empty;
			if (Node != null && !string.IsNullOrEmpty(ChildNodeName))
			{
				XmlNode childNode = Node.SelectSingleNode(ChildNodeName, NamespaceMgr);
				if (childNode != null)
				{
					ChildNodeVal = childNode.InnerText;
				}
			}
			return ChildNodeVal;
		}
	}

	#endregion
}
