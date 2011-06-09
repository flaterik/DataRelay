using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using MySpace.Common.Barf;

namespace MySpace.Common.IO
{
	/// <summary>
	/// Stores serialization information for a specific type
	/// </summary>
	[DebuggerDisplay("{type.Name} Version={currentVersion} Properties={properties.Count}")]
	internal class TypeSerializationInfo : IComparer<PropertySerializationInfo>
	{
		private static readonly Factory<Type, TypeSerializationInfo> _typeInfoByType;
		private static readonly TypeSerializationInfo _versionSerializable;
		private static readonly TypeSerializationInfo _customSerializable;

		static TypeSerializationInfo()
		{
			_typeInfoByType = Algorithm.LazyIndexer<Type, TypeSerializationInfo>(type => new TypeSerializationInfo(type));
			_versionSerializable = new TypeSerializationInfo(LegacySerializationType.Version);
			_customSerializable = new TypeSerializationInfo(LegacySerializationType.Custom);
		}

		internal static TypeSerializationInfo GetTypeInfo(Type type)
		{
			if (type == null) throw new ArgumentNullException("type");

			return _typeInfoByType(type);
		}

		internal static TypeSerializationInfo VersionSerializable
		{
			[DebuggerStepThrough]
			get { return _versionSerializable; }
		}

		internal static TypeSerializationInfo CustomSerializable
		{
			[DebuggerStepThrough]
			get { return _customSerializable; }
		}

		#region Constants

		internal enum LegacySerializationType
		{
			Custom,
			Version,
			Unknown
		}
		#endregion

		#region Types
		delegate void SerializeMethod(object instance, TypeSerializationArgs args);
		delegate bool DeserializeMethod(ref object instance, TypeSerializationArgs args);
		delegate void AutoSerializeMethod(object instance, TypeSerializationArgs args);
		delegate void AutoDeserializeMethod(object instance, int version, TypeSerializationArgs args);
		delegate bool CompareMethod(object a, object b);
		#endregion

		#region Members
		Type type = null;
		int currentVersion = 0;
		SerializableClassAttribute attribute = null;
		SerializeMethod serializeMethod = null;
		DeserializeMethod deserializeMethod = null;
		AutoSerializeMethod autoSerializeMethod = null;
		LazyInitializer<AutoDeserializeMethod> autoDeserializeMethod = null;
		CompareMethod compareMethod = null;
		bool versionSerializable = false;
		bool supportsSerializationInfo = false;
		Type serializableBaseType = null;
		Action deferredInitializationAction;
		readonly object syncRoot = new object();
		readonly LazyInitializer<IBarfSerializer> _barfSerializer;
		#endregion

		#region Construction

		/// <summary>
		/// Initializes a new instance of the TypeSerializationInfo class for
		/// legacy serialization mechanisms. For internal use only.
		/// </summary>
		/// <param name="legacyType"></param>
		internal TypeSerializationInfo(LegacySerializationType legacyType)
		{
			deferredInitializationAction = () =>
			{
				switch (legacyType)
				{
					case LegacySerializationType.Version:
						this.serializeMethod = SerializeVersionSerializable;
						this.deserializeMethod = DeserializeVersionSerializable;
						break;

					case LegacySerializationType.Custom:
						this.serializeMethod = SerializeCustomSerializable;
						this.deserializeMethod = DeserializeCustomSerializable;
						break;

					case LegacySerializationType.Unknown:
						this.serializeMethod = SerializeUnknownType;
						this.deserializeMethod = DeserializeUnknownType;
						break;
				}

				this.compareMethod = object.Equals;
			};
		}

		/// <summary>
		/// Initializes a new instance of the TypeSerializationInfo class.
		/// </summary>
		/// <param name="type">Type info</param>
		public TypeSerializationInfo(Type type)
		{
			object[] attributes = type.GetCustomAttributes(typeof(SerializableClassAttribute), true);

			//  Check input parameters
			if (type == null)
			{
				throw new ArgumentNullException("type");
			}
			else if (type.IsInterface)
			{
				throw new ArgumentException("Type cannot be an interface", "type");
			}

			this.type = type;
			_barfSerializer = new LazyInitializer<IBarfSerializer>(() => BarfSerializers.Get(this.type, PartFlags.None));
			this.versionSerializable = type.GetInterface(typeof(IVersionSerializable).Name) != null;

			//  Cache whether this type supports ISerializationInfo for forward-compatibility
			//  Note that this only indicates if this specific type supports ISerializationInfo
			//  Forward-compatibility will still be supported if this or any derived classes
			//  implement the necessary interface
			this.supportsSerializationInfo = type.GetInterface(typeof(ISerializationInfo).Name) != null;

			attributes = type.GetCustomAttributes(typeof(SerializableClassAttribute), true);

			deferredInitializationAction = () =>
			{
				if (attributes.Length > 0)
				{
					this.attribute = attributes[0] as SerializableClassAttribute;
					InitializeAutoSerializable();
				}
				else
				{
					InitializeLegacySerializable();
				}
			};
		}

