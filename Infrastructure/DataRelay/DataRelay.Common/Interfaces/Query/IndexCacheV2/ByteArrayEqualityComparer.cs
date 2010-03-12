using System;
using System.Collections.Generic;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV2
{
	public class ByteArrayEqualityComparer : EqualityComparer<byte[]>
	{
		public override bool Equals(byte[] x, byte[] y)
		{
			return CacheIndexExtractionUtility.CompareByteArrays(x, y);
		}

		public override int GetHashCode(byte[] obj)
		{
			return BitConverter.ToString(obj).GetHashCode();
		}
	}
}
