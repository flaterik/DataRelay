using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using MySpace.Common.HelperObjects;

namespace MySpace.Common.IO
{
	[Flags]
	internal enum PropertySerializationType
	{
		Value = 0x0000,
		Serializable = 0x0001,
		NullableValue = 0x0002,
		BaseMask = 0x00FF,

		Single = 0x0000,
		Array = 0x0100,
		List = 0x0200,
		Dictionary = 0x0400,
		CollectionMask = Array | List | Dictionary,

		Default = Single | Value
	}

	/// <summary>
	/// Stores serialization information for a property in a specific type
	/// </summary>
	[DebuggerDisplay("{Name} Version={attribute.Version}")]
	internal class PropertySerializationInfo
	{
		#region Constants
		internal enum TypeMethodType
		{
			Serialize,
			Deserialize,
			Compare
		}
		#endregion

		#region Members

		private readonly SerializablePropertyAttribute attribute;
		private readonly MemberInfo property;
		private PropertySerializationType serializationType = PropertySerializationType.Default;
		private readonly Type classType;
		private Type collectionInterfaceType;
		private Type dictionaryInterfaceType;
		private Type elementType;
		private Type dictionaryKeyType;
		private Type dictionaryValueType;
		private MethodInfo collectionAddMethod;
		private MethodInfo collectionCountMethod;
		private static readonly Dictionary<TypeMethodType, MethodInfo> genericMethods = new Dictionary<TypeMethodType, MethodInfo>();
		private static readonly Dictionary<TypeMethodType, Dictionary<Type, MethodInfo>> typeMethods = new Dictionary<TypeMethodType, Dictionary<Type, MethodInfo>>();
		private static readonly MsReaderWriterLock typeMethodsLock = new MsReaderWriterLock(System.Threading.LockRecursionPolicy.NoRecursion);
		#endregion

		#region Types
		public class GenerateArgs
		{
			public DynamicMethodHelper il;
			public string streamVar;
			public string nameTableVar;
			public int instanceArg;
			public int versionArg;
			public int dataArg;
			public string headerVar;
		}
		#endregion

		#region Construction

		/// <summary>
		/// Static constructor to initialize internal static collections
		/// </summary>
		static PropertySerializationInfo()
		{
			GetGenericMethods();

			typeMethods.Add(TypeMethodType.Compare, new Dictionary<Type, MethodInfo>());
			typeMethods.Add(TypeMethodType.Serialize, new Dictionary<Type, MethodInfo>());
			typeMethods.Add(TypeMethodType.Deserialize, new Dictionary<Type, MethodInfo>());
		}

		/// <summary>
		/// Creates cache information for a property of a serializable class
		/// </summary>
		/// <param name="attrib">Serialization attribute applied to the property</param>
		/// <param name="prop">Property metadata</param>
		public PropertySerializationInfo(
								  SerializablePropertyAttribute attrib,
								  MemberInfo prop
								  )
		{
			this.attribute = attrib;
			this.property = prop;

			DeterminePropertyType(false);
		}

		/// <summary>
		/// Constructs cache information for the indexer property of a serializable class
		/// </summary>
		/// <param name="attrib"></param>
		/// <param name="type"></param>
		public PropertySerializationInfo(
								  SerializablePropertyAttribute attrib,
								  Type type
								  )
		{
			Type icoll, idict;
			MethodInfo add, count;

			//  Verify that the type implements collection semantics
			if (FindCollectionInterface(type, out icoll, out idict, out add, out count) == false)
			{
				throw new ArgumentException(string.Format(
								"The type {0} must support ICollection<> to serialize the indexer", type.FullName
								));
			}

			this.attribute = attrib;
			this.classType = type;
			DeterminePropertyType(true);
		}

		void DeterminePropertyType(bool indexer)
		{
			Type t = this.Type;
			bool serializable = Serializer.IsSerializable(t);
			this.elementType = t;

			if (
				 (indexer == false)
				 && (
					  t.IsValueType
					  || (t == typeof(string))
					  || serializable
					  )
				 )
			{
				//  Not a collection or array
			}
			else if (t.IsArray)
			{
				this.elementType = t.GetElementType();
				this.serializationType |= PropertySerializationType.Array;
			}
			else // check for ICollection<> or IDictionary<> or Nullable<>
			{
				if (FindCollectionInterface(
						  t,
						  out this.collectionInterfaceType,
						  out this.dictionaryInterfaceType,
						  out this.collectionAddMethod,
						  out this.collectionCountMethod
						  ))
				{
					Debug.Assert(this.collectionInterfaceType != null);

					Type itf = this.dictionaryInterfaceType ?? this.collectionInterfaceType;
					Type[] genArgs = itf.GetGenericArguments();

					if (this.dictionaryInterfaceType != null)
					{
						this.serializationType = PropertySerializationType.Dictionary;
						this.elementType = typeof(KeyValuePair<,>).MakeGenericType(genArgs);
						this.dictionaryKeyType = genArgs[0];
						this.dictionaryValueType = genArgs[1];
					}
					else
					{
						this.serializationType = PropertySerializationType.List;
						this.elementType = genArgs[0];
					}
				}
				else  // if (not a collection)
				{
					//  Check for Nullable<>
					for (; t != null; t = t.BaseType)
					{
						if (t.IsGenericType)
						{
							Type genType = t.GetGenericTypeDefinition();

							if (genType == typeof(Nullable<>))
							{
								this.serializationType = PropertySerializationType.NullableValue;
								break;
							}
						}
					}

					if ((t == null) && (IsDynamic == false))
					{
						if (this.attribute != null
							&& String.IsNullOrEmpty(this.attribute.ReadMethod) == false
							&& String.IsNullOrEmpty(this.attribute.WriteMethod) == false)
						{
							//it's ok, custom serialization
						}
						else
						{
							throw new InvalidOperationException(string.Format("The type {0} cannot be serialized", this.Type.Name));
						}
					}
				}
			}

			if (
				 IsDynamic && !(
					  ((this.elementType == null) || (this.elementType.IsValueType == false))
					  && ((this.dictionaryKeyType == null) || (this.dictionaryKeyType.IsValueType == false))
					  && ((this.dictionaryValueType == null) || (this.dictionaryValueType.IsValueType == false))
					  )
				 )
			{
				throw new InvalidOperationException(string.Format(
								"The dynamic property {0} does not specify any non-value types to serialize",
								this.Name
								));
			}
		}

