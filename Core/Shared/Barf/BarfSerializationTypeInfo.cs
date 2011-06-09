using System;
using System.Linq;

namespace MySpace.Common.Barf
{
	internal struct BarfSerializationTypeInfo
	{
		public static BarfSerializationTypeInfo Create(BarfObjectHeader originalHeader, byte[] futureData)
		{
			return new BarfSerializationTypeInfo
			{
				OriginalHeader = originalHeader,
				FutureData = futureData
			};
		}

		public BarfObjectHeader OriginalHeader { get; private set; }
		public byte[] FutureData { get; private set; }
	}
}
