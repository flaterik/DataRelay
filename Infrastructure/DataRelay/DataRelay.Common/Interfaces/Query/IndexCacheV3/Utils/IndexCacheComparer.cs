using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Logging;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    internal class IndexCacheComparer : IComparer<Byte[]>, IComparer<List<KeyValuePair<int /*TagHashCode*/, byte[] /*TagValue*/>>>
    {
        #region Data Members
        private int startIndex1;
        private int startIndex2;
        private readonly Encoding stringEncoder = new UTF8Encoding(false, true);
        private const int SIZE_OF_INT16 = sizeof(Int16);
        private const int SIZE_OF_INT32 = sizeof(Int32);
        private const int SIZE_OF_INT64 = sizeof(Int64);
        private const int SIZE_OF_BYTE = sizeof(Byte);
        private const int SIZE_OF_FLOAT = sizeof(Single);
        private const int SIZE_OF_DOUBLE = sizeof(Double);
        private DataType dataType;
        private int hintIndex = -1;
        private static readonly LogWrapper Log = new LogWrapper();
        #endregion

        #region Ctors
        internal IndexCacheComparer()
        {
            Init(null, null);
        }

        internal IndexCacheComparer(SortOrder sortOrder)
        {
            if (sortOrderList == null)
            {
                sortOrderList = new List<SortOrder>();
            }
            sortOrderList.Add(sortOrder);
            Init(sortOrderList, null);
        }

        internal IndexCacheComparer(List<SortOrder> sortOrderList)
        {
            Init(sortOrderList, null);
        }

        private void Init(List<SortOrder> sortOrderList, int? tagHashCode)
        {
            this.sortOrderList = sortOrderList;
            this.tagHashCode = tagHashCode;
            startIndex1 = 0;
            startIndex2 = 0;
        }
        #endregion

        #region Methods
        private List<SortOrder> sortOrderList;
        internal List<SortOrder> SortOrderList
        {
            get
            {
                return sortOrderList;
            }
            set
            {
                sortOrderList = value;
            }
        }

        private int? tagHashCode;
        internal int? TagHashCode
        {
            get
            {
                return tagHashCode;
            }
            set
            {
                tagHashCode = value;
            }
        }

        internal int Compare(byte[] arr1, byte[] arr2, DataType dataType)
        {
            #region Null check for arrays
            if (arr1 == null || arr2 == null)
            {
                if (arr1 == null && arr2 == null)	 //Both arrays are null
                {
                    return 0;
                }
                else	 //One of the arrays is null
                {
                    if (arr1 == null)
                    {
                        return -1;
                    }
                    else
                    {
                        return 1;
                    }
                }
            }
            #endregion

            startIndex1 = 0;
            startIndex2 = 0;
            this.dataType = dataType;

            return CompareIndex(arr1, arr2);
        }

        private int CompareIndex(byte[] arr1, byte[] arr2)
        {
            try
            {
                int retVal = 0;
                short int16o1, int16o2;
                int int32o1, int32o2;
                long int64o1, int64o2;
                ushort uint16o1, uint16o2;
                uint uint32o1, uint32o2;
                ulong uint64o1, uint64o2;
                string stro1, stro2;
                float flto1, flto2;
                double dblo1, dblo2;

                switch (dataType)
                {
                    case DataType.UInt16:
                        uint16o1 = BitConverter.ToUInt16(arr1, startIndex1);
                        uint16o2 = BitConverter.ToUInt16(arr2, startIndex2);
                        retVal = uint16o1.CompareTo(uint16o2);
                        startIndex1 += SIZE_OF_INT16;
                        startIndex2 += SIZE_OF_INT16;
                        break;

                    case DataType.Int16:
                        int16o1 = BitConverter.ToInt16(arr1, startIndex1);
                        int16o2 = BitConverter.ToInt16(arr2, startIndex2);
                        retVal = int16o1.CompareTo(int16o2);
                        startIndex1 += SIZE_OF_INT16;
                        startIndex2 += SIZE_OF_INT16;
                        break;

                    case DataType.UInt32:
                        uint32o1 = BitConverter.ToUInt32(arr1, startIndex1);
                        uint32o2 = BitConverter.ToUInt32(arr2, startIndex2);
                        retVal = uint32o1.CompareTo(uint32o2);
                        startIndex1 += SIZE_OF_INT32;
                        startIndex2 += SIZE_OF_INT32;
                        break;

                    case DataType.Int32:
                    case DataType.SmallDateTime:
                        int32o1 = BitConverter.ToInt32(arr1, startIndex1);
                        int32o2 = BitConverter.ToInt32(arr2, startIndex2);
                        retVal = int32o1.CompareTo(int32o2);
                        startIndex1 += SIZE_OF_INT32;
                        startIndex2 += SIZE_OF_INT32;
                        break;

                    case DataType.UInt64:
                        uint64o1 = BitConverter.ToUInt64(arr1, startIndex1);
                        uint64o2 = BitConverter.ToUInt64(arr2, startIndex2);
                        retVal = uint64o1.CompareTo(uint64o2);
                        startIndex1 += SIZE_OF_INT64;
                        startIndex2 += SIZE_OF_INT64;
                        break;

                    case DataType.Int64:
                    case DataType.DateTime:
                        int64o1 = BitConverter.ToInt64(arr1, startIndex1);
                        int64o2 = BitConverter.ToInt64(arr2, startIndex2);
                        retVal = int64o1.CompareTo(int64o2);
                        startIndex1 += SIZE_OF_INT64;
                        startIndex2 += SIZE_OF_INT64;
                        break;

                    case DataType.String:
                        stro1 = stringEncoder.GetString(arr1);
                        stro2 = stringEncoder.GetString(arr2);
                        retVal = string.Compare(stro1, stro2);
                        startIndex1 += stro1.Length;
                        startIndex2 += stro2.Length;
                        break;

                    case DataType.Byte:
                        retVal = arr1[startIndex1].CompareTo(arr2[startIndex2]);
                        startIndex1 += SIZE_OF_BYTE;
                        startIndex2 += SIZE_OF_BYTE;
                        break;

                    case DataType.Float:
                        flto1 = BitConverter.ToSingle(arr1, startIndex1);
                        flto2 = BitConverter.ToSingle(arr2, startIndex2);
                        retVal = flto1.CompareTo(flto2);
                        startIndex1 += SIZE_OF_FLOAT;
                        startIndex2 += SIZE_OF_FLOAT;
                        break;

                    case DataType.Double:
                        dblo1 = BitConverter.ToDouble(arr1, startIndex1);
                        dblo2 = BitConverter.ToDouble(arr2, startIndex2);
                        retVal = dblo1.CompareTo(dblo2);
                        startIndex1 += SIZE_OF_DOUBLE;
                        startIndex2 += SIZE_OF_DOUBLE;
                        break;
                }
                return retVal;
            }
            catch (Exception)
            {
                Log.Error("Error inside IndexCacheComparer");
                Log.ErrorFormat("arr1.Length = {0}, arr1 = {1}", arr1.Length, GetReadableByteArray(arr1));
                Log.ErrorFormat("startIndex1 = {0}", startIndex1);
                Log.ErrorFormat("arr2.Length = {0}, arr2 = {1}", arr2.Length, GetReadableByteArray(arr2));
                Log.ErrorFormat("startIndex2 = {0}", startIndex2);
                Log.ErrorFormat("IndexCacheComparer.TagHashCode = {0}", tagHashCode);
                Log.Error("IndexCacheComparer.SortOrderList = ");
                int i = 0;
                foreach (SortOrder so in sortOrderList)
                {
                    Log.ErrorFormat("SortOrderList[{0}].DataType = {1}", i, so.DataType);
                    Log.ErrorFormat("SortOrderList[{0}].SortBy = {1}", i, so.SortBy);
                    i++;
                }
                throw;
            }
        }

        private static string GetReadableByteArray(byte[] buffer)
        {
            string retVal;
            if (buffer == null || buffer.Length == 0)
            {
                retVal = "Null Buffer";
            }
            else
            {
                if (buffer.Length == 4)
                {
                    retVal = BitConverter.ToInt32(buffer, 0).ToString();
                }
                else
                {
                    retVal = "Bytes: ";
                    foreach (byte b in buffer)
                    {
                        retVal += ((int)b) + " ";
                    }
                }
            }

            return retVal;

        }
        #endregion

        #region IComparer<byte[]> Members
        public int Compare(byte[] arr1, byte[] arr2)
        {
            if (sortOrderList == null || sortOrderList.Count < 1)
            {
                if (Log.IsErrorEnabled)
                    Log.Error("Empty sortOrderList in IndexCacheComparer");
                throw new Exception("Empty sortOrderList in IndexCacheComparer");
            }

            int retVal = 0;
            startIndex1 = 0;
            startIndex2 = 0;

            #region Null check for arrays
            if (arr1 == null || arr2 == null)
            {
                if (arr1 == null && arr2 == null)									//Both arrays are null
                {
                    return 0;
                }
                else if (sortOrderList[0].SortBy == SortBy.ASC)		//One of the arrays is null and order is ASC
                {
                    if (arr1 == null)
                    {
                        return -1;
                    }
                    else
                    {
                        return 1;
                    }
                }
                else																					//One of the arrays is null and order is DESC
                {
                    if (arr1 == null)
                    {
                        return 1;
                    }
                    else
                    {
                        return -1;
                    }
                }
            }
            #endregion

            #region Length check for arrays
            if (arr1.Length != arr2.Length)
            {
                if (arr1.Length > arr2.Length)
                {
                    return 1;
                }
                else
                {
                    return -1;
                }
            }
            #endregion

            for (int i = 0; i < sortOrderList.Count && retVal == 0; i++)
            {
                dataType = sortOrderList[i].DataType;
                retVal = (sortOrderList[i].SortBy == SortBy.ASC) ? CompareIndex(arr1, arr2) : CompareIndex(arr2, arr1);
            }
            return retVal;
        }
        #endregion

        #region IComparer<List<KeyValuePair<int,byte[]>>> Members
        public int Compare(List<KeyValuePair<int, byte[]>> tagList1, List<KeyValuePair<int, byte[]>> tagList2)
        {
            int retVal = 0;
            startIndex1 = 0;
            startIndex2 = 0;

            if (tagHashCode == null)
            {
                if (Log.IsErrorEnabled)
                    Log.Error("Empty tagHashCode in IndexCacheComparer");
                throw new Exception("Empty tagHashCode in IndexCacheComparer");
            }
            if (sortOrderList == null || sortOrderList.Count < 1)
            {
                if (Log.IsErrorEnabled)
                    Log.Error("Empty sortOrderList in IndexCacheComparer");
                throw new Exception("Empty sortOrderList in IndexCacheComparer");
            }

            #region Null check for lists
            if (tagList1 == null || tagList2 == null)
            {
                if (tagList1 == null && tagList2 == null)									//Both lists are null
                {
                    return 0;
                }
                else if (sortOrderList[0].SortBy == SortBy.ASC)					  //One of the lists is null and order is ASC
                {
                    if (tagList1 == null)
                    {
                        return -1;
                    }
                    else
                    {
                        return 1;
                    }
                }
                else																								//One of the lists is null and order is DESC
                {
                    if (tagList1 == null)
                    {
                        return 1;
                    }
                    else
                    {
                        return -1;
                    }
                }
            }
            #endregion

            byte[] tag1 = GetTagValue(tagList1);
            byte[] tag2 = GetTagValue(tagList2);

            #region Null check for tags
            if (tag1 == null || tag2 == null)
            {
                if (tag1 == null && tag2 == null)										//Both tags are null
                {
                    return 0;
                }
                else if (sortOrderList[0].SortBy == SortBy.ASC)			  //One of the tags is null and order is ASC
                {
                    if (tag1 == null)
                    {
                        return -1;
                    }
                    else
                    {
                        return 1;
                    }
                }
                else																							//One of the tags is null and order is DESC
                {
                    if (tag1 == null)
                    {
                        return 1;
                    }
                    else
                    {
                        return -1;
                    }
                }
            }
            #endregion

            dataType = sortOrderList[0].DataType;

            retVal = (sortOrderList[0].SortBy == SortBy.ASC) ? CompareIndex(tag1, tag2) : CompareIndex(tag2, tag1);

            return retVal;
        }

        private byte[] GetTagValue(List<KeyValuePair<int, byte[]>> tagList)
        {
            //Try hint first
            if (hintIndex > -1 && tagList.Count > hintIndex)
            {
                if (tagList[hintIndex].Key == tagHashCode)
                {
                    return tagList[hintIndex].Value;
                }
            }

            //Linear Search on tagList
            for (int i = 0; i < tagList.Count; i++)
            {
                if (tagList[i].Key == tagHashCode)
                {
                    hintIndex = i;
                    return tagList[i].Value;
                }
            }
            return null;
        }
        #endregion
    }
}