		void InitializeLegacySerializable()
		{
			if (this.versionSerializable == true)
			{
				this.serializeMethod = SerializeVersionSerializable;
				this.deserializeMethod = DeserializeVersionSerializable;
			}
			else if (this.type.GetInterface(typeof(ICustomSerializable).Name) != null)
			{
				this.serializeMethod = SerializeCustomSerializable;
				this.deserializeMethod = DeserializeCustomSerializable;
			}
			else
			{
				this.serializeMethod = SerializeUnknownType;
				this.deserializeMethod = DeserializeUnknownType;
			}

			this.compareMethod = object.Equals;
		}

		[Conditional("DEBUG")]
		void DumpProperties(List<PropertySerializationInfo> properties)
		{
			int version = 0;

			Debug.WriteLine(string.Format(
					  "Type {0} version {1} has {2} properties for serialization:",
					  this.type,
					  this.currentVersion,
					  properties.Count
					  ));

			foreach (PropertySerializationInfo p in properties)
			{
				if (version < p.Version)
				{
					Debug.WriteLine(string.Format("  Version {0}", p.Version));
					version++;
				}
				Debug.WriteLine(string.Format("    - {0} {1}", p.Type, p.Name));
			}
		}

		void InitializeAutoSerializable()
		{
			List<PropertySerializationInfo> properties = new List<PropertySerializationInfo>();
			int obsoleteVersion = 0;

			//  Check if we need to automatically serialize base class members. If so then find
			//  the first base class with the SerializableClass attribute and save its serialization
			//  info for quick access
			if (this.Attribute.SerializeBaseClass == true)
			{
				if (this.Attribute.Inline)
				{
					throw new ApplicationException("SerializableClass: Inline option cannot be used with the SerializeBase option");
				}

				for (
					 Type baseType = this.type.BaseType;
					 baseType != null;
					 baseType = baseType.BaseType
					 )
				{
					if (SerializableClassAttribute.HasAttribute(baseType))
					{
						this.serializableBaseType = baseType;
						break;
					}
				}
			}

			//  Create an ordered list of serializable properties. Get the version from the last
			//  property in the ordered list. Note that in the case of a hierarchy this typeinfo
			//  only serializes the uninherited properties. Base classes will be serialized
			//  before this class using their own serialize/deserialize methods
			BuildPropertyList(properties, out obsoleteVersion);

			if (properties.Count > 0)
			{
				properties.Sort(this);
				this.currentVersion = Math.Max(properties[properties.Count - 1].Version, obsoleteVersion);

				if (this.IsInline && (this.currentVersion > 1))
				{
					throw new NotSupportedException(string.Format("All properties for inline classes must be version 1 ({0})", this.Type.FullName));
				}

				DumpProperties(properties);
			}

			//  If legacy version is set then all properties must have a higher version
			if (this.LegacyVersion > 0)
			{
				PropertySerializationInfo badProp = null;

				badProp = properties.Find(p => p.Version <= this.LegacyVersion);
				if (badProp != null)
				{
					throw new InvalidOperationException(string.Format(
									"Class {0} with legacy version {1} contains property {2} with version {3}",
									new object[] {
                                    this.Type.Name,
                                    this.LegacyVersion,
                                    badProp.Name,
                                    badProp.Version
                                }));
				}
			}

			//  If MinVersion is higher than the property versions, then the "current" version
			//  is equal to the minimum version. Developers can use this to prevent issues with
			//  breaking changes that don't add new properties. For example, if the base class
			//  hierarchy is changed in some way then the developer must use this to increase the
			//  class' version and prevent the serializaer from loading older versions.
			//  
			//  Workitems:
			//  Sprint Backlog Item 40877: Serialization: Allow MinVersion to be higher than property versions
			//
			if (this.MinVersion > this.currentVersion)
			{
				this.currentVersion = this.MinVersion;
			}

			CreateSerializationMethod(properties);
			CreateDeserializationMethod(properties);
			CreateCompareMethod(properties);
		}

