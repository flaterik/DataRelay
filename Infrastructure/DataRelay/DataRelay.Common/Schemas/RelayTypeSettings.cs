using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

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
		private readonly Dictionary<int, TypeSetting> idMapping = new Dictionary<int, TypeSetting>();

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
			idMapping.Add(item.TypeId, item);
			base.Add(item);
		}

		public new bool Remove(TypeSetting item)
		{
			if (Contains(item))
			{
				idMapping.Remove(item.TypeId);
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
				TypeSetting result;
				idMapping.TryGetValue(typeId, out result);
				return result;
			}
		}

		#endregion
	}

	public class TypeSetting
	{
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
		[XmlElement("GroupName")]
		public string GroupName;
		[XmlElement("RelatedIndexTypeId")]
		public short RelatedIndexTypeId;
		[XmlElement("CheckRaceCondition")]
		public bool CheckRaceCondition;
		[XmlElement("TTLSetting")]
		public TTLSetting TTLSetting;

		[XmlElement("SyncInMessages")]
		public bool SyncInMessages;
		[XmlElement("ThrowOnSyncFailure")]
		public bool ThrowOnSyncFailure;

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

						var type = Type.GetType(_assemblyQualifiedTypeName, false);
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
			return String.Format("{0} {1} Id: {2} {3} {4} {5} {6} {7} HydrationPolicy - {8}",
				TypeName,
				GroupName,
				TypeId,
				Disabled ? "Disabled" : String.Empty,
				Compress ? "Compressed" : String.Empty,
				CheckRaceCondition ? "Checking Race Condition" : String.Empty,
				TTLSetting,
				RelatedIndexTypeId,
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
