using System;
using System.Collections.Generic;
using System.Linq;
using MySpace.Common.IO;

namespace MySpace.Common.Barf
{
	internal class BarfSerializationInfo : SerializationInfo
	{
		private readonly Dictionary<Type, BarfSerializationTypeInfo> _perTypeInfo = new Dictionary<Type,BarfSerializationTypeInfo>();

		public bool TryGet(Type type, out BarfSerializationTypeInfo info)
		{
			return _perTypeInfo.TryGetValue(type, out info);
		}

		public void Add(Type type, BarfSerializationTypeInfo info)
		{
			_perTypeInfo.Add(type, info);
		}
	}
}