		void BuildPropertyList(
				  List<PropertySerializationInfo> properties,
				  out int obsoleteVersion
				  )
		{
			obsoleteVersion = 0;

			//  If class has SerializableProperty attribute then add Indexer
			object[] attributes = this.type.GetCustomAttributes(typeof(SerializablePropertyAttribute), false);

			foreach (SerializablePropertyAttribute attrib in attributes)
			{
				PropertySerializationInfo propInfo = null;

				if (attrib is SerializableInheritedPropertyAttribute)
				{
					SerializableInheritedPropertyAttribute inherit = (SerializableInheritedPropertyAttribute)attrib;

					if (this.serializableBaseType != null)
					{
						throw new ApplicationException(string.Format(
										"The SerializableInheritedProperty attribute cannot be used with SerializeBaseClass=true ({0})",
										this.type.FullName
										));
					}

					if (this.type.IsSubclassOf(inherit.BaseClass) == false)
					{
						throw new ApplicationException(string.Format(
										"Invalid SerializableInheritedProperty attribute: {0} is not a base class of {1}",
										this.type.Name,
										inherit.BaseClass.Name
										));
					}

					MemberInfo[] prop = inherit.BaseClass.GetMember(
													inherit.BasePropertyName,
													MemberTypes.Field | MemberTypes.Property,
													BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly
													);

					if (prop.Length == 0)
					{
						throw new ApplicationException(string.Format(
										"Failed to located property \"{0}\" inherited from base class \"{1}\"",
										inherit.BasePropertyName,
										inherit.BaseClass.Name
										));
					}

					propInfo = new PropertySerializationInfo(attrib, prop[0]);
				}
				else
				{
					propInfo = new PropertySerializationInfo(attrib, this.type);
				}

				properties.Add(propInfo);
				obsoleteVersion = Math.Max(obsoleteVersion, propInfo.ObsoleteVersion);
			}

			//  Get members. We don't need to worry about the member type
			//  because only valid member types can have the SerializableProperty attribute
			foreach (MemberInfo prop in this.type.GetMembers(
																 BindingFlags.Public
																 | BindingFlags.NonPublic
																 | BindingFlags.Instance
																 | BindingFlags.DeclaredOnly
																 ))
			{
				PropertySerializationInfo propInfo = null;
				SerializablePropertyAttribute attrib = null;

				attrib = SerializablePropertyAttribute.GetAttribute(prop);
				if (attrib == null)
					continue;

				var property = prop as PropertyInfo;

				if (property != null)
				{
					if (!(property.CanRead && property.CanWrite))
					{
						throw new SerializationException(string.Format("{0}.{1} must define a getter and a setter.", type.Name, property.Name));
					}
				}

				propInfo = new PropertySerializationInfo(attrib, prop);
				properties.Add(propInfo);
				obsoleteVersion = Math.Max(obsoleteVersion, propInfo.ObsoleteVersion);
			}

		}
		#endregion // Construction

		#region Properties
		//*********************************************************************
		//
		//  Properties
		//
		//*********************************************************************

		/// <summary>
		/// Gets the current version of the class
		/// </summary>
		public int CurrentVersion
		{
			get { return this.currentVersion; }
		}

		/// <summary>
		/// Deprecated. Has no effect.
		/// </summary>
		[Obsolete("Volatile has no effect on serialization.", false)]
		public bool Volatile
		{
			get { return this.attribute.Volatile; }
		}

		/// <summary>
		/// Gets the minimum version that this class can deserialize. 
		/// See <see cref="SerializableClassAttribute.MinVersion"/>.
		/// </summary>
		public int MinVersion
		{
			get { return this.attribute.MinVersion; }
		}

		/// <summary>
		/// Gets the minimum version that a serialized stream can be to allow deserialization. 
		/// See <see cref="SerializableClassAttribute.MinDeserializeVersion"/>.
		/// </summary>
		public int MinDeserializeVersion
		{
			get { return this.attribute.MinDeserializeVersion; }
		}

		/// <summary>
		/// Returns the type that this serialization information is for
		/// </summary>
		public Type Type
		{
			get { return this.type; }
		}

		/// <summary>
		/// Returns the <seealso cref="SerializableClassAttribute"/> attribute for the type
		/// </summary>
		public SerializableClassAttribute Attribute
		{
			get { return this.attribute; }
		}

		/// <summary>
		/// Returns true if this is an inline class
		/// </summary>
		public bool IsInline
		{
			get { return this.attribute.Inline; }
		}

		/// <summary>
		/// Returns the last version of the class that used
		/// legacy serialization. Default is zero.
		/// </summary>
		public int LegacyVersion
		{
			get { return this.attribute.LegacyVersion; }
		}

		#endregion

		#region Methods
		public void Serialize(object instance, TypeSerializationArgs args)
		{
			if (serializeMethod == null)
			{
				lock (syncRoot)
				{
					if (serializeMethod == null)
					{
						deferredInitializationAction();
						deferredInitializationAction = null;
					}
				}
			}
			if (attribute != null && attribute.SuppressWarningExceptions)
			{
				using (Serializer.OpenSuppressAlertScope())
				{
					serializeMethod(instance, args);
				}
			}
			else
			{
				this.serializeMethod(instance, args);
			}
		}

		public bool Deserialize(ref object instance, TypeSerializationArgs args)
		{
			if (deserializeMethod == null)
			{
				lock (syncRoot)
				{
					if (deserializeMethod == null)
					{
						deferredInitializationAction();
						deferredInitializationAction = null;
					}
				}
			}
			if (attribute != null && attribute.SuppressWarningExceptions)
			{
				using (Serializer.OpenSuppressAlertScope())
				{
					return this.deserializeMethod(ref instance, args);
				}
			}
			return this.deserializeMethod(ref instance, args);
		}

		public bool Compare(object x, object y)
		{
			if (compareMethod == null)
			{
				lock (syncRoot)
				{
					if (compareMethod == null)
					{
						deferredInitializationAction();
						deferredInitializationAction = null;
					}
				}
			}
			if (attribute != null && attribute.SuppressWarningExceptions)
			{
				using (Serializer.OpenSuppressAlertScope())
				{
					return this.compareMethod(x, y);
				}
			}
			return this.compareMethod(x, y);
		}

