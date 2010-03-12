using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV2
{
	/// <summary>
	/// Provides set of utility methods for extracting objects from relay message
	/// </summary>
	public static class CacheIndexExtractionUtility
	{
		private const int SIZE_OF_UINT16 = sizeof(UInt16);
		private const int SIZE_OF_INT32 = sizeof(Int32);
		private const int SIZE_OF_BYTE = sizeof(Byte);
		private static Encoding stringEncoder = new UTF8Encoding(false, true);

		#region Extraction methods
		public static int ExtractInt(byte[] payloadByteArray, ref int payloadPosition)
		{
			int retVal = 0;
			retVal = BitConverter.ToInt32(payloadByteArray, payloadPosition);
			payloadPosition += SIZE_OF_INT32;
			return retVal;
		}

		public static bool ExtractBool(byte[] payloadByteArray, ref int payloadPosition)
		{
			bool retVal = BitConverter.ToBoolean(payloadByteArray, payloadPosition);
			payloadPosition++;
			return retVal;
		}

		public static byte[] ExtractByteArrayValueField(byte[] payloadByteArray, ref int payloadPosition, bool isLengthUshort)
		{
			int fieldLength = 0;
			byte[] retVal = null;

			if (isLengthUshort) //ushort field used for length
			{
				fieldLength = BitConverter.ToUInt16(payloadByteArray, payloadPosition);
				payloadPosition += SIZE_OF_UINT16;
			}
			else //int field used for length
			{
				fieldLength = BitConverter.ToInt32(payloadByteArray, payloadPosition);
				payloadPosition += SIZE_OF_INT32;
			}

			if (fieldLength > 0)
			{
				retVal = new byte[fieldLength];
				Array.Copy(payloadByteArray, payloadPosition, retVal, 0, fieldLength);
			}
			payloadPosition += fieldLength;
			return retVal;
		}

		public static void SkipByteArray(byte[] payloadByteArray, ref int payloadPosition, bool isLengthUshort)
		{
			int fieldLength = 0;

			if (isLengthUshort) //ushort field used for length
			{
				fieldLength = BitConverter.ToUInt16(payloadByteArray, payloadPosition);
				payloadPosition += (SIZE_OF_UINT16 + fieldLength);
			}
			else //int field used for length
			{
				fieldLength = BitConverter.ToInt32(payloadByteArray, payloadPosition);
				payloadPosition += (SIZE_OF_INT32 + fieldLength);
			}
		}

		public static byte[] ExtractDictionaryItem(byte[] payloadByteArray, string indexName, ref int payloadPosition)
		{
			string key;
			ushort valueLength;
			byte[] retVal = null;

			ushort dictionaryCount = BitConverter.ToUInt16(payloadByteArray, payloadPosition);
			payloadPosition += SIZE_OF_UINT16;

			for (ushort i = 0; i < dictionaryCount; i++)
			{
				//Skip key
				key = Read7BitEncodedIntPrefixedString(payloadByteArray, ref payloadPosition);

				valueLength = BitConverter.ToUInt16(payloadByteArray, payloadPosition);
				payloadPosition += SIZE_OF_UINT16;

				if(string.Equals(key, indexName))
				{
					retVal = new byte[valueLength];
					Array.Copy(payloadByteArray, payloadPosition, retVal, 0, retVal.Length);
				}
				//Skip value
				payloadPosition += valueLength;
			}
			return retVal;
		}

		public static List<byte[]> ExtractDictionaryItems(byte[] payloadByteArray, ref int payloadPosition, bool appendLength)
		{
			ushort valueLength;
			int sortFieldStart;
			List<byte[]> retVal = new List<byte[]>();
			byte[] valueField = null;

			ushort dictionaryCount = BitConverter.ToUInt16(payloadByteArray, payloadPosition);
			payloadPosition += SIZE_OF_UINT16;

			for (ushort i = 0; i < dictionaryCount; i++)
			{
				//Skip key
				Read7BitEncodedIntPrefixedString(payloadByteArray, ref payloadPosition);

				valueLength = BitConverter.ToUInt16(payloadByteArray, payloadPosition);
				if (appendLength)
				{
					sortFieldStart = payloadPosition;
					valueField = new byte[valueLength + SIZE_OF_UINT16];
				}
				else
				{
					sortFieldStart = payloadPosition + SIZE_OF_UINT16;
					valueField = new byte[valueLength];
				}
				payloadPosition += SIZE_OF_UINT16;

				Array.Copy(payloadByteArray, sortFieldStart, valueField, 0, valueField.Length);
				payloadPosition += valueLength;

				retVal.Add(valueField);
			}
			return retVal;
		}

		public static void SkipDictionary(byte[] payloadByteArray, ref int payloadPosition)
		{
			ushort dictionaryCount = BitConverter.ToUInt16(payloadByteArray, payloadPosition);
			payloadPosition += SIZE_OF_UINT16;

			ushort valueLength;

			for (ushort i = 0; i < dictionaryCount; i++)
			{
				//Skip key
				Read7BitEncodedIntPrefixedString(payloadByteArray, ref payloadPosition);

				//Skip value
				valueLength = BitConverter.ToUInt16(payloadByteArray, payloadPosition);
				payloadPosition += SIZE_OF_UINT16;

				payloadPosition += valueLength;
			}
		}
		#endregion

		#region Utility Methods
		public unsafe static bool CompareByteArrays(byte[] x, byte[] y)
		{
			// Null objects
			if (x == null || y == null)
				return false;
			// Correct lenghts
			if (x.Length != y.Length)
				return false;

			// I want to compare 4 bytes at a time if possible
			int iterations = x.Length / 4;
			int remainder = x.Length % 4;
			fixed (byte* bPtrX = x)
			{
				fixed (byte* bPtrY = y)
				{
					int* iPtrX = (int*)bPtrX;
					int* iPtrY = (int*)bPtrY;

					// Evens
					for (int i = 0; i < iterations; i++)
					{
						if (*iPtrX != *iPtrY)
							return false;
						iPtrX++;
						iPtrY++;
					}
					// Odds
					if (remainder > 0)
					{
						byte* remainderX = (byte*)iPtrX;
						byte* remainderY = (byte*)iPtrY;
						for (int j = 0; j < remainder; j++)
						{
							if (*remainderX != *remainderY)
								return false;
							remainderX++;
							remainderY++;
						}
					}
				}
			}

			return true;
		}

		private static string Read7BitEncodedIntPrefixedString(byte[] payloadByteArray, ref int payloadPosition)
		{
			//Don't really know how and why this payloadPosition + 1 works ??
			string str = stringEncoder.GetString(
				payloadByteArray,
				payloadPosition + 1, 
				Read7BitEncodedInt(payloadByteArray, ref payloadPosition));

			payloadPosition += str.Length;
			return str;
		}

		private static int Read7BitEncodedInt(byte[] payloadByteArray, ref int payloadPosition)
		{
			byte num3;
			int num = 0;
			int num2 = 0;
			do
			{
				if (num2 == 0x23)
				{
					throw new Exception("Error reading 7BitEncodedInt");
				}
				//num3 = this.ReadByte();
				num3 = payloadByteArray[payloadPosition++];
				num |= (num3 & 0x7f) << num2;
				num2 += 7;
			}
			while ((num3 & 0x80) != 0);
			return num;
		}

		///// <summary>
		///// Custom BinarySearch method written on top of List.BinarySearch() to remove the limitation that
		///// the first occurance of the searchItem is returned. This limitation may cause problem when there are
		///// duplicate items in the list. This method will return TRUE first occurance of the searchItem depending on
		///// the sort order for the list
		///// </summary>
		///// <param name="itemList"></param>
		///// <param name="searchItem"></param>
		///// <param name="indexCacheComparer"></param>
		///// <returns></returns>
		//public static int BinarySearch(List<byte[]> itemList, byte[] searchItem, IndexCacheComparer indexCacheComparer)
		//{
		//   int searchIndex = -1;

		//   searchIndex = itemList.BinarySearch(searchItem, indexCacheComparer);
		//   if (searchIndex > -1)
		//   {

		//   }

		//   return searchIndex;
		//}
		#endregion
	}
}