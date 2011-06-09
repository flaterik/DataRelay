using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.Serialization.Formatters.Binary;
using MySpace.Common.Dynamic;
using MySpace.Common.Dynamic.Reflection;

namespace MySpace.Common.Barf
{
	/// <summary>
	/// Encapsulates methods for retrieving <see cref="BarfSerializer{T}"/>, <see cref="IBarfSerializer"/>,
	/// <see cref="IBarfTester{T}"/>, and <see cref="IBarfTester"/> implementations.
	/// </summary>
	internal static class BarfSerializers
	{
		private static readonly Factory<BarfTypeKey, IBarfSerializer> _serializers
			= Algorithm.LazyIndexer<BarfTypeKey, IBarfSerializer>(GetSerializer, new BarfTypeKeyComparer());
		private static readonly Factory<BarfTypeKey, IBarfTester> _testers
			= Algorithm.LazyIndexer<BarfTypeKey, IBarfTester>(GetTester, new BarfTypeKeyComparer());
		private static readonly Factory<int, IPartResolver> _resolversByFrameworkVersion
			= Algorithm.LazyIndexer<int, IPartResolver>(v => new DefaultPartResolver(v));

		private static IBarfSerializer GetSerializer(BarfTypeKey key)
		{
			var type = typeof(RuntimeBarfSerializer<>).MakeGenericType(key.Type);
			return (IBarfSerializer)Activator.CreateInstance(type, key.FrameworkVersion);
		}

		private static IBarfTester GetTester(BarfTypeKey key)
		{
			var type = typeof(RuntimeBarfTester<>).MakeGenericType(key.Type);
			return (IBarfTester)Activator.CreateInstance(type, key.FrameworkVersion);
		}

		/// <summary>
		/// Gets an instance of the proper <see cref="BarfSerializer{T}"/> implementation given the specified <paramref name="flags"/>.
		/// </summary>
		/// <typeparam name="T">The type that the <see cref="BarfSerializer{T}"/> implementation will serialize.</typeparam>
		/// <param name="flags">Flags that affect the serialization.</param>
		/// <returns>An instance of the proper <see cref="BarfSerializer{T}"/> implementation given the specified <paramref name="flags"/>.</returns>
		public static BarfSerializer<T> Get<T>(PartFlags flags)
		{
			return (BarfSerializer<T>)_serializers(new BarfTypeKey(typeof(T), BarfFormatter.MaxFrameworkVersion));
		}

		/// <summary>
		/// Gets an instance of the proper <see cref="IBarfSerializer"/> implementation given the specified <paramref name="flags"/>.
		/// </summary>
		/// <param name="type">The type that the <see cref="IBarfSerializer"/> implementation will serialize.</param>
		/// <param name="flags">Flags that affect the serialization.</param>
		/// <returns>An instance of the proper <see cref="IBarfSerializer"/> implementation given the specified <paramref name="flags"/>.</returns>
		public static IBarfSerializer Get(Type type, PartFlags flags)
		{
			return _serializers(new BarfTypeKey(type, BarfFormatter.MaxFrameworkVersion));
		}

		/// <summary>
		/// Gets an instance of the proper <see cref="IBarfTester"/> implementation given the specified <paramref name="flags"/>.
		/// </summary>
		/// <param name="type">The type that the <see cref="IBarfTester"/> implementation will test.</param>
		/// <param name="flags">Flags that affect the serialization.</param>
		/// <returns>An instance of the proper <see cref="IBarfTester"/> implementation given the specified <paramref name="flags"/>.</returns>
		public static IBarfTester GetTester(Type type, PartFlags flags)
		{
			return _testers(new BarfTypeKey(type, BarfFormatter.MaxFrameworkVersion));
		}

		/// <summary>
		/// Gets an instance of the proper <see cref="IBarfTester{T}"/> implementation given the specified <paramref name="flags"/>.
		/// </summary>
		/// <typeparam name="T">The type that the <see cref="IBarfTester{T}"/> implementation will test.</typeparam>
		/// <param name="flags">Flags that affect the serialization.</param>
		/// <returns>An instance of the proper <see cref="IBarfTester{T}"/> implementation given the specified <paramref name="flags"/>.</returns>
		public static IBarfTester<T> GetTester<T>(PartFlags flags)
		{
			return (IBarfTester<T>)_testers(new BarfTypeKey(typeof(T), BarfFormatter.MaxFrameworkVersion));
		}

		private struct BarfTypeKey
		{
			public BarfTypeKey(Type type, int frameworkVersion)
			{
				Type = type;
				FrameworkVersion = frameworkVersion;
			}

			public Type Type;
			public int FrameworkVersion;
		}

		private class BarfTypeKeyComparer : IEqualityComparer<BarfTypeKey>
		{
			public bool Equals(BarfTypeKey x, BarfTypeKey y)
			{
				return x.Type == y.Type
					&& x.FrameworkVersion == y.FrameworkVersion;
			}