		/// <summary>
		/// Determines if the type supports ICollection<> or IDictionary<>
		/// </summary>
		/// <param name="sourceType">Type to check</param>
		/// <returns>The found interface type</returns>
		private static bool FindCollectionInterface(
						Type sourceType,
						out Type icollection,
						out Type idictionary,
						out MethodInfo addMethod,
						out MethodInfo countMethod
						)
		{
			icollection = null;
			idictionary = null;
			addMethod = null;
			countMethod = null;

			foreach (Type itf in sourceType.GetInterfaces())
			{
				if (itf.IsGenericType)
				{
					Type genericType = itf.GetGenericTypeDefinition();

					if (genericType == typeof(IDictionary<,>))
					{
						if (idictionary != null)
						{
							throw new NotSupportedException("Cannot serialize ambiguous collection type (Multiple IDictionary<> interfaces found)");
						}

						idictionary = itf;
						addMethod = itf.GetMethod("Add");
					}
					else if (genericType == typeof(ICollection<>))
					{
						if (icollection != null)
						{
							throw new NotSupportedException("Cannot serialize ambiguous collection type (Multiple ICollection<> interfaces found)");
						}
						icollection = itf;

						if (idictionary == null) addMethod = itf.GetMethod("Add");
						countMethod = itf.GetMethod("get_Count");
					}
				}
			}

			return icollection != null;
		}

		#endregion

		#region Properties
		/// <summary>
		/// Version of the class in which this property was added
		/// </summary>
		public int Version
		{
			get { return this.attribute.Version; }
		}

		/// <summary>
		/// Name of the property
		/// </summary>
		public string Name
		{
			get
			{
				if (this.classType != null)
				{
					return "*Root";
				}
				else
				{
					return this.property.Name;
				}
			}
		}

		public int ObsoleteVersion
		{
			get { return this.attribute.ObsoleteVersion; }
		}

		/// <summary>
		/// Type of the property
		/// </summary>
		public Type Type
		{
			get
			{
				if (this.classType != null)
				{
					return this.classType;
				}
				else if (this.property is PropertyInfo)
				{
					return (this.property as PropertyInfo).PropertyType;
				}
				else
				{
					return (this.property as FieldInfo).FieldType;
				}
			}
		}

		Type OwnerType
		{
			get
			{
				if (this.classType != null)
				{
					return this.classType;
				}
				else
				{
					return this.property.DeclaringType;
				}
			}
		}

		public PropertySerializationType SerializationType
		{
			get { return this.serializationType; }
		}

		public bool IsArray
		{
			get { return (this.serializationType & PropertySerializationType.Array) != 0; }
		}

		public bool IsCollection
		{
			get { return (this.serializationType & PropertySerializationType.CollectionMask) != 0; }
		}

		public bool IsList
		{
			get { return (this.serializationType & PropertySerializationType.List) != 0; }
		}

		public bool IsDictionary
		{
			get { return (this.serializationType & PropertySerializationType.Dictionary) != 0; }
		}

		public bool IsSerializable
		{
			get { return (this.serializationType & PropertySerializationType.Serializable) != 0; }
		}

		public bool IsNullable
		{
			get { return (this.serializationType & PropertySerializationType.NullableValue) != 0; }
		}

		public bool IsDynamic
		{
			get { return this.attribute.Dynamic; }
		}

		#endregion