		internal static TypeSerializationInfo GetTypeInfo<T>(object instance)
		{
			Type t = typeof(T);

			if ((object)instance == null)
			{
				return GetTypeInfo(t);
			}
			else if (instance is IVersionSerializable)
			{
				return VersionSerializable;
			}
			else if (instance is ICustomSerializable)
			{
				return CustomSerializable;
			}
			else
			{
				return GetTypeInfo(instance.GetType());
			}
		}

		#endregion

		#region Helper Methods
		void CreateSerializationMethod(List<PropertySerializationInfo> properties)
		{
			if (attribute.MinWriteFrameworkVersion >= 1)
			{
				serializeMethod = (o, args) =>
				{
					_barfSerializer.Value.SerializeObject(o, new BarfSerializationArgs(args.Writer));
				};
			}
			else
			{
				PropertySerializationInfo.GenerateArgs args = new PropertySerializationInfo.GenerateArgs();
				FieldInfo nameTableField = typeof(TypeSerializationArgs).GetField("NameTable");

				const string CreatedNameTableVar = "_createdNameTable";
				const string HasNameTableLabel = "_hasNameTable";
				const string SkipNameTableSerializeLabel = "_skipNameTableSerialize";
				const string SerializationInfoVar = "_serializationInfo";
				const string SkipUnhandledDataLabel = "_skipUnhandledData";

				args.il = new DynamicMethodHelper(
												"Serialize_" + type.Name,
												null,
												new Type[] { typeof(object), typeof(TypeSerializationArgs) },
												this.type
												);

				args.instanceArg = 0;
				args.dataArg = 1;
				args.streamVar = "_writer";
				args.headerVar = "_header";

				//  IPrimitiveWriter writer = dataArg.Writer;
				args.il.DeclareLocal(args.streamVar, typeof(IPrimitiveWriter));
				args.il.GetField(args.dataArg, typeof(TypeSerializationArgs).GetField("Writer"));
				args.il.PopLocal(args.streamVar);

				//  TypeSerializationHeader _header = dataArg.Header
				args.il.DeclareLocal(args.headerVar, typeof(TypeSerializationHeader));
				args.il.GetField(args.dataArg, typeof(TypeSerializationArgs).GetField("Header"));
				args.il.PopLocal(args.headerVar);

				//  SerializationInfo _serializationInfo = dataArg.SerializationInfo
				args.il.DeclareLocal(SerializationInfoVar, typeof(SerializationInfo));
				args.il.GetField(args.dataArg, typeof(TypeSerializationArgs).GetField("SerializationInfo"));
				args.il.PopLocal(SerializationInfoVar);

				//  bool createdNameTable
				//  TypeNameTable _typeNameTable = dataArg.NameTable;
				//
				//  if (_typeNameType == null)
				//  {
				//      _typeNameType = new TypeNameTable();
				//      dataArg.NameTable = _typeNameType;
				//      createdNameTable = true;
				//  }
				args.il.DeclareLocal(CreatedNameTableVar, typeof(bool));

				args.nameTableVar = "_typeNameTable";
				args.il.DeclareLocal(args.nameTableVar, typeof(TypeNameTable));
				args.il.GetField(args.dataArg, nameTableField);
				args.il.PopLocal(args.nameTableVar);

				args.il.PushLocal(args.nameTableVar);
				args.il.GotoIfTrue(HasNameTableLabel);

				args.il.PushBool(true);
				args.il.PopLocal(CreatedNameTableVar);

				args.il.NewObject(args.nameTableVar);
				args.il.SetField(args.dataArg, args.nameTableVar, nameTableField);

				args.il.BeginCallMethod(args.nameTableVar, "Add", new Type[] { typeof(SerializationInfo) });
				args.il.PushLocal(SerializationInfoVar);
				args.il.CallMethod();

				args.il.MarkLabel(HasNameTableLabel);

				//  If the type automatically serializes its base class
				//  then write it now. This must be done after the derived class
				//  has a chance to create the name table
				if (this.serializableBaseType != null)
				{
					FieldInfo fi = typeof(TypeSerializationArgs).GetField("IsBaseClass");

					args.il.PushArg(args.dataArg);
					args.il.PushBool(true);
					args.il.SetField(fi);

					args.il.PushArg(args.instanceArg);
					args.il.PushArg(args.dataArg);
					args.il.CallMethod(PropertySerializationInfo.GetTypeMethod(this.serializableBaseType, PropertySerializationInfo.TypeMethodType.Serialize));

					args.il.PushArg(args.dataArg);
					args.il.PushBool(false);
					args.il.SetField(fi);
				}

				//  Create calls to serialize each member
				foreach (PropertySerializationInfo propInfo in properties)
				{
					//  Don't serialize this property if it's marked obsolete
					//  and the min version is equal or greater than the
					//  version it was made obsolete in.
					if ((propInfo.ObsoleteVersion > 0) && (this.MinVersion >= propInfo.ObsoleteVersion))
					{
						continue;
					}

					args.il.BeginScope();
					propInfo.GenerateWriteIL(args);
					args.il.EndScope();
				}

				if (this.IsInline == false)
				{
					//  Write saved unhandled data if current version is lower
					//  than the data we original deserialized from
					args.il.PushInt(this.CurrentVersion);
					args.il.CallMethod(args.headerVar, typeof(TypeSerializationHeader).GetProperty("DataVersion").GetGetMethod());
					args.il.GotoIfGreaterOrEqual(SkipUnhandledDataLabel);

					args.il.BeginCallMethod(args.streamVar, "Write", new Type[] { typeof(byte[]) });
					args.il.CallMethod(SerializationInfoVar, typeof(SerializationInfo).GetProperty("UnhandledData").GetGetMethod());
					args.il.CallMethod();

					args.il.MarkLabel(SkipUnhandledDataLabel);

					//  Update header with actual data length
					args.il.BeginCallMethod(args.headerVar, "UpdateDataLength", new Type[] { typeof(IPrimitiveWriter) });
					args.il.PushLocal(args.streamVar);
					args.il.CallMethod();

					//  if (createdNameTable)
					//  {
					//      _typeNameTable.Serialize();     -- Will do nothing if the table is empty
					//  }
					args.il.PushLocal(CreatedNameTableVar);
					args.il.GotoIfFalse(SkipNameTableSerializeLabel);

					args.il.BeginCallMethod(args.nameTableVar, "Serialize", new Type[] { typeof(IPrimitiveWriter) });
					args.il.PushLocal(args.streamVar);
					args.il.CallMethod();

					args.il.MarkLabel(SkipNameTableSerializeLabel);
				}

				args.il.Return();

				this.autoSerializeMethod = (AutoSerializeMethod)args.il.Compile(typeof(AutoSerializeMethod));
				this.serializeMethod = SerializeAutoSerializable;
			}
		}

