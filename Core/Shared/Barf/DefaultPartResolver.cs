using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using MySpace.Common.Barf.Parts;

namespace MySpace.Common.Barf
{
	internal class DefaultPartResolver : IPartResolver
	{
		private delegate IPartBuilder PartBuilderFactory(Type type, PartDefinition partDefinition);

		private static IEnumerable<KeyValuePair<Type, IPartBuilder>> GetReadyParts(int frameworkVersion)
		{
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(string), new PrimitivePartBuilder(typeof(string)));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(float), new PrimitivePartBuilder(typeof(float)));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(double), new PrimitivePartBuilder(typeof(double)));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(uint), new PrimitivePartBuilder(typeof(uint)));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(ulong), new PrimitivePartBuilder(typeof(ulong)));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(ushort), new PrimitivePartBuilder(typeof(ushort)));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(sbyte), new PrimitivePartBuilder(typeof(sbyte)));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(char), new PrimitivePartBuilder(typeof(char)));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(byte), new PrimitivePartBuilder(typeof(byte)));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(bool), new PrimitivePartBuilder(typeof(bool)));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(decimal), new PrimitivePartBuilder(typeof(decimal)));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(long), new PrimitivePartBuilder(typeof(long)));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(short), new PrimitivePartBuilder(typeof(short)));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(int), new PrimitivePartBuilder(typeof(int)));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(DateTime), new PrimitivePartBuilder(typeof(DateTime), "ReadRoundTripDateTime", "WriteRoundTripDateTime"));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(Guid), new PrimitiveExtensionPartBuilder(typeof(Guid)));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(byte[]), new PrimitiveExtensionPartBuilder(typeof(byte[])));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(IPAddress), new PrimitiveExtensionPartBuilder(typeof(IPAddress)));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(IPEndPoint), new PrimitiveExtensionPartBuilder(typeof(IPEndPoint)));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(BitArray), new PrimitiveExtensionPartBuilder(typeof(BitArray)));
			yield return new KeyValuePair<Type, IPartBuilder>(typeof(Uri), new PrimitiveExtensionPartBuilder(typeof(Uri)));
		}

		private static IEnumerable<KeyValuePair<Type, PartBuilderFactory>> GetGenericPartFactories(int frameworkVersion, IPartResolver resolver)
		{
			yield return new KeyValuePair<Type, PartBuilderFactory>(
                typeof(Nullable<>),
				(type, partDef) => new NullablePartBuilder(type, partDef, resolver));

			yield return new KeyValuePair<Type, PartBuilderFactory>(
                typeof(KeyValuePair<,>),
				(type, partDef) => new KeyValuePairPartBuilder(type, partDef, resolver));

			PartBuilderFactory collectionFactory = (type, partDef) => new CollectionPartBuilder(type, type.GetGenericArguments()[0], partDef, resolver);
			yield return new KeyValuePair<Type, PartBuilderFactory>(typeof(List<>), collectionFactory);
			yield return new KeyValuePair<Type, PartBuilderFactory>(typeof(Collection<>), collectionFactory);
			yield return new KeyValuePair<Type, PartBuilderFactory>(typeof(LinkedList<>), collectionFactory);
			//yield return new KeyValuePair<Type, PartBuilderFactory>(typeof(Queue<>), collectionFactory);
			//yield return new KeyValuePair<Type, PartBuilderFactory>(typeof(Stack<>), collectionFactory);
			collectionFactory = (type, partDef) =>
			{
				var elementType = type.GetGenericArguments().First<Type>();
				var underlyingType = typeof(List<>).MakeGenericType(elementType);
				return new CollectionPartBuilder(type, elementType, partDef, resolver, underlyingType);
			};
			yield return new KeyValuePair<Type, PartBuilderFactory>(typeof(IList<>), collectionFactory);
			yield return new KeyValuePair<Type, PartBuilderFactory>(typeof(ICollection<>), collectionFactory);

			PartBuilderFactory dictionaryFactory = (type, partDef) =>
            {
                var genericArgs = type.GetGenericArguments();
                var elementType = typeof(KeyValuePair<,>).MakeGenericType(genericArgs[0], genericArgs[1]);
				return new CollectionPartBuilder(type, elementType, partDef, resolver);
			};
			yield return new KeyValuePair<Type, PartBuilderFactory>(typeof(Dictionary<,>), dictionaryFactory);
			yield return new KeyValuePair<Type, PartBuilderFactory>(typeof(SortedDictionary<,>), dictionaryFactory);
			yield return new KeyValuePair<Type, PartBuilderFactory>(typeof(SortedList<,>), dictionaryFactory);
			dictionaryFactory = (type, partDef) =>
			{
				var genericArgs = type.GetGenericArguments();
				var elementType = typeof(KeyValuePair<,>).MakeGenericType(genericArgs[0], genericArgs[1]);
				var underlyingType = typeof(Dictionary<,>).MakeGenericType(genericArgs[0], genericArgs[1]);
				return new CollectionPartBuilder(type, elementType, partDef, resolver, underlyingType);
			};
			yield return new KeyValuePair<Type, PartBuilderFactory>(typeof(IDictionary<,>), dictionaryFactory);
		}

		private readonly Dictionary<Type, IPartBuilder> _readyParts;
		private readonly Dictionary<Type, PartBuilderFactory> _genericParts;
		private readonly int _frameworkVersion;

		internal DefaultPartResolver(int frameworkVersion)
		{
			_frameworkVersion = frameworkVersion;
			_readyParts = new Dictionary<Type, IPartBuilder>();
			foreach (var part in GetReadyParts(frameworkVersion))
			{
				_readyParts.Add(part.Key, part.Value);
			}

			_genericParts = new Dictionary<Type, PartBuilderFactory>();
			foreach (var item in GetGenericPartFactories(frameworkVersion, this))
			{
				_genericParts.Add(item.Key, item.Value);
			}
		}

		public IPartBuilder GetPartBuilder(Type type, PartDefinition partDefinition)
		{
			if (type == null) throw new ArgumentNullException("type");

			IPartBuilder result;

			if (partDefinition.ReadMethod != null || partDefinition.WriteMethod != null)
			{
				return CustomPartBuilder.Create(type, partDefinition);
			}

			if (type.IsGenericParameter)
			{
				return new DeferredPartBuilder(type, partDefinition.Flags);
			}

			if (_readyParts.TryGetValue(type, out result)) return result;

			if (type.IsArray)
			{
				return new ArrayPartBuilder(type, partDefinition, this);
			}

			if (type.IsEnum)
			{
				return new EnumPartBuilder(type, partDefinition, this);
			}

			if (BarfFormatter.IsSerializable(type))
			{
				return new DeferredPartBuilder(type, partDefinition.Flags);
			}

			if (type.IsGenericType)
			{
				var genericTypeDef = type.GetGenericTypeDefinition();
				PartBuilderFactory factory;
				if (_genericParts.TryGetValue(genericTypeDef, out factory))
				{
					return factory(type, partDefinition);
				}
			}

			if (partDefinition.IsDynamic)
			{
				return new DeferredPartBuilder(type, partDefinition.Flags);
			}

			if (typeof(IVersionSerializable).IsAssignableFrom(type))
			{
				return new VersionSerializablePartBuilder(type);
			}

			if (typeof(ICustomSerializable).IsAssignableFrom(type))
			{
				return new CustomSerializablePartBuilder(type);
			}

			var builder = SerializableStructPartBuilder.TryCreate(type, partDefinition, this);
			if (builder != null) return builder;

			if (type.IsSerializable && !partDefinition.IsBaseType)
			{
				return BinaryFormatterPartBuilder.Instance;
			}

			throw new NotSupportedException("The defined member cannot be serialized because it and/or one if its nested types are not serializable - " + partDefinition);
		}

		public int FrameworkVersion
		{
			[DebuggerStepThrough]
			get { return _frameworkVersion; }
		}
	}
}
