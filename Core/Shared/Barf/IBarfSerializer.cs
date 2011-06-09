using System;
using System.Linq;

namespace MySpace.Common.Barf
{
	public interface IBarfSerializer
	{
		void SerializeObject(object instance, BarfSerializationArgs writeArgs);
		object DeserializeObject(BarfDeserializationArgs readArgs);
		void InnerDeserializeObject(ref object instance, BarfDeserializationArgs readArgs);
	}
}