		void CreateDeserializationMethod(List<PropertySerializationInfo> properties)
		{
			autoDeserializeMethod = new LazyInitializer<AutoDeserializeMethod>(() => CreateAutoDeserializeMethod(properties));
			this.deserializeMethod = DeserializeAutoSerializable;
		}

		AutoDeserializeMethod CreateAutoDeserializeMethod(List<PropertySerializationInfo> properties)
		{
			const string HasNameTableLabel = "_hasNameTable";
			const string endLabel = "exitFunction";
			const string SkipPropertyLabel = "_skipProperty";
			const string BaseSucceededLabel = "_baseSucceeded";
			const string SkipLegacyDeserialize = "_skipLegacyDeserialize";

			int version = 0;
			PropertySerializationInfo.GenerateArgs args = new PropertySerializationInfo.GenerateArgs();

			args.il = new DynamicMethodHelper(
												 "Deserialize_" + type.Name,
												 null,
												 new Type[] { typeof(object), typeof(int), typeof(TypeSerializationArgs) },
												 this.type
												 );

			args.instanceArg = 0;
			args.versionArg = 1;
			args.dataArg = 2;
			args.streamVar = "_reader";
			args.headerVar = "_header";

			args.il.DeclareLocal(args.streamVar, typeof(IPrimitiveReader));
			args.il.GetField(args.dataArg, typeof(TypeSerializationArgs).GetField("Reader"));
			args.il.PopLocal(args.streamVar);

			args.il.DeclareLocal(args.headerVar, typeof(TypeSerializationHeader));
			args.il.GetField(args.dataArg, typeof(TypeSerializationArgs).GetField("Header"));
			args.il.PopLocal(args.headerVar);

			//  Handle legacy deserialization
			if ((this.LegacyVersion > 0) && (this.MinVersion <= this.LegacyVersion))
			{
				MethodInfo method = this.Type.GetMethod(
												"Deserialize",
												BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
												null,
												new Type[] { typeof(IPrimitiveReader), typeof(int) },
												null
												);

				if (method == null)
				{
					throw new ApplicationException(string.Format(
									"Class {0} specified a legacy version but could not find the Deserialize method",
									this.Type.Name
									));
				}

				//
				//  if (dataVersion > legacyVersion)
				//    goto SkipLegacyDeserialize
				//
				//      call instance.Deserialize(reader, version)
				//      goto endLabel
				//
				//  :SkipLegacyDeserialize
				//
				args.il.PushArg(args.versionArg);
				args.il.PushInt(this.LegacyVersion);
				args.il.GotoIfGreater(SkipLegacyDeserialize);

				args.il.PushArg(args.instanceArg);
				args.il.PushLocal(args.streamVar);
				args.il.PushArg(args.versionArg);
				args.il.CallMethod(method);

				args.il.Goto(endLabel);
				args.il.MarkLabel(SkipLegacyDeserialize);
			}

			//  TypeNameTable _typeNameTable = dataArg.NameTable;
			//  if (_typeNameType == null)
			//      _typeNameType = new TypeNameTable();
			//      dataArg.NameTable = _typeNameType;
			args.nameTableVar = "_typeNameTable";
			args.il.DeclareLocal(args.nameTableVar, typeof(TypeNameTable));
			args.il.GetField(args.dataArg, typeof(TypeSerializationArgs).GetField("NameTable"));
			args.il.PopLocal(args.nameTableVar);

			args.il.PushLocal(args.nameTableVar);
			args.il.GotoIfTrue(HasNameTableLabel);

			args.il.NewObject(args.nameTableVar);
			args.il.SetField(args.dataArg, args.nameTableVar, typeof(TypeSerializationArgs).GetField("NameTable"));

			args.il.BeginCallMethod(args.nameTableVar, "Deserialize", new Type[] { typeof(IPrimitiveReader), typeof(TypeSerializationHeader) });
			args.il.PushLocal(args.streamVar);
			args.il.PushLocal(args.headerVar);
			args.il.CallMethod();

			args.il.BeginCallMethod(args.nameTableVar, "GetSerializationInfo", new Type[] { typeof(SerializationInfo), typeof(int) });
			args.il.GetField(args.dataArg, typeof(TypeSerializationArgs).GetField("SerializationInfo"));
			args.il.PushInt(this.CurrentVersion);
			args.il.CallMethod();

			args.il.MarkLabel(HasNameTableLabel);

			//  If the type automatically serializes its base class
			//  then read it now. This must be done after the derived class
			//  has a chance to load the name table
			if (this.serializableBaseType != null)
			{
				FieldInfo fi = typeof(TypeSerializationArgs).GetField("IsBaseClass");

				args.il.PushArg(args.dataArg);
				args.il.PushBool(true);
				args.il.SetField(fi);

				//  Set IsBaseClass field to ensure proper type mapping
				args.il.PushArgAsRef(args.instanceArg);
				args.il.PushArg(args.dataArg);
				args.il.CallMethod(PropertySerializationInfo.GetTypeMethod(this.serializableBaseType, PropertySerializationInfo.TypeMethodType.Deserialize));

				args.il.GotoIfTrue(BaseSucceededLabel);

				args.il.PushArg(args.dataArg);
				args.il.PushBool(false);
				args.il.SetField(typeof(TypeSerializationArgs).GetField("Succeeded"));
				args.il.Goto(endLabel);

				args.il.MarkLabel(BaseSucceededLabel);

				//  Clear IsBaseClass flag
				args.il.PushArg(args.dataArg);
				args.il.PushBool(false);
				args.il.SetField(fi);
			}


			//  Create calls to deserialize each member
			foreach (PropertySerializationInfo propInfo in properties)
			{
				//  If we hit a higher version property then put in
				//  an "if" statement to exit if the serialized version
				//  is lower than the next batch of properties
				if (propInfo.Version > version)
				{
					//  push versionParam
					//  push propInfo.Version
					//  branch_if_less_than endLabel    ; versionParam < propInfo.Version
					args.il.PushArg(args.versionArg);
					args.il.PushInt(propInfo.Version);
					args.il.GotoIfLess(endLabel);

					version = propInfo.Version;
				}

				args.il.BeginScope();

				//  If this property is marked obsolete, do not deserialize
				//  it if the data min version is equal to or greater than
				//  the version it was made obsolete in.
				if (propInfo.ObsoleteVersion > 0)
				{
					const string NoHeaderLabel = "_checkObsoleteNoHeader";
					const string NotObsoleteLabel = "_notObsolete";

					args.il.PushLocal(args.headerVar);
					args.il.GotoIfFalse(NoHeaderLabel);

					args.il.CallMethod(args.headerVar, typeof(TypeSerializationHeader).GetProperty("DataMinVersion").GetGetMethod());
					args.il.PushInt(propInfo.ObsoleteVersion);
					args.il.GotoIfGreaterOrEqual(SkipPropertyLabel);
					args.il.Goto(NotObsoleteLabel);

					args.il.MarkLabel(NoHeaderLabel);

					args.il.PushArg(args.versionArg);
					args.il.PushInt(propInfo.ObsoleteVersion);
					args.il.GotoIfGreaterOrEqual(SkipPropertyLabel);

					args.il.MarkLabel(NotObsoleteLabel);
				}

				propInfo.GenerateReadIL(args);
				args.il.MarkLabel(SkipPropertyLabel);
				args.il.EndScope();

			}

			//  :endFunction
			args.il.MarkLabel(endLabel);
			args.il.Return();
			return (AutoDeserializeMethod)args.il.Compile(typeof(AutoDeserializeMethod));
		}

