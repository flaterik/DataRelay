using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV2
{

	public class IndexCacheComparer:IComparer<Byte[]>
	{
		private int startIndex1;
		private int startIndex2;
		private Encoding stringEncoder = new UTF8Encoding(false, true);
		private const int SIZE_OF_INT16 = sizeof(Int16);
		private const int SIZE_OF_INT32 = sizeof(Int32);
		private const int SIZE_OF_INT64 = sizeof(Int64);
		private char[] charArr = { ' ', ',' };

		private string[] dataTypes;
		public string SortString
		{
			set
			{
				if (value != null)
				{
					dataTypes = value.Split(charArr);
				}
			}
		}

		public IndexCacheComparer()
		{
			this.SortString = null;
		}

		public IndexCacheComparer(string sortString)
		{
			this.SortString = sortString;
			startIndex1 = 0;
			startIndex2 = 0;
		}

		#region IComparer<byte[]> Members
		public int Compare(byte[] arr1, byte[] arr2)
		{
			int retVal = 0;
			startIndex1 = 0;
			startIndex2 = 0;

			#region Null check for arrays
			if (arr1 == null || arr2 == null)
			{
				if (arr1 == null && arr2 == null)										//Both arrays are null
				{
					return 0;
				}
				else if (string.Compare(dataTypes[1], "ASC") == 0) //One of the arrays is null and order is ASC
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
				else																						//One of the arrays is null and order is DESC
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

			for (int i = 0; i < dataTypes.Length && retVal == 0; i += 2)
			{
				retVal = (string.Compare(dataTypes[i + 1], "ASC") == 0 ? 
					CompareIndex(arr1, arr2, dataTypes[i]) : 
					CompareIndex(arr2, arr1, dataTypes[i]));
			}
			return retVal;
		}
		#endregion

		private int CompareIndex(byte[] arr1, byte[] arr2, string datatype)
		{
			int retVal = 0;
			int int32o1, int32o2;
			long int64o1, int64o2;
			string stro1, stro2;
			Int16 int16Len1, int16Len2;

			switch (datatype)
			{
				case "Int32":
				case "SmallDateTime":
					int32o1 = BitConverter.ToInt32(arr1, startIndex1);
					int32o2 = BitConverter.ToInt32(arr2, startIndex2);
					retVal = int32o1.CompareTo(int32o2);
					startIndex1 += SIZE_OF_INT32;
					startIndex2 += SIZE_OF_INT32;
					break;

				case "Int64":
				case "DateTime":
					int64o1 = BitConverter.ToInt64(arr1, startIndex1);
					int64o2 = BitConverter.ToInt64(arr2, startIndex2);
					retVal = int64o1.CompareTo(int64o2);
					startIndex1 += SIZE_OF_INT64;
					startIndex2 += SIZE_OF_INT64;
					break;

				case "Int32PrefixedUtf8String":
					int32o1 = BitConverter.ToInt32(arr1, startIndex1);
					int32o2 = BitConverter.ToInt32(arr2, startIndex2);
					startIndex1 += SIZE_OF_INT32;
					startIndex2 += SIZE_OF_INT32;
					stro1 = stringEncoder.GetString(arr1, startIndex1, int32o1);
					stro2 = stringEncoder.GetString(arr2, startIndex2, int32o2);
					retVal = string.Compare(stro1, stro2);
					startIndex1 += int32o1;
					startIndex2 += int32o2;
					break;

				case "Int16PrefixedUtf8String":
					int16Len1 = BitConverter.ToInt16(arr1, startIndex1);
					int16Len2 = BitConverter.ToInt16(arr2, startIndex2);
					startIndex1 += SIZE_OF_INT16;
					startIndex2 += SIZE_OF_INT16;
					stro1 = stringEncoder.GetString(arr1, startIndex1, int16Len1);
					stro2 = stringEncoder.GetString(arr2, startIndex2, int16Len2);
					retVal = string.Compare(stro1, stro2);
					startIndex1 += int16Len1;
					startIndex2 += int16Len2;
					break;
			}
			return retVal;
		}
	}
}
