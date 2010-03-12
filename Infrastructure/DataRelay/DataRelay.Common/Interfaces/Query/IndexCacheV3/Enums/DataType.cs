using System;
using System.Collections.Generic;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public enum DataType : byte
    {
        // Add data types to the end and *Add DataType size to the dictionary below*
        UInt16,
        Int16,
        UInt32,
        Int32,
        UInt64,
        Int64,
        SmallDateTime,
        DateTime,
        String,
        Byte,
        Float,
        Double,
        ByteArray
    }

    public static class DataTypeSize
    {
        public static Dictionary<DataType, int> Size;

        /// <summary>
        /// Initializes the <see cref="DataTypeSize"/> class.
        /// </summary>
        static DataTypeSize()
        {
            Size = new Dictionary<DataType, int>
                       {
                           {DataType.UInt16, sizeof (UInt16)},
                           {DataType.Int16, sizeof (Int16)},
                           {DataType.UInt32, sizeof (UInt32)},
                           {DataType.Int32, sizeof (Int32)},
                           {DataType.UInt64, sizeof (UInt64)},
                           {DataType.Int64, sizeof (Int64)},
                           {DataType.SmallDateTime, sizeof (Int32)},
                           {DataType.DateTime, sizeof (Int64)},
                           {DataType.String, -1},
                           {DataType.Byte, sizeof (byte)},
                           {DataType.Float, sizeof (float)},
                           {DataType.Double, sizeof (double)},
                           {DataType.ByteArray, -1}
                       };
        }
    }
}