		void CreateCompareMethod(List<PropertySerializationInfo> properties)
		{
			const string endFalseLabel = "compareFailedLabel";
			DynamicMethodHelper il = new DynamicMethodHelper(
													  "Compare_" + type.Name,
													  typeof(bool),
													  new Type[] { typeof(object), typeof(object) },
													  this.type
													  );

			//  Create calls to compare each member
			foreach (PropertySerializationInfo propInfo in properties)
			{
				il.BeginScope();
				propInfo.GenerateCompareIL(il, 0, 1);
				il.EndScope();

				il.GotoIfFalse(endFalseLabel);
			}

			//  Return true if all properties passed (didn't goto endFalseLabel)
			il.PushInt(Convert.ToInt32(true));
			il.Return();

			//  Return false if any properties failed (did goto endFalseLabel)
			il.MarkLabel(endFalseLabel);
			il.PushInt(Convert.ToInt32(false));
			il.Return();

			this.compareMethod = (CompareMethod)il.Compile(typeof(CompareMethod));
		}

		void SerializeAutoSerializable(object instance, TypeSerializationArgs args)
		{
			TypeSerializationArgs callerArgs = args;

			args = args.Clone();

			if (this.IsInline)
			{
				//  Inline objects are serialized with only a 2-byte length prefix
				//  No version info or anything else
				int length = 0;
				long lengthPosition = args.Writer.BaseStream.Position;
				long endPosition = -1;

				args.Writer.Write((byte)length);

				if (instance != null)
				{
					args.Header = new TypeSerializationHeader();
					args.Header.DataVersion = 1;
					args.Header.DataMinVersion = 1;

					this.autoSerializeMethod(instance, args);
					endPosition = args.Writer.BaseStream.Position;

					length = (int)((endPosition - lengthPosition) - SerializerHeaders.InlineHeaderSize);
					if (length > SerializerHeaders.MaxInlineSize)
					{
						throw new NotSupportedException(string.Format(
								  "Inline classes must be {0} bytes or less. Class {1} serialized to {2} bytes.",
								  SerializerHeaders.MaxInlineSize,
								  instance.GetType().FullName,
								  length
								  ));
					}

					args.Writer.BaseStream.Position = lengthPosition;
					args.Writer.Write((byte)length);
					args.Writer.BaseStream.Position = endPosition;
				}
			}
			else if (instance == null)
			{
				args.Writer.Write(SerializerHeaders.NullVersion);
			}
			else
			{
				args.Header = new TypeSerializationHeader();
				args.Header.DataVersion = (byte)this.CurrentVersion;
				args.Header.DataMinVersion = (byte)this.MinVersion;

				//  If min version is less than or equal legacy version then write
				//  out legacy version + 1 as the min version in the data stream.This
				//  is to prevent older class definitions from attempting to
				//  deserialize the new data using IVersionSerializable
				if ((this.LegacyVersion > 0) && (this.LegacyVersion >= this.MinVersion))
				{
					args.Header.DataMinVersion = (byte)(this.LegacyVersion + 1);
				}

				//  If this isn't a base class invocation then make sure
				//  SerializationInfo is initialized for the first time.
				//  If it is a base class then get the base class info
				//  from the parent's SerializationInfo
				if (callerArgs.IsBaseClass == false)
				{
					ISerializationInfo info = instance as ISerializationInfo;

					if (info != null)
					{
						args.SerializationInfo = info.SerializationInfo;
					}
					else
					{
						//  SerializationInfo may not be null in this case
						//  because this may be an object contained in a parent
						//  class. We need to make sure the child object doesn't
						//  use the serialization info from the container
						args.SerializationInfo = null;
					}
				}
				else if (args.SerializationInfo != null)
				{
					args.SerializationInfo = callerArgs.SerializationInfo.BaseClassInfo;
				}

				//  Handled cached serialization info if original data
				//  was newer than the class definition that deserialized it
				if (
					 (args.SerializationInfo != null)
					 && (args.SerializationInfo.Version > this.CurrentVersion)
					 )
				{
					args.Header.DataVersion = (byte)args.SerializationInfo.Version;
					args.Header.DataMinVersion = (byte)args.SerializationInfo.MinVersion;
				}

				//  Write a special version byte to indicate this serialized
				//  data uses a header. Because this version is guaranteed to
				//  be higher than actual object versions, older serialization
				//  code will simply treat this is a higher unhandled version,
				//  which is true.
				args.Writer.Write(SerializerHeaders.AutoSerializable);

				//  Write the header with placeholder data
				args.Header.Write(args.Writer);

				//  Serialize the object
				this.autoSerializeMethod(instance, args);

				//  Update the header with final data. Note that the
				//  TypeSerializationHeader class takes care of the
				//  stream position
				args.Header.Write(args.Writer);

			}
		}