		#region Methods
		/// <summary>
		/// Generates IL for serializing the property
		/// </summary>
		/// <param name="args">
		///	<para>The arguments used to generate the il.</para>
		/// </param>
		public void GenerateWriteIL(GenerateArgs args)
		{
			#region Names
			const string loopConditionLabel = "loopStart";
			const string loopEndLabel = "loopEnd";
			const string propVar = "propValue";
			const string elemVar = "elemValue";
			const string instanceVar = "instance";
			const string collectionNotNullLabel = "collectionNotNullLabel";
			const string countVar = "collectionCount";
			const string enumVar = "enumerator";
			const string dictEnumVar = "dictEnumerator";
			const string dictKeyVar = "dictKey";
			const string dictValueVar = "dictValue";
			const string enumerableVar = "enumerable";
			#endregion

			DynamicMethodHelper il = args.il;

			if (this.attribute.WriteMethod != null)
			{
				//  Call custom handler
				MethodInfo method = this.OwnerType.GetMethod(
													 this.attribute.WriteMethod,
													 BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy,
													 null,
													 new [] { typeof(IPrimitiveWriter) },
													 null
													 );

				if (method == null)
				{
					throw new ApplicationException(string.Format(
									"Failed to locate method {0}.{1} to write property {2}",
									this.OwnerType.Name,
									this.attribute.ReadMethod,
									this.Name
									));
				}

				il.PushArg(args.instanceArg);
				il.PushLocal(args.streamVar);
				il.CallMethod(method);
				return;
			}

			il.DeclareLocal(instanceVar, this.OwnerType);
			il.DeclareLocal(propVar, this.Type);

			il.PushArg(args.instanceArg);
			il.PopLocal(instanceVar);

			//  Get property value and store in local
			if (this.classType != null)
			{
				//  For indexer load the instance itself
				il.PushLocal(instanceVar);
			}
			else if (this.property is PropertyInfo)
			{
				il.PushLocal(instanceVar);
				il.CallMethod(((PropertyInfo)this.property).GetGetMethod(true));
			}
			else // FieldInfo
			{
				//  push instance
				//  load_field
				il.GetField(instanceVar, (FieldInfo)this.property);
			}

			//  store propValue
			il.PopLocal(propVar);

			//  If this is a collection and null then store -1 length
			//  If this is a non-null collection then emit loop code            
			if (IsCollection)
			{
				il.DeclareLocal(countVar, typeof(int));
				il.DeclareLocal(enumVar, typeof(IEnumerator));
				il.DeclareLocal(enumerableVar, typeof(IEnumerable));
				il.DeclareLocal(elemVar, this.elementType);

				//  push propValue
				//  push null
				//  branch_if_equal collectionNullLabel
				//  branch collectionNotNullLabel
				il.PushLocal(propVar);
				il.GotoIfTrue(collectionNotNullLabel);

				//  ; collection is null so write -1
				//  push writer
				//  push -1
				//  call IPrimitiveWriter.Write(int)
				//  goto loopEnd
				il.PushLocal(args.streamVar);
				il.PushInt(-1);
				il.CallMethod(GetTypeMethod(typeof(int), TypeMethodType.Serialize));
				il.Goto(loopEndLabel);

				//  :collectionNotNullLabel
				il.MarkLabel(collectionNotNullLabel);

				//  countVar = collection.Length/Count
				if (IsArray)
				{
					il.CallMethod(propVar, "get_Length");
				}
				else
				{
					il.PushLocal(propVar);
					il.Cast(this.collectionInterfaceType);
					il.CallMethod(this.collectionCountMethod);
				}
				il.PopLocal(countVar);

				//  writer.Write(countVar)
				il.PushLocal(args.streamVar);
				il.PushLocal(countVar);
				il.CallMethod(GetTypeMethod(typeof(int), TypeMethodType.Serialize));

				//  enumerable = propVar
				//  enumVar = enumerable.GetEnumerator()
				il.CopyLocal(propVar, enumerableVar);
				il.CallMethod(enumerableVar, "GetEnumerator");
				il.PopLocal(enumVar);

				if (IsDictionary)
				{
					il.DeclareLocal(dictEnumVar, typeof(IDictionaryEnumerator));
					il.DeclareLocal(dictKeyVar, this.dictionaryKeyType);
					il.DeclareLocal(dictValueVar, this.dictionaryValueType);
					il.CopyLocal(enumVar, dictEnumVar);
				}

				//  :loopConditionLable
				//  if (enumVar.MoveNext == false) goto loopEndLabel
				il.MarkLabel(loopConditionLabel);
				il.CallMethod(enumVar, "MoveNext");
				il.GotoIfFalse(loopEndLabel);

				//  if (!dictionary) elemVar = enumVar.Current
				if (IsDictionary == false)
				{
					il.CallMethod(enumVar, "get_Current");
					il.PopLocalFromObject(elemVar);
				}
			}

			//  For dictionary properties serialize the key and value
			//  For everything else serialize the element as-is
			if (IsDictionary)
			{
				//  push elemValue
				//  call get_Key
				//  serialize key
				il.CallMethod(dictEnumVar, "get_Key");
				this.GenerateWriteTypeIL(this.dictionaryKeyType, args);

				//  push elemValue
				//  call get_Value
				//  serialize value
				il.CallMethod(dictEnumVar, "get_Value");
				GenerateWriteTypeIL(this.dictionaryValueType, args);
			}
			else if (IsCollection)
			{
				//  push elemValue
				//  serialize elemValue
				il.PushLocalAsObject(elemVar);
				GenerateWriteTypeIL(this.elementType, args);
			}
			else
			{
				il.PushLocalAsObject(propVar);
				GenerateWriteTypeIL(this.elementType, args);
			}

			//  Complete loop instructions for collection
			if (IsCollection)
			{
				//  branch loopConditionLabel
				//  :loopEnd
				il.Goto(loopConditionLabel);
				il.MarkLabel(loopEndLabel);
			}
		}

		/// <summary>
		/// Generates IL to write a single value from the stack to the stream
		/// </summary>
		/// <param name="type">Type of the object on the stack</param>
		/// <param name="args">The generate args.</param>
		void GenerateWriteTypeIL(Type type, GenerateArgs args)
		{
			MethodInfo method = GetTypeMethod(type, TypeMethodType.Serialize);
			const string valueVar = "writeValue";
			const string valueDoneLabel = "_writeValueDone";
			DynamicMethodHelper il = args.il;
			bool dynamic = IsDynamic && (type.IsValueType == false);

			il.BeginScope();
			{
				il.DeclareLocal(valueVar, type);
				il.PopLocalFromObject(valueVar);

				if (dynamic)
				{
					const string notNullLabel = "_dynamicNotNull";

					//  If value is a reference type then serialize
					//  NullVersion (-1) and do nothing else. No need to
					//  save a type name or anything. If not null then
					//  add type to name table and write the type byte
					//  before calling serializer to write the instance
					il.PushLocal(valueVar);
					il.GotoIfTrue(notNullLabel);

					il.PushLocal(args.streamVar);
					il.PushInt(-1);
					il.CallMethod(GetTypeMethod(typeof(byte), TypeMethodType.Serialize));
					il.Goto(valueDoneLabel);

					il.MarkLabel(notNullLabel);

					//  push writer for Write(byte) call
					il.PushLocal(args.streamVar);

					//  push nameTable for Add(Type) call
					il.DebugWriteNamedLocal(args.nameTableVar);
					il.PushLocal(args.nameTableVar);

					//  push (object)value
					//  call object.GetType()
					il.PushLocal(valueVar);
					il.Cast(typeof(object));
					il.CallMethod(typeof(object).GetMethod("GetType"));

					//  push this.Version
					il.PushInt(this.Version);

					//  call TypeNameTable.Add(Type, version)
					il.CallMethod(typeof(TypeNameTable).GetMethod("Add", new [] { typeof(Type), typeof(int) }));          //  Return type byte

					//  call IPrimitiveWriter.Write(byte)
					il.CallMethod(GetTypeMethod(typeof(byte), TypeMethodType.Serialize));

				}

				if (method.DeclaringType == typeof(IPrimitiveWriter))
				{
					//  push IPrimitiveWriter       ; arg 1 for method call
					//  push elemValue              ; arg 2 for method call
					il.PushLocal(args.streamVar);
					il.PushLocal(valueVar);
				}
				else
				{
					//  push value
					//  push TypeSerializationArgs
					il.PushLocal(valueVar);
					il.PushArg(args.dataArg);
				}

				il.CallMethod(method);

				if (dynamic)
				{
					il.MarkLabel(valueDoneLabel);
				}
			}
			il.EndScope();

		}

