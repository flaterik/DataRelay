using System;
using System.IO;
using System.Linq;
using MySpace.Common.IO;

namespace MySpace.Common.Barf
{
	public abstract class BarfSerializer<T> : IBarfSerializer
	{
		private readonly LazyInitializer<TypeSerializationInfo> _typeSerializer = new LazyInitializer<TypeSerializationInfo>(() => TypeSerializationInfo.GetTypeInfo(typeof(T)));

		/// <summary>
		/// Initializes a new instance of the <see cref="BarfSerializer{T}"/> class.
		/// </summary>
		protected BarfSerializer() { }

		public abstract void Serialize(T instance, BarfSerializationArgs writeArgs);

		public T Deserialize(BarfDeserializationArgs readArgs)
		{
			var value = readArgs.Reader.ReadByte();
			if (value == SerializerHeaders.Barf)
			{
				var result = default(T);
				InnerDeserialize(ref result, readArgs);
				return result;
			}
			else
			{
				readArgs.Reader.BaseStream.Seek(-1L, SeekOrigin.Current);

				object result = CreateEmpty();
				_typeSerializer.Value.Deserialize(ref result, new TypeSerializationArgs
				{
					Reader = readArgs.Reader,
					Flags = SerializerFlags.Default
				});
				return (T)result;
			}
		}

		protected internal abstract void InnerDeserialize(ref T instance, BarfDeserializationArgs readArgs);

		protected abstract T CreateEmpty();

		public void SerializeObject(object instance, BarfSerializationArgs writeArgs)
		{
			Serialize((T)instance, writeArgs);
		}

		public object DeserializeObject(BarfDeserializationArgs readArgs)
		{
			return Deserialize(readArgs);
		}

		public void InnerDeserializeObject(ref object instance, BarfDeserializationArgs readArgs)
		{
			var typedInstance = instance == null ? default(T) : (T)instance;
			InnerDeserialize(ref typedInstance, readArgs);
			instance = typedInstance;
		}
	}
}