		bool DeserializeAutoSerializable(ref object instance, TypeSerializationArgs args)
		{
			byte dataVersion = 1;
			bool isSerializationInfoSupported = (instance == null) ? this.supportsSerializationInfo : (instance is ISerializationInfo);
			TypeSerializationArgs callerArgs = args;
			byte dataMinVersion = 0;

			//  Create a local copy of args so we don't mess with caller's context
			args = args.Clone();

			if (this.IsInline)
			{
				//  Read length prefix. If length is 0 then object is null
				byte length = args.Reader.ReadByte();
				if (length == 0) dataVersion = SerializerHeaders.NullVersion;
			}
			else
			{
				dataVersion = args.Reader.ReadByte();
			}

			if (dataVersion == SerializerHeaders.NullVersion)
			{
				instance = null;
				return false;
			}

			if (dataVersion == SerializerHeaders.Barf)
			{
				_barfSerializer.Value.InnerDeserializeObject(ref instance, new BarfDeserializationArgs(args.Reader));
				return true;
			}

			if (dataVersion == SerializerHeaders.AutoSerializable)
			{
				args.Header = new TypeSerializationHeader();
				args.Header.Read(args.Reader);
				dataVersion = args.Header.DataVersion;
				dataMinVersion = args.Header.DataMinVersion;
			}

			if (
				//  Version of the data is less than our supported minimum version
				//	OR data is a lower version than the min version that can be deserialized
				//  OR data is a higher version but this type isn't forward compatible
				//  OR data has a min version that is higher than this current type definition
				 (dataVersion < this.MinVersion)
			|| (this.CurrentVersion < dataMinVersion)
			|| (dataVersion < this.MinDeserializeVersion)
				 || ((dataVersion > this.CurrentVersion) && (isSerializationInfoSupported == false))
				 || ((args.Header != null) && (args.Header.DataMinVersion > this.CurrentVersion))
				 )
			{
				throw new UnhandledVersionException(this.CurrentVersion, dataVersion);
			}
			if (instance == null)
			{
				instance = CreateInstance();
			}

			//  If this is the derived class then create a new top-level
			//  serialization info. If this is the base class then set
			//  the base class serialization info
			if (callerArgs.IsBaseClass == false)
			{
				args.SerializationInfo = new SerializationInfo();

				if (isSerializationInfoSupported == true)
				{
					((ISerializationInfo)instance).SerializationInfo = args.SerializationInfo;
				}
			}
			else
			{
				callerArgs.SerializationInfo.BaseClassInfo = new SerializationInfo();
				args.SerializationInfo = callerArgs.SerializationInfo.BaseClassInfo;
			}

			args.SerializationInfo.Version = dataVersion;

			if (args.Header != null)
			{
				args.SerializationInfo.MinVersion = args.Header.DataMinVersion;
			}

			this.autoDeserializeMethod.Value(instance, dataVersion, args);

			if (args.Succeeded == false)
			{
				//this should never be hit, as it is a legacy of the volatile flag
				return false;
			}

			if (isSerializationInfoSupported == true)
			{
				if (dataVersion > this.CurrentVersion)
				{
					int readDataLength = (int)(args.Reader.BaseStream.Position - args.Header.DataPosition);
					int unhandledDataLength = args.Header.DataLength - readDataLength;

					args.SerializationInfo.UnhandledData = args.Reader.ReadBytes(unhandledDataLength);
				}
			}

			return true;
		}

