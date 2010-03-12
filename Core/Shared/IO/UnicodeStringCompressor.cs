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
			byte[] messagebytes = Encoding.Unicode.GetBytes(unicodeString);

			if (ConfigurationManager.AppSettings["UseManagedZLibForCompress"] == "true")
			{
				return ManagedZLib.Compress(messagebytes, 6, true);
			}
			else
			{
				return MySpace.Common.IO.Compressor.GetInstance().Compress(messagebytes, true);
			}
		}

		public static string Decompress(byte[] compressedUnicodeString)
		{
			byte[] messagebytes = new byte[0];
			if (ConfigurationManager.AppSettings["UseManagedZLibForDecompress"] == "true")
			{
				messagebytes = ManagedZLib.Decompress(compressedUnicodeString, true);
			}
			else
			{
				messagebytes = MySpace.Common.IO.Compressor.GetInstance().Decompress(compressedUnicodeString, true);
			}

			return Encoding.Unicode.GetString(messagebytes);
		}
	}
}
