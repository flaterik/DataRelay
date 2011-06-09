using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using MySpace.Common.Dynamic;

namespace MySpace.Common
{
	/// <summary>
	/// Encapsulates methods for mapping two instances that implement a common contract.
	/// </summary>
	public sealed class Mapper
	{
		private static readonly Factory<Type, Mapper> _propertyMappersByType = Algorithm.LazyIndexer<Type, Mapper>(CreatePropertyMapper);
		private static int _nextMapperId;

		private static Mapper CreatePropertyMapper(Type type)
		{
			return Create(instructions =>
			{
				const BindingFlags propertyFlags = BindingFlags.Public | BindingFlags.Instance;

				var contractProperties = type.GetProperties(propertyFlags);
				PropertyInfo[] destProperties = null;

				foreach (var property in type.GetProperties(propertyFlags))
				{
					if (!property.CanRead) continue;
					var commonGetter = property.GetGetMethod(true);
					var sourceGetter = instructions.SourceType.GetBestCallableOverride(commonGetter);

					MethodInfo destSetter;
					if (property.CanWrite)
					{
						destSetter = instructions.DestinationType.GetBestCallableOverride(property.GetSetMethod(true));
					}
					else
					{
						if (destProperties == null)
						{
							destProperties = instructions.DestinationType == type ? contractProperties : instructions.DestinationType.GetProperties(propertyFlags);
						}
						var destGetter = instructions.DestinationType.GetBestCallableOverride(commonGetter);
						var destProperty = destProperties
							.Where<PropertyInfo>(prop => prop.CanRead && prop.GetGetMethod(true) == destGetter)
							.First<PropertyInfo>();
						if (!destProperty.CanWrite)
						{
							throw new MissingMethodException(instructions.DestinationType.FullName, "set_" + destProperty.Name);
						}
						destSetter = destProperty.GetSetMethod(true);
					}
					instructions.AddMapping(sourceGetter, destSetter);
				}
			});
		}

		/// <summary>
		/// Copies all properties of <typeparamref name="T"/> from <paramref name="source"/>
		/// to <paramref name="destination"/>. Write-only properties are ignored. Exceptions
		/// are thrown in cases where a property cannot be written to <paramref name="destination"/>.
		/// </summary>
		/// <param name="source">The source to read from.</param>
		/// <param name="destination">The destination instance to write to.</param>
		/// <exception cref="MissingMethodException">
		///		<para>A destination setter property is missing.</para>
		/// </exception>
		public static void MapProperties<T>(T source, T destination)
			where T : class
		{
			if (source == null) throw new ArgumentNullException("source");
			if (destination == null) throw new ArgumentNullException("destination");

			var mapper = _propertyMappersByType(typeof(T));
			mapper.Map(source, destination);
		}

		/// <summary>
		/// Creates the a mapper from the builder implementation.
		/// </summary>
		/// <param name="mapperBuilder">The builder implementation.</param>
		/// <returns>A new mapper.</returns>
		public static Mapper Create(Action<MappingInstructions> mapperBuilder)
		{
			if (mapperBuilder == null) throw new ArgumentNullException("mapperBuilder");

			return new Mapper(mapperBuilder);
		}

		private readonly Factory<MapperKey, Action<object, object>> _mappers;

		private Mapper(Action<MappingInstructions> mapperBuilder)
		{
			_mappers = Algorithm.LazyIndexer<MapperKey, Action<object, object>>(key =>
			{
				var instructions = new MappingInstructions(key.SourceType, key.DestType);
				mapperBuilder(instructions);
				return GenerateMapper(instructions);
			},
			new MapperKeyEqualityComparer());
		}

		/// <summary>
		/// Maps the <paramref name="source"/> to <paramref name="destination"/>.
		/// </summary>
		/// <param name="source">The source object.</param>
		/// <param name="destination">The destination object.</param>
		/// <exception cref="ArgumentNullException">
		///		<para><paramref name="source"/> is <see langword="null"/>.</para>
		///		<para>- or -</para>
		///		<para><paramref name="destination"/> is <see langword="null"/>.</para>
		/// </exception>
		public void Map(object source, object destination)
		{
			if(source == null) throw new ArgumentNullException("source");
			if (destination == null) throw new ArgumentNullException("destination");

			var mapper = _mappers(new MapperKey(source.GetType(), destination.GetType()));
			mapper(source, destination);
		}

		private static Action<object, object> GenerateMapper(MappingInstructions instructions)
		{
			var dm = new DynamicMethod(
				"Map" + instructions.SourceType.Name + "To" + instructions.SourceType.Name + Interlocked.Increment(ref _nextMapperId),
				typeof(void),
				new[] { typeof(object), typeof(object) },
				true);
			var ilGen = dm.GetILGenerator();

			foreach (var mapping in instructions.GetMappings())
			{
				mapping.GenerateMap(ilGen, 0, 1);
			}

			ilGen.Emit(OpCodes.Ret);
			return (Action<object, object>)dm.CreateDelegate(typeof(Action<object, object>));
		}

		private struct MapperKey
		{
			private readonly Type _sourceType;
			private readonly Type _destType;

			public MapperKey(Type sourceType, Type destType)
			{
				_sourceType = sourceType;
				_destType = destType;
			}

			public Type SourceType
			{
				get { return _sourceType; }
			}

			public Type DestType
			{
				get { return _destType; }
			}
		}

		private class MapperKeyEqualityComparer : IEqualityComparer<MapperKey>
		{
			#region IEqualityComparer<MapperKey<T>> Members

			public bool Equals(MapperKey x, MapperKey y)
			{
				return x.DestType == y.DestType
					&& x.SourceType == y.SourceType;
			}

			public int GetHashCode(MapperKey obj)
			{
				return obj.DestType.GetHashCode() ^ obj.SourceType.GetHashCode();
			}

			#endregion
		}
	}
}