		void SerializeVersionSerializable(object instance, TypeSerializationArgs args)
		{
			if (instance == null)
			{
				args.Writer.Write(SerializerHeaders.NullVersion);
			}
			else
			{
				IVersionSerializable ivs = instance as IVersionSerializable;
				args.Writer.Write((byte)ivs.CurrentVersion);
				ivs.Serialize(args.Writer);
			}
		}

		bool DeserializeVersionSerializable(ref object instance, TypeSerializationArgs args)
		{
			byte version = args.Reader.ReadByte();
			bool success = false;

			if (version != SerializerHeaders.NullVersion)
			{
				if (instance == null)
				{
					instance = CreateInstance();
				}

				IVersionSerializable ivs = instance as IVersionSerializable;
				args.Reader.Response = SerializationResponse.Success;
				try
				{
					ivs.Deserialize(args.Reader, version);
				}
				catch (Exception exc)
				{
					const bool indicateTruncate = true;
					string data = Algorithm.ToByteString(args.Reader.BaseStream, 2 << 11, indicateTruncate);
					throw new SerializationException(string.Format("Failed to deserialized type {0} version {1} data {2}",
						instance.GetType().FullName, version, data), exc);
				}
				success = true;

				//throw exception if necessary
				switch (args.Reader.Response)
				{
					case SerializationResponse.Handled:
						throw new HandledVersionException(ivs.CurrentVersion, version);
					case SerializationResponse.Unhandled:
						throw new UnhandledVersionException(ivs.CurrentVersion, version);
					default:
						break;
				}
			}
			else
			{
				instance = null;
			}

			return success;
		}

		void SerializeCustomSerializable(object instance, TypeSerializationArgs args)
		{
			if (instance == null)
			{
				throw new ArgumentNullException("Cannot serialize null object with ICustomSerializable");
			}
			else
			{
				ICustomSerializable ics = instance as ICustomSerializable;
				ics.Serialize(args.Writer);
			}
		}

		bool DeserializeCustomSerializable(ref object instance, TypeSerializationArgs args)
		{
			if (instance == null)
			{
				instance = CreateInstance();
			}

			ICustomSerializable ics = instance as ICustomSerializable;
			ics.Deserialize(args.Reader);
			return true;
		}

		void SerializeUnknownType(object instance, TypeSerializationArgs args)
		{
			args.Writer.Write(instance);
		}

		bool DeserializeUnknownType(ref object instance, TypeSerializationArgs args)
		{
			instance = args.Reader.Read();
			return true;
		}

		object CreateInstance()
		{
			if (this.type.IsInterface)
			{
				throw new ArgumentNullException(
								"instance",
								string.Format("Instance cannot be null when deserializing type {0}", this.type.Name)
								);
			}
			return Activator.CreateInstance(this.type);
		}

		#endregion

		#region IComparer<PropertySerializationInfo> Members
		/// <summary>
		/// Compares properties based on target class version and order
		/// </summary>
		/// <param name="x">Left operand</param>
		/// <param name="y">Right operand</param>
		/// <returns></returns>
		public int Compare(PropertySerializationInfo x, PropertySerializationInfo y)
		{
			return x.CompareTo(y);
		}
		#endregion // IComparer<PropertySerializationInfo>
	}
}
