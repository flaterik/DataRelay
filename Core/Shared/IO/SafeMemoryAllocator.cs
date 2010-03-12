using System;
using System.Collections.Generic;
using System.Runtime;

namespace MySpace.Common.IO
{
	/// <summary>
	/// 	<para>Encapsulates methods for allocating memory with <see cref="MemoryFailPoint"/> checks, if necessary.</para>
	/// </summary>
	public static class SafeMemoryAllocator
	{
		private const int _checkThreshold = (10 << 20); // 10 MB

		/// <summary>
		/// Creates a one-dimensional <see cref="Array"/> instance with element type <typeparamref name="T"/>.
		/// </summary>
		/// <param name="size">The size of the array to create.</param>
		/// <typeparam name="T">The type of element in the array to allocate.</typeparam>
		/// <returns>A one-dimensional <see cref="Array"/> instance with element type <typeparamref name="T"/>.</returns>
		/// <exception cref="InsufficientMemoryException">
		///	<para>There is not enough system memory available to allocate the array.</para>
		/// </exception>
		public static T[] CreateArray<T>(int size)
		{
			if (size >= TypeInfo<T>.ElementCountThreshold)
			{
				using (GetFailPoint<T>(size))
				{
					return new T[size];
				}
			}
			return new T[size];
		}

		/// <summary>
		/// Creates a <see cref="List{T}"/> instance with element type <typeparamref name="T"/>.
		/// </summary>
		/// <param name="capacity">The capacity of the <see cref="List{T}"/> to create.</param>
		/// <typeparam name="T">The type of element in the list to allocate.</typeparam>
		/// <returns>A <see cref="List{T}"/> instance with element type <typeparamref name="T"/>.</returns>
		/// <exception cref="InsufficientMemoryException">
		///	<para>There is not enough system memory available to allocate the list.</para>
		/// </exception>
		public static List<T> CreateList<T>(int capacity)
		{
			if (capacity >= TypeInfo<T>.ElementCountThreshold)
			{
				using (GetFailPoint<T>(capacity))
				{
					return new List<T>(capacity);
				}
			}
			return new List<T>(capacity);
		}

		/// <summary>
		/// Creates a <see cref="Dictionary{TKey, TValue}"/>.
		/// </summary>
		/// <param name="capacity">The capacity of the <see cref="Dictionary{TKey, TValue}"/> to create.</param>
		/// <typeparam name="TKey">The key type of the dictionary.</typeparam>
		/// <typeparam name="TValue">The value type of the dictionary.</typeparam>
		/// <returns>A <see cref="Dictionary{TKey, TValue}"/> instance with key type <typeparamref name="TKey"/>
		/// and value type <typeparamref name="TValue"/>.</returns>
		/// <exception cref="InsufficientMemoryException">
		///	<para>There is not enough system memory available to allocate the list.</para>
		/// </exception>
		public static Dictionary<TKey, TValue> CreateDictionary<TKey, TValue>(int capacity)
		{
			if (capacity >= TypeInfo<TKey, TValue>.ElementCountThreshold)
			{
				using (GetFailPoint<TKey, TValue>(capacity))
				{
					return new Dictionary<TKey, TValue>(capacity);
				}
			}
			return new Dictionary<TKey, TValue>();
		}

		private static MemoryFailPoint GetFailPoint<T>(int elementCount)
		{
			var megabytes = ((long)TypeInfo<T>.ApproximateElementSize * (long)elementCount) >> 20;
			if (megabytes <= 0) megabytes = 1;
			return new MemoryFailPoint((int)megabytes);
		}

		private static MemoryFailPoint GetFailPoint<T1, T2>(int elementCount)
		{
			var megabytes = ((long)TypeInfo<T1, T2>.ApproximateElementSize * (long)elementCount) >> 20;
			if (megabytes <= 0) megabytes = 1;
			return new MemoryFailPoint((int)megabytes);
		}

		private static class TypeInfo<T>
		{
			public static readonly int ElementCountThreshold;
			public static readonly int ApproximateElementSize;

			private static readonly Dictionary<Type, int> _simpleGenericTypes = new Dictionary<Type, int>
			{
				// the key is the generic type definition and the value is the overhead on the type
				// so the total size of the type will be the sum of the type arguments and the overhead
				{ typeof(int?).GetGenericTypeDefinition(), sizeof(bool) },
				{ typeof(KeyValuePair<int, int>).GetGenericTypeDefinition(), 0 }
			};

			private static readonly Dictionary<Type, int> _knownValueTypes = new Dictionary<Type, int>
			{
				// key is the type, and the value is the size of the type
				// primitives
				{ typeof(bool), sizeof(bool) },
				{ typeof(byte), sizeof(byte) },
				{ typeof(sbyte), sizeof(sbyte) },
				{ typeof(short), sizeof(short) },
				{ typeof(ushort), sizeof(ushort) },
				{ typeof(int), sizeof(int) },
				{ typeof(uint), sizeof(uint) },
				{ typeof(long), sizeof(long) },
				{ typeof(ulong), sizeof(ulong) },
				{ typeof(float), sizeof(float) },
				{ typeof(double), sizeof(double) },
				{ typeof(char), sizeof(char) },
				{ typeof(decimal), sizeof(decimal) },
				// common value types
				{ typeof(IntPtr), IntPtr.Size },
				{ typeof(Guid), 16 },
				{ typeof(DateTime), 8 },
				{ typeof(TimeSpan), 8 }
			};

			/// <summary>
			/// 	<para>Initializes static members of the <see cref="TypeInfo{T}"/> class.</para>
			/// </summary>
			static TypeInfo()
			{
				ApproximateElementSize = SizeOf(typeof(T));
				ElementCountThreshold = _checkThreshold / ApproximateElementSize;
			}

			private static int SizeOf(Type type)
			{
				if (type.IsClass)
				{
					return IntPtr.Size;
				}
				int overhead;
				if (type.IsGenericType && _simpleGenericTypes.TryGetValue(type.GetGenericTypeDefinition(), out overhead))
				{
					int result = 0;
					foreach (var arg in type.GetGenericArguments())
					{
						result += SizeOf(arg);
					}
					return result + overhead;
				}
				if (type.IsEnum)
				{
					return SizeOf(Enum.GetUnderlyingType(type));
				}
				int size;
				if (_knownValueTypes.TryGetValue(type, out size))
				{
					return size;
				}
				// we don't know what the real size is so assume it's 16 bytes.
				// Microsoft recommends that structs shouldn't be bigger than this
				// anyway so it's unlikely that types will be bigger than this.
				return 16;
			}
		}

		private static class TypeInfo<T1, T2>
		{
			public static readonly int ElementCountThreshold;
			public static readonly int ApproximateElementSize;

			static TypeInfo()
			{
				ApproximateElementSize
					= TypeInfo<T1>.ApproximateElementSize
					+ TypeInfo<T2>.ApproximateElementSize;
				ElementCountThreshold = _checkThreshold / ApproximateElementSize;
			}
		}
	}
}
