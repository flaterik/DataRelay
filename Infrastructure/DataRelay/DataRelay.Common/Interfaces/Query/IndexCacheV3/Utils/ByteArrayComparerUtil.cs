using System;
using System.Text;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    /// <summary>
    /// Provides a utilities for comparing byte arrays
    /// </summary>
    internal static class ByteArrayComparerUtil
    {
        private static readonly Encoding stringEncoder = new UTF8Encoding(false, true);

        /// <summary>
        /// Compares the type of the byte array based on data.
        /// </summary>
        /// <param name="arr1">The arr1.</param>
        /// <param name="arr2">The arr2.</param>
        /// <param name="startIndex1">The start index1.</param>
        /// <param name="startIndex2">The start index2.</param>
        /// <param name="count1">The count1. (Only required if dataType is string)</param>
        /// <param name="count2">The count2. (Only required if dataType is string)</param>
        /// <param name="dataType">Type of the data.</param>
        /// <returns>
        /// Value
        /// Condition
        /// Less than zero
        /// <paramref name="x"/> is less than <paramref name="y"/>.
        /// Zero
        /// <paramref name="x"/> equals <paramref name="y"/>.
        /// Greater than zero
        /// <paramref name="x"/> is greater than <paramref name="y"/>.
        /// </returns>
        internal static int CompareByteArrayBasedOnDataType(byte[] arr1,
            byte[] arr2,
            ref int startIndex1,
            ref int startIndex2,
            int count1,
            int count2,
            DataType dataType)
        {
            int retVal = 0;

            switch (dataType)
            {
                case DataType.UInt16:
                    var uint16O1 = BitConverter.ToUInt16(arr1, startIndex1);
                    var uint16O2 = BitConverter.ToUInt16(arr2, startIndex2);
                    retVal = uint16O1.CompareTo(uint16O2);
                    startIndex1 += DataTypeSize.Size[DataType.UInt16];
                    startIndex2 += DataTypeSize.Size[DataType.UInt16];
                    break;

                case DataType.Int16:
                    var int16O1 = BitConverter.ToInt16(arr1, startIndex1);
                    var int16O2 = BitConverter.ToInt16(arr2, startIndex2);
                    retVal = int16O1.CompareTo(int16O2);
                    startIndex1 += DataTypeSize.Size[DataType.Int16];
                    startIndex2 += DataTypeSize.Size[DataType.Int16];
                    break;

                case DataType.UInt32:
                    var uint32O1 = BitConverter.ToUInt32(arr1, startIndex1);
                    var uint32O2 = BitConverter.ToUInt32(arr2, startIndex2);
                    retVal = uint32O1.CompareTo(uint32O2);
                    startIndex1 += DataTypeSize.Size[DataType.UInt32];
                    startIndex2 += DataTypeSize.Size[DataType.UInt32];
                    break;

                case DataType.Int32:
                case DataType.SmallDateTime:
                    var int32O1 = BitConverter.ToInt32(arr1, startIndex1);
                    var int32O2 = BitConverter.ToInt32(arr2, startIndex2);
                    retVal = int32O1.CompareTo(int32O2);
                    startIndex1 += DataTypeSize.Size[DataType.Int32];
                    startIndex2 += DataTypeSize.Size[DataType.Int32];
                    break;

                case DataType.UInt64:
                    var uint64O1 = BitConverter.ToUInt64(arr1, startIndex1);
                    var uint64O2 = BitConverter.ToUInt64(arr2, startIndex2);
                    retVal = uint64O1.CompareTo(uint64O2);
                    startIndex1 += DataTypeSize.Size[DataType.UInt64];
                    startIndex2 += DataTypeSize.Size[DataType.UInt64];
                    break;

                case DataType.Int64:
                case DataType.DateTime:
                    var int64O1 = BitConverter.ToInt64(arr1, startIndex1);
                    var int64O2 = BitConverter.ToInt64(arr2, startIndex2);
                    retVal = int64O1.CompareTo(int64O2);
                    startIndex1 += DataTypeSize.Size[DataType.Int64];
                    startIndex2 += DataTypeSize.Size[DataType.Int64];
                    break;

                case DataType.String:
                    var strO1 = count1 < 1 ? stringEncoder.GetString(arr1) : stringEncoder.GetString(arr1, startIndex1, count1);
                    var strO2 = count2 < 1 ? stringEncoder.GetString(arr2) : stringEncoder.GetString(arr2, startIndex2, count2);
                    retVal = string.Compare(strO1, strO2);
                    startIndex1 += strO1.Length;
                    startIndex2 += strO2.Length;
                    break;

                case DataType.Byte:
                    retVal = arr1[startIndex1].CompareTo(arr2[startIndex2]);
                    startIndex1 += DataTypeSize.Size[DataType.Byte];
                    startIndex2 += DataTypeSize.Size[DataType.Byte];
                    break;

                case DataType.Float:
                    var fltO1 = BitConverter.ToSingle(arr1, startIndex1);
                    var fltO2 = BitConverter.ToSingle(arr2, startIndex2);
                    retVal = fltO1.CompareTo(fltO2);
                    startIndex1 += DataTypeSize.Size[DataType.Float];
                    startIndex2 += DataTypeSize.Size[DataType.Float];
                    break;

                case DataType.Double:
                    var dblO1 = BitConverter.ToDouble(arr1, startIndex1);
                    var dblO2 = BitConverter.ToDouble(arr2, startIndex2);
                    retVal = dblO1.CompareTo(dblO2);
                    startIndex1 += DataTypeSize.Size[DataType.Double];
                    startIndex2 += DataTypeSize.Size[DataType.Double];
                    break;

                case DataType.ByteArray:
                    IsEqualByteArrays(arr1, arr2);
                    break;
            }
            return retVal;
        }

        /// <summary>
        /// Compares the byte arrays.
        /// </summary>
        /// <param name="x">x.</param>
        /// <param name="y">y.</param>
        /// <returns><c>true</c> if x equals y; otherwise, <c>false</c></returns>
        internal static bool CompareByteArrays(byte[] x, byte[] y)
        {
            return IsEqualByteArrays(x, y) == 0;
        }

        /// <summary>
        /// Compares two byte arrays and returns a value indicating whether one is less than, equal to, or greater than the other..
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>
        /// Value
        /// Condition
        /// Less than zero
        /// <paramref name="x"/> is less than <paramref name="y"/>.
        /// Zero
        /// <paramref name="x"/> equals <paramref name="y"/>.
        /// Greater than zero
        /// <paramref name="x"/> is greater than <paramref name="y"/>.
        /// </returns>
        internal unsafe static int IsEqualByteArrays(byte[] x, byte[] y)
        {
            //Null objects
            if (x == null || y == null)
            {
                if (x == null && y == null)	//Both arrays are null								
                    return 0;
                if (y == null)
                    return 1;
                return -1;
            }

            // Compare 4 bytes at a time if possible
            int iterations = x.Length / 4;
            int remainder = x.Length % 4;
            fixed (byte* bPtrX = x)
            {
                fixed (byte* bPtrY = y)
                {
                    int* iPtrX = (int*)bPtrX;
                    int* iPtrY = (int*)bPtrY;
                    int diffInt;

                    // Evens
                    for (int i = 0; i < iterations; i++)
                    {
                        diffInt = *iPtrX - *iPtrY;
                        if (diffInt != 0)
                            return diffInt;
                        iPtrX++;
                        iPtrY++;
                    }
                    // Odds
                    if (remainder > 0)
                    {
                        byte* remainderX = (byte*)iPtrX;
                        byte* remainderY = (byte*)iPtrY;
                        int diffbyte;

                        for (int j = 0; j < remainder; j++)
                        {
                            diffbyte = *remainderX - *remainderY;
                            if (diffbyte != 0)
                                return diffbyte;
                            remainderX++;
                            remainderY++;
                        }
                    }
                }
            }
            return 0;
        }
    }
}