		private static SerializableStructAttribute GetStructSerializationInfo(Type structType)
		{
			object[] attributes = structType.GetCustomAttributes(typeof(SerializableStructAttribute), true);

			if (attributes.Length == 0)
			{
				return null;
			}

			SerializableStructAttribute serializationAttribute = (SerializableStructAttribute)attributes[0];
			if (serializationAttribute.TargetType.IsPrimitive == false)
			{
				throw new NotSupportedException(string.Format(
								"The struct type {0} specifies an invalid target type in the SerializableStruct attribute", structType.FullName
								));
			}

			return serializationAttribute;
		}

		static MethodInfo GenerateWriteStructMethod(Type structType)
		{
			SerializableStructAttribute serializationAttribute = GetStructSerializationInfo(structType);
			MethodInfo method;

			if (serializationAttribute != null)
			{
				method = GetTypeMethod(serializationAttribute.TargetType, TypeMethodType.Serialize);
			}
			else
			{
				MethodInfo genericMethod = genericMethods[TypeMethodType.Serialize];
				method = genericMethod.MakeGenericMethod(new [] { structType });
			}

			if (method == null)
			{
				throw new NotSupportedException(string.Format(
									 "The struct type {0} cannot be serialized", structType.FullName
									 ));
			}

			return method;
		}

		/// <summary>
		/// Generates IL to deserialize the property from a stream
		/// </summary>
		/// <param name="args">
		///	<para>The arguments used to generate the read il.</para>
		/// </param>
		public void GenerateReadIL(GenerateArgs args)
		{
			#region Names
			const string loopStart = "loopStart";
			const string loopEnd = "loopEnd";
			const string propVar = "propValue";
			const string elemVar = "elemVar";
			const string indexVar = "loopIndex";
			const string instanceVar = "instance";
			const string countVar = "count";
			const string collectionNotNull = "collectionNotNull";
			const string skipObsoleteLabel = "skipObsolete";
			#endregion

			DynamicMethodHelper il = args.il;

			if (this.attribute.ReadMethod != null)
			{
				//  Call custom handler
				MethodInfo method = this.OwnerType.GetMethod(
													 this.attribute.ReadMethod,
													 BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy,
													 null,
													 new [] { typeof(IPrimitiveReader) },
													 null
													 );

				if (method == null)
				{
					throw new ApplicationException(string.Format(
									"Failed to locate method {0}.{1} to read property {2}",
									this.OwnerType.Name,
									this.attribute.ReadMethod,
									this.Name
									));
				}

				il.PushArg(args.instanceArg);
				il.PushLocal(args.streamVar);
				il.CallMethod(method);
				return;
			}

			il.DeclareLocal(instanceVar, this.OwnerType);
			il.DeclareLocal(propVar, this.Type);

			il.PushArg(args.instanceArg);
			il.PopLocal(instanceVar);

			//  Create loop for collection types
			if (IsCollection)
			{
				il.DeclareLocal(indexVar, typeof(int));
				il.DeclareLocal(countVar, typeof(int));
				il.DeclareLocal(elemVar, this.elementType);

				//  countVar = reader.ReadInt32()
				il.PushLocal(args.streamVar);
				il.CallMethod(GetTypeMethod(typeof(int), TypeMethodType.Deserialize));
				il.PopLocal(countVar);

				//  ; Set prop to null if length < 0
				//  if (countVar >= 0) goto collectionNotNull
				il.PushLocal(countVar);
				il.PushInt(0);
				il.GotoIfGreaterOrEqual(collectionNotNull);

				//  propVar = null
				//  goto loopEnd
				il.PushNull();
				il.PopLocal(propVar);
				il.Goto(loopEnd);

				//  :collectionNotNull
				//  if (indexer)
				//      propVar = instanceVar
				//  else if (array)
				//      propVar = new(countVar)
				//  else
				//      propVar = new()
				//  end if
				il.MarkLabel(collectionNotNull);
				if (this.classType != null)
				{
					il.CopyLocal(instanceVar, propVar);
				}
				else if (IsArray)
				{
					il.PushLocal(countVar);
					il.NewObject(propVar, new [] { typeof(int) });
				}
				else
				{
					il.NewObject(propVar);
				}

				//  indexVar = 0
				//  :loopStart
				//  if (indexVar == countVar) goto loopEnd
				il.PushInt(0);
				il.PopLocal(indexVar);
				il.MarkLabel(loopStart);

				il.PushLocal(indexVar);
				il.PushLocal(countVar);
				il.GotoIfEqual(loopEnd);
			}

			//  If this is a dictionary then do special handling to add the element
			//  If not then just read the value and use generic code to store it
			if (IsDictionary)
			{
				//  propVar.Add(ReadKey(), ReadValue())
				il.PushLocal(propVar);
				il.Cast(this.dictionaryInterfaceType);
				this.GenerateReadTypeIL(this.dictionaryKeyType, args);
				this.GenerateReadTypeIL(this.dictionaryValueType, args);
				il.CallMethod(this.collectionAddMethod);
			}
			else
			{
				GenerateReadTypeIL(this.elementType, args);
				il.PopLocal(IsCollection ? elemVar : propVar);
			}

			//  If this is a collection then add the element and loop
			if (IsCollection)
			{
				switch (this.serializationType & PropertySerializationType.CollectionMask)
				{
					case PropertySerializationType.Dictionary:
						//  Already handled
						break;

					case PropertySerializationType.Array:
						//  push propValue      ; arg 1 (this)
						//  push loopIndex      ; arg 2
						//  push elemValue      ; arg 3
						//  call SetValue
						il.BeginCallMethod(propVar, "SetValue", new [] { typeof(object), typeof(int) });
						il.PushLocalAsObject(elemVar);
						il.PushLocal(indexVar);
						il.CallMethod();
						break;

					case PropertySerializationType.List:
						//  push propValue      ; arg 1 (this)
						//  push elemValue      ; arg 2
						//  call Add
						il.PushLocal(propVar);
						il.Cast(this.collectionInterfaceType);
						il.PushLocal(elemVar);
						il.CallMethod(this.collectionAddMethod);
						break;
				}

				//  indexVar++
				//  goto loopStart
				//  :loopEnd
				il.IncrementLocal(indexVar);
				il.Goto(loopStart);
				il.MarkLabel(loopEnd);
			}

			//  Set property/field value
			//  This isn't required for indexers.
			if (this.property != null)
			{
				if (this.property is PropertyInfo)
				{
					il.PushLocal(instanceVar);
					il.PushLocal(propVar);
					il.CallMethod(((PropertyInfo)this.property).GetSetMethod(true));
				}
				else // FieldInfo
				{
					il.SetField(instanceVar, propVar, (FieldInfo)this.property);
				}
			}

			if (this.attribute.ObsoleteVersion > 0)
			{
				il.MarkLabel(skipObsoleteLabel);
			}
		}