			public int GetHashCode(BarfTypeKey obj)
			{
				return obj.Type.GetHashCode() ^ obj.FrameworkVersion.GetHashCode();
			}
		}

		private class RuntimeBarfSerializer<T> : BarfSerializer<T>
		{
			private delegate void DeserializeMethod(ref T instance, BarfDeserializationArgs args);
			private readonly int _frameworkVersion;
			private readonly LazyInitializer<Action<T, BarfSerializationArgs>> _serializeMethod = new LazyInitializer<Action<T, BarfSerializationArgs>>(() =>
			{
				var builder = new BarfSerializerBuilder(BarfTypeDefinition.Get<T>(true));
				var dm = new DynamicMethod(
					Guid.NewGuid().ToString("N") + "_Serialize_" + typeof(T).Name,
					typeof(void),
					new[] { typeof(T), typeof(BarfSerializationArgs) },
					true);
				using (var writer = new MsilWriter(dm))
				{
					builder.GenerateSerializeMethod(writer);
				}
				return (Action<T, BarfSerializationArgs>)dm.CreateDelegate(typeof(Action<T, BarfSerializationArgs>));
			});
			private readonly LazyInitializer<DeserializeMethod> _deserializeMethod = new LazyInitializer<DeserializeMethod>(() =>
			{
				var builder = new BarfSerializerBuilder(BarfTypeDefinition.Get<T>(true));
				var dm = new DynamicMethod(
					Guid.NewGuid().ToString("N") + "_InnerDeserialize_" + typeof(T).Name,
					typeof(void),
					new[] { typeof(T).ResolveByRef(), typeof(BarfDeserializationArgs) },
					true);
				using (var writer = new MsilWriter(dm))
				{
					builder.GenerateInnerDeserializeMethod(writer);
				}
				return (DeserializeMethod)dm.CreateDelegate(typeof(DeserializeMethod));
			});
			private readonly LazyInitializer<Factory<T>> _createEmtpyMethod = new LazyInitializer<Factory<T>>(() =>
			{
				if (typeof(T).IsValueType)
				{
					return () => default(T);
				}
				return DynamicMethods.GetCtor<T>();
			});

			public RuntimeBarfSerializer(int frameworkVersion)
			{
				_frameworkVersion = frameworkVersion;
			}

			public override void Serialize(T instance, BarfSerializationArgs writeArgs)
			{
				_serializeMethod.Value(instance, writeArgs);
			}

			protected internal override void InnerDeserialize(ref T instance, BarfDeserializationArgs readArgs)
			{
				_deserializeMethod.Value(ref instance, readArgs);
			}

			protected override T CreateEmpty()
			{
				return _createEmtpyMethod.Value();
			}
		}

		private class RuntimeBarfTester<T> : IBarfTester<T>
		{
			private delegate void FillMethod(ref T instance, FillArgs args);
			private readonly int _frameworkVersion;
			private readonly LazyInitializer<FillMethod> _fill = new LazyInitializer<FillMethod>(() =>
			{
				var def = BarfTypeDefinition.Get<T>(true);
				var builder = new BarfTesterBuilder(def);

				var dm = new DynamicMethod(
					Guid.NewGuid().ToString("N") + "_Fill_" + typeof(T).Name,
					typeof(void),
					new[] { typeof(T).MakeByRefType(), typeof(FillArgs) },
					true);
				var writer = new MsilWriter(dm);
				builder.GenerateFill(writer);
				return (FillMethod)dm.CreateDelegate(typeof(FillMethod));
			});
			private readonly LazyInitializer<Action<T, T, AssertArgs>> _assert = new LazyInitializer<Action<T, T, AssertArgs>>(() =>
			{
				var def = BarfTypeDefinition.Get<T>(true);
				var builder = new BarfTesterBuilder(def);

				var dm = new DynamicMethod(
						Guid.NewGuid().ToString("N") + "_AssertAreEqual_" + typeof(T).Name,
						typeof(void),
						new[] { typeof(T), typeof(T), typeof(AssertArgs) },
						true);
				var writer = new MsilWriter(dm);
				builder.GenerateAssertAreEqual(writer);
				return (Action<T, T, AssertArgs>)dm.CreateDelegate(typeof(Action<T, T, AssertArgs>));
			});

			public RuntimeBarfTester(int frameworkVersion)
			{
				_frameworkVersion = frameworkVersion;
			}

			void IBarfTester.Fill(ref object instance, FillArgs args)
			{
				T temp = default(T);
				Fill(ref temp, args);
				instance = temp;
			}

			public void AssertAreEqual(object expected, object actual, AssertArgs args)
			{
				AssertAreEqual((T)expected, (T)actual, args);
			}

			public void Fill(ref T instance, FillArgs args)
			{
				_fill.Value(ref instance, args);
			}

			public void AssertAreEqual(T expected, T actual, AssertArgs args)
			{
				_assert.Value(expected, actual, args);
			}
		}
	}
}
