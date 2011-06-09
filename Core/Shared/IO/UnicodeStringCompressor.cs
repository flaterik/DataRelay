using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;


namespace MySpace.Common.IO
{
	public class UnicodeStringCompressor
	{
		public static byte[] Compress(string unicodeString)
		{
			if (unicodeString == null)
				return null;

			byte[] messagebytes = Encoding.Unicode.GetBytes(unicodeString);
			return Compressor.Instance.Compress(messagebytes, true);
			
		}

		public static string Decompress(byte[] compressedUnicodeString)
		{
			if (compressedUnicodeString == null)
				return null;

			byte[] messagebytes = Compressor.Instance.Decompress(compressedUnicodeString, true);
			return Encoding.Unicode.GetString(messagebytes);
		}
	}
}