		/// <summary>
		/// Generates IL to read a single item from the stream and leaves the result on the stack.
		/// </summary>
		/// <param name="type">The <see cref="Type"/> of item to read.</param>
		/// <param name="args">The arguments for generating the il.</param>
		void GenerateReadTypeIL(Type type, GenerateArgs args)
		{
			MethodInfo method = GetTypeMethod(type, TypeMethodType.Deserialize);
			DynamicMethodHelper il = args.il;
			bool dynamic = IsDynamic && (type.IsValueType == false);

			//  Variable names
			const string valueVar = "_readValue";
			const string dynamicTypeIndexVar = "_typeIndex";
			const string dynamicTypeVar = "_dynamicType";
			const string dynamicTypeNameVar = "_dynamicTypeName";
			const string dynamicTypeResolvedLabel = "_dynamicTypeResolved";
			const string dynamicTypeNotNullLabel = "_dynamicTypeNotNull";
			const string dynamicTypeDoneLabel = "_dynamicTypeDone";

			il.BeginScope();
			{
				if (dynamic)
				{
					il.DeclareLocal(dynamicTypeIndexVar, typeof(byte));
					il.DeclareLocal(valueVar, typeof(object));
					il.DeclareLocal(dynamicTypeNameVar, typeof(string));
					il.DeclareLocal(dynamicTypeVar, typeof(Type));

					//  Read type index
					il.PushLocal(args.streamVar);
					il.CallMethod(GetTypeMethod(typeof(byte), TypeMethodType.Deserialize));
					il.PopLocal(dynamicTypeIndexVar);

					//  if (typeIndex == -1) goto :dynamicInstanceNull
					il.PushLocal(dynamicTypeIndexVar);
					il.PushInt(SerializerHeaders.NullVersion);
					il.GotoIfNotEqual(dynamicTypeNotNullLabel);

					//  return null
					//  goto :dynamicTypeDone
					il.PushNull();
					il.Goto(dynamicTypeDoneLabel);

					//  :dynamicTypeNotNull
					il.MarkLabel(dynamicTypeNotNullLabel);

					//  Get type info for typeIndex
					il.PushLocal(args.nameTableVar);
					il.PushLocal(dynamicTypeIndexVar);
					il.PushLocalAsRef(dynamicTypeVar);
					il.PushLocalAsRef(dynamicTypeNameVar);
					il.CallMethod(typeof(TypeNameTable).GetMethod("GetTypeInfo"));

					//  If (type != null) goto :typeResolved
					il.PushLocal(dynamicTypeVar);
					il.GotoIfTrue(dynamicTypeResolvedLabel);

					//  Call Type.GetType to resolve type. We must do this
					//  in the context of the type being deserialized to
					//  get the appropriate visibility permissions
					il.PushLocal(dynamicTypeNameVar);
					il.PushBool(true);
					il.CallMethod(typeof(Type).GetMethod(
										 "GetType",
										 BindingFlags.Static | BindingFlags.Public,
										 null,
										 new [] { typeof(string), typeof(bool) },
										 null
										 ));
					il.PopLocal(dynamicTypeVar);

					//  Save the resolved type back to the type table so
					//  subsequent instances of the same type don't have to
					//  do it again
					il.PushLocal(args.nameTableVar);
					il.PushLocal(dynamicTypeIndexVar);
					il.PushLocal(dynamicTypeVar);
					il.CallMethod(typeof(TypeNameTable).GetMethod("SetResolvedType"));

					//  :typeResolved
					il.MarkLabel(dynamicTypeResolvedLabel);

					//  Create an empty instance of the resolved type
					il.PushLocal(dynamicTypeVar);
					il.PushBool(true);
					il.CallMethod(typeof(Activator).GetMethod(
										 "CreateInstance",
										 new [] { typeof(Type), typeof(bool) }
										 ));

					il.PopLocal(valueVar);

					//  Call the serializer to read it
					il.PushLocalAsRef(valueVar);
					il.PushArg(args.dataArg);
					il.CallMethod(method);
					il.Pop();

					//  return (baseType)dynamicInstance
					il.PushLocal(valueVar);
					il.Cast(type);

					//  :dynamicTypeDone
					il.MarkLabel(dynamicTypeDoneLabel);

				}
				else if (method.DeclaringType == typeof(IPrimitiveReader))
				{
					//  return reader.ReadXXXX()
					il.PushLocal(args.streamVar);
					il.CallMethod(method);
				}
				else //if (type.IsClass)
				{
					//  Create empty instance of type
					il.DeclareLocal(valueVar, typeof(object));
					if (type.IsClass)
					{
						il.NewObject(valueVar, type, Type.EmptyTypes);
					}

					//  Call the serializer to read it
					il.PushLocalAsRef(valueVar);
					il.PushArg(args.dataArg);
					il.CallMethod(method);
					il.Pop();                       // Ignore return value

					//  return (type)instance
					il.PushLocal(valueVar);
					if (type.IsClass)
					{
						il.Cast(type);
					}
					else
					{
						il.UnboxValueType(type);
					}
				}
			}
			il.EndScope();
		}

