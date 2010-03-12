using System;
using System.Collections.Generic;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    /// <summary>
    /// EqualityComparer for a ByteArray
    /// </summary>
    internal class ByteArrayEqualityComparer : EqualityComparer<byte[]>
    {
        public override bool Equals(byte[] x, byte[] y)
        {
            return ByteArrayComparerUtil.CompareByteArrays(x, y);
        }

        public override int GetHashCode(byte[] obj)
        {
            return BitConverter.ToString(obj).GetHashCode();
        }
    }
}