		static MethodInfo GenerateReadStructMethod(Type structType)
		{
			SerializableStructAttribute serializationAttribute = GetStructSerializationInfo(structType);
			MethodInfo method;

			if (serializationAttribute != null)
			{
				method = GetTypeMethod(serializationAttribute.TargetType, TypeMethodType.Deserialize);
			}
			else
			{
				MethodInfo genericMethod = genericMethods[TypeMethodType.Deserialize];
				method = genericMethod.MakeGenericMethod(new [] { structType });
			}

			if (method == null)
			{
				throw new NotSupportedException(string.Format(
									 "The struct type {0} cannot be deserialized", structType.FullName
									 ));
			}

			return method;
		}

		public void GenerateCompareIL(DynamicMethodHelper il, int xIndex, int yIndex)
		{
			#region Names
			const string instanceX = "instanceX";
			const string propVarX = "propValueX";
			const string enumerableX = "enumerableX";
			const string enumVarX = "enumX";
			const string dictEnumVarX = "dictEnumX";

			const string instanceY = "instanceY";
			const string propVarY = "propValueY";
			const string enumerableY = "enumerableY";
			const string enumVarY = "enumY";
			const string dictEnumVarY = "dictEnumY";

			const string endFalseLabel = "logBadPropertyLabel";
			const string endTrueLabel = "endTrue";
			const string endLabel = "endLabel";
			const string loopStart = "loopStart";
			const string loopEnd = "loopEnd";
			#endregion

			il.DebugWriteLine("Comparing property " + this.Name);

			il.DeclareLocal(instanceX, this.OwnerType);
			il.PushArg(xIndex);
			il.PopLocal(instanceX);

			il.DeclareLocal(instanceY, this.OwnerType);
			il.PushArg(yIndex);
			il.PopLocal(instanceY);

			il.DeclareLocal(propVarX, this.Type);
			il.DeclareLocal(propVarY, this.Type);

			if (this.classType != null)
			{
				il.CopyLocal(instanceX, propVarX);
				il.CopyLocal(instanceY, propVarY);
			}
			else if (this.property is PropertyInfo)
			{
				PropertyInfo pi = (PropertyInfo)this.property;
				MethodInfo getMethod = pi.GetGetMethod(true);

				il.CallMethod(instanceX, getMethod);
				il.PopLocal(propVarX);
				il.CallMethod(instanceY, getMethod);
				il.PopLocal(propVarY);
			}
			else
			{
				il.GetField(instanceX, this.property as FieldInfo);
				il.PopLocal(propVarX);
				il.GetField(instanceY, this.property as FieldInfo);
				il.PopLocal(propVarY);
			}

			//  For ref types, if both null or the same object then return true
			//  For value types, this is a value comparison
			Type t = IsCollection ? (IsDictionary ? null : this.elementType) : this.Type;
			bool doDefault = true;
			if (t != null && t.IsValueType)
			{
				MethodInfo mi = t.GetMethod("op_Equality", new [] { t, t });
				if (mi != null)
				{
					il.PushLocal(propVarX);
					il.PushLocal(propVarY);
					il.CallMethod(mi);
					il.GotoIfFalse(endTrueLabel);
					doDefault = false;
				}
				else if (t.IsGenericType && t.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
				{
					Type it = t.GetGenericArguments()[0];
					mi = typeof(Nullable).GetMethod("Equals", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
					mi = mi.MakeGenericMethod(it);

					il.PushLocal(propVarX);
					il.PushLocal(propVarY);
					il.CallMethod(mi);
					il.GotoIfFalse(endTrueLabel);
					doDefault = false;
				}
			}

			if (doDefault)
			{
				il.PushLocal(propVarX);
				il.PushLocal(propVarY);
				il.GotoIfEqual(endTrueLabel);
			}

			if (IsCollection)
			{
				il.DeclareLocal(enumerableX, typeof(IEnumerable));
				il.DeclareLocal(enumVarX, typeof(IEnumerator));
				il.DeclareLocal(enumerableY, typeof(IEnumerable));
				il.DeclareLocal(enumVarY, typeof(IEnumerator));

				il.CopyLocal(propVarX, enumerableX);
				il.CallMethod(enumerableX, "GetEnumerator");
				il.PopLocal(enumVarX);

				il.CopyLocal(propVarY, enumerableX);
				il.CallMethod(enumerableX, "GetEnumerator");
				il.PopLocal(enumVarY);

				if (IsDictionary)
				{
					il.DeclareLocal(dictEnumVarX, typeof(IDictionaryEnumerator));
					il.CopyLocal(enumVarX, dictEnumVarX);
					il.DeclareLocal(dictEnumVarY, typeof(IDictionaryEnumerator));
					il.CopyLocal(enumVarY, dictEnumVarY);
				}

				il.MarkLabel(loopStart);
				il.CallMethod(enumVarX, "MoveNext");
				il.GotoIfFalse(loopEnd);

				il.CallMethod(enumVarY, "MoveNext");
				il.GotoIfFalse(endFalseLabel); // y has less elements than x

				if (IsDictionary)
				{
					il.CallMethod(dictEnumVarX, "get_Key");
					il.CallMethod(dictEnumVarY, "get_Key");
					GenerateCompareTypeIL(il, this.dictionaryKeyType);
					il.GotoIfFalse(endFalseLabel);

					il.CallMethod(dictEnumVarX, "get_Value");
					il.CallMethod(dictEnumVarY, "get_Value");
					GenerateCompareTypeIL(il, this.dictionaryValueType);
					il.GotoIfFalse(endFalseLabel);
				}
				else // if (not dictionary)
				{
					il.CallMethod(enumVarX, "get_Current");
					il.CallMethod(enumVarY, "get_Current");
					GenerateCompareTypeIL(il, this.elementType);
					il.GotoIfFalse(endFalseLabel);
				}

				il.Goto(loopStart);
				il.MarkLabel(loopEnd);

				//  enumVarX has no more elements so enumVarY shouldn't
				//  have any more either
				il.CallMethod(enumVarY, "MoveNext");
				il.GotoIfTrue(endFalseLabel); // count mismatch
			}
			else // if (not collection)
			{
				il.PushLocalAsObject(propVarX);
				il.PushLocalAsObject(propVarY);
				GenerateCompareTypeIL(il, this.Type);
				il.GotoIfFalse(endFalseLabel);
			}

			il.MarkLabel(endTrueLabel);
			//il.DebugWriteLine(string.Format("Property {0} is equal", this.Name));
			il.PushInt(1);
			il.Goto(endLabel);

			//  Log out name of property if not equal
			il.MarkLabel(endFalseLabel);
			il.DebugWriteLine(string.Format("Property {0} is not equal", this.Name));
			il.PushInt(0);  // set success to false

			il.MarkLabel(endLabel);
		}

		static void GenerateCompareTypeIL(DynamicMethodHelper il, Type t)
		{
			MethodInfo method = GetTypeMethod(t, TypeMethodType.Compare);

			if ((method == null) && (t.IsPrimitive == false))
			{
				il.Pop();
				il.Pop();
				il.PushInt(1);
				return;
			}

			il.BeginScope();
			{
				const string valX = "x";
				const string valY = "y";
				const string valThis = "_thisX";
				const string trueLabel = "endTrue";
				const string falseLabel = "endFalse";
				const string endLabel = "endCheck";

				il.DeclareLocal(valX, typeof(object));
				il.PopLocal(valX);
				il.DeclareLocal(valY, typeof(object));
				il.PopLocal(valY);

				//  If both null or the same object then return true
				il.PushLocal(valX);
				il.PushLocal(valY);
				il.GotoIfEqual(trueLabel);

				//  Exit false if either value is null
				il.PushLocal(valX);
				il.GotoIfFalse(falseLabel);

				il.PushLocal(valY);
				il.GotoIfFalse(falseLabel);

				//  Both operands are non-null so call the type-specific comparison method
				if (t.IsPrimitive)
				{
					il.PushLocal(valX);
					il.UnboxValueType(t);
					il.PushLocal(valX);
					il.UnboxValueType(t);
					il.CompareEqual();
				}
				else
				{
					Type paramType = method.GetParameters()[0].ParameterType;

					il.DebugWriteLine("Calling " + method);
					if (method.IsStatic)
					{
						il.PushLocal(valX);
						if (paramType.IsValueType) il.UnboxValueType(paramType);
					}
					else
					{
						il.DeclareLocal(valThis, t);
						il.PushLocal(valX);
						il.PopLocalFromObject(valThis);
						il.PushThis(valThis, method);
					}
					il.PushLocal(valY);
					if (paramType.IsValueType) il.UnboxValueType(paramType);
					il.CallMethod(method);
				}
				il.GotoIfFalse(falseLabel);

				il.MarkLabel(trueLabel);
				il.PushInt(1);
				il.Goto(endLabel);

				il.MarkLabel(falseLabel);
				il.DebugWriteLine("The following values are not equal:");
				il.DebugWriteLocal(valX);
				il.DebugWriteLocal(valY);
				il.PushInt(0);

				il.MarkLabel(endLabel);
			}
			il.EndScope();
		}

		/// <summary>
		/// Returns a typed version of the Serializer.Serialize method.
		/// </summary>
		/// <param name="t">The <see cref="Type"/> to serialize or deserialize.</param>
		/// <param name="methodType">
		///	<para>The type of method to get; <see cref="TypeMethodType.Serialize"/>,
		///	<see cref="TypeMethodType.Deserialize"/>, or <see cref="TypeMethodType.Compare"/>.</para>
		/// </param>
		/// <returns>
		///	<para>A typed version of the <see cref="Serializer.Serialize{T}(Stream, T)"/> method.</para>
		/// </returns>
		internal static MethodInfo GetTypeMethod(Type t, TypeMethodType methodType)
		{
			MethodInfo method = null;
			Dictionary<Type, MethodInfo> methodTypeMethods = null;

			Debug.WriteLine(string.Format("Getting type method {0} for {1}", methodType.ToString(), t.FullName));

			typeMethodsLock.Read(() =>
				{
					if (typeMethods.TryGetValue(methodType, out methodTypeMethods))
					{
						methodTypeMethods.TryGetValue(t, out method);
					}
				});

			if (method == null)
			{
				Type[] typeArgs = { t };

				//  For simple types, find the corresponding IPrimitiveReader/IPrimitveWriter method
				//  For complex types, get a typed version of the appropriate Serializer method
				//  For enumerations, find the corresponding method for the base type
				if (t.IsEnum)
				{
					method = GetTypeMethod(Enum.GetUnderlyingType(t), methodType);
				}
				else if (t.IsValueType || (t == typeof(string)))
				{
					switch (methodType)
					{
						case TypeMethodType.Serialize:
							method = typeof(IPrimitiveWriter).GetMethod("Write", typeArgs);
							if ((method == null) && (t.IsPrimitive == false))
							{
								method = GenerateWriteStructMethod(t);
							}
							break;

						case TypeMethodType.Deserialize:
							if (t.Name.StartsWith("Nullable"))
							{
								Type[] targs = t.GetGenericArguments();
								method = typeof(IPrimitiveReader).GetMethod(
												string.Format("ReadNullable{0}", targs[0].Name),
												Type.EmptyTypes
												);
							}
							else
							{
								method = typeof(IPrimitiveReader).GetMethod(
												string.Format("Read{0}", t.Name),
												Type.EmptyTypes
												);
							}

							if ((method == null) && (t.IsPrimitive == false))
							{
								method = GenerateReadStructMethod(t);
							}
							break;

						case TypeMethodType.Compare:
							{
								for (
									 Type currentType = t;
									 (currentType != null) && (method == null);
									 currentType = currentType.BaseType
									 )
								{
									method = t.GetMethod(
													"Equals",
													BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
													null,
													new [] { currentType },
													null
													);
								}

								if (method == null)
								{
									Debug.WriteLine(string.Format("WARNING: The type {0} does contain a supported Equals method", t.FullName));
								}
							}
							break;
					}

				}
				else // complex type
				{
					//  Get a generic version of a method that can
					//  perform the requested operation and then make
					//  a type-specific version of it
					method = genericMethods[methodType];
					method = method.MakeGenericMethod(typeArgs);
				}

				if ((method == null) && (methodType != TypeMethodType.Compare))
				{
					string msg = string.Format("Failed to resolve handler method for type {0}", t.FullName);
					Debug.Fail(msg);
					throw new NotImplementedException(msg);
				}

				//  Update the method cache
				typeMethodsLock.Write(() => typeMethods[methodType][t] = method);

			} // if (method not created yet)

			return method;
		}

		/// <summary>
		/// Retrieves the signatures for the generic Serializer
		/// Serialize and Deserialize method. This is a one-time operation
		/// that happens the first time we need a type-specific version of those
		/// methods.
		/// Note that if a race condition happens and two threads get in here at the
		/// the same time then we don't really care.
		/// </summary>
		static void GetGenericMethods()
		{
			ParameterInfo[] parameters = null;
			Type TypeSerializationArgsType = typeof(TypeSerializationArgs);
			Type ObjectType = typeof(object);
			Type ObjectRefType = ObjectType.MakeByRefType();

			//  It's possible that multiple threads may have gotten here.
			//  If so then exit if we weren't first
			if (genericMethods.Count > 0) return;

			//  Enumerate all public static Serializer methods to
			//  find "void Serialize<T>(IPrimitiveWriter, T, SerializeFlags)"
			//  and "T Deserialize<T>(IPrimitiveReader, SerializeFlags)"
			foreach (MethodInfo m in typeof(Serializer).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic))
			{
				//  Check if this method matches Serialize<T>
				if (
					 (genericMethods.ContainsKey(TypeMethodType.Serialize) == false)
					 && (m.Name == "Serialize")
					 && m.IsGenericMethodDefinition
					 )
				{
					parameters = m.GetParameters();
					if (
						 (parameters.Length == 2)
						 && (parameters[1].ParameterType == TypeSerializationArgsType)
						 )
					{
						genericMethods.Add(TypeMethodType.Serialize, m);
					}
				}

				//  Check if this method matches Deserialize<T>
				else if (
					 (genericMethods.ContainsKey(TypeMethodType.Deserialize) == false)
					 && (m.Name == "Deserialize")
					 && m.IsGenericMethodDefinition
					 )
				{
					parameters = m.GetParameters();
					if (
						 (parameters.Length == 2)
						 && (parameters[0].ParameterType == ObjectRefType)
						 && (parameters[1].ParameterType == TypeSerializationArgsType)
						 )
					{
						genericMethods.Add(TypeMethodType.Deserialize, m);
					}
				}
				else if (
						 (genericMethods.ContainsKey(TypeMethodType.Compare) == false)
						 && (m.Name == "Compare")
						 && m.IsGenericMethodDefinition
						 )
				{
					parameters = m.GetParameters();
					if (
						 (parameters.Length == 2)
						 )
					{
						genericMethods.Add(TypeMethodType.Compare, m);
					}
				}

			} // foreach (method)

			Debug.Assert(genericMethods.Count == 3, "Failed to locate one or more generic methods");
		}

		/// <summary>
		/// Compares two properties for sorting
		/// </summary>
		/// <param name="y">Right operand</param>
		/// <returns></returns>
		public int CompareTo(PropertySerializationInfo y)
		{
			int result = this.Version.CompareTo(y.Version);

			if (result == 0)
			{
				result = this.attribute.Order.CompareTo(y.attribute.Order);

				if (result == 0)
				{
					result = string.Compare(this.Name, y.Name, true);
				}
			}

			return result;
		}

		#endregion

	}
}
