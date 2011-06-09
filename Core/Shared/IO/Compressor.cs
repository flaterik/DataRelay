using System;
using System.IO;
using MySpace.Logging;

namespace MySpace.Common.IO
{
	public enum CompressionImplementation
	{
		ManagedZLib
	}

	public class Compressor
	{
		public const CompressionImplementation DefaultCompressionImplementation = CompressionImplementation.ManagedZLib;
		private static readonly Logging.LogWrapper log = new LogWrapper();
		#region Singleton implementation

		public static readonly Compressor Instance = new Compressor();
		
		private Compressor()
		{
			
		}

		public static Compressor GetInstance()
		{
			return Instance;
		}

		#endregion

		public static bool CompareData(byte[] buf1, int len1, byte[] buf2, int len2)
		{
			// Use this method to compare data from two different buffers.
			if (len1 != len2)
			{
				Console.WriteLine("Number of bytes in two buffer are different {0}:{1}", len1, len2);
				return false;
			}

			for (int i = 0; i < len1; i++)
			{
				if (buf1[i] != buf2[i])
				{
					Console.WriteLine("byte {0} is different {1}|{2}", i, buf1[i], buf2[i]);
					return false;
				}
			}
			Console.WriteLine("All bytes compare.");
			return true;
		}

		/// <summary>
		/// Compress bytes using the default compression implementation and no header.
		/// </summary>        
		public byte[] Compress(byte[] bytes)
		{
			return InternalCompress(bytes, false, DefaultCompressionImplementation);
		}

		/// <summary>
		/// Compress bytes using the default compression implementation and optional header.
		/// </summary>    
		public byte[] Compress(byte[] bytes, bool useHeader)
		{
			return InternalCompress(bytes, useHeader, DefaultCompressionImplementation);
		}

		/// <summary>
		/// Compress bytes using the supplied compression implementation and no header.
		/// </summary> 
		public byte[] Compress(byte[] bytes, CompressionImplementation compressionImplementation)
		{
			return InternalCompress(bytes, false, compressionImplementation);
		}

		/// <summary>
		/// Compress bytes using the supplied compression implementation and optional header.
		/// </summary> 
		public byte[] Compress(byte[] bytes, bool useHeader, CompressionImplementation compressionImplementation)
		{
			return InternalCompress(bytes, useHeader, compressionImplementation);
		}


		/// <summary>
		/// Decompress bytes using the default compression implementation and no header.
		/// </summary>        
		public byte[] Decompress(byte[] bytes)
		{
			return InternalDecompress(bytes, false, DefaultCompressionImplementation);
		}


		/// <summary>
		/// Decompress bytes using the default compression implementation and optional header.
		/// </summary>  
		public byte[] Decompress(byte[] bytes, bool useHeader)
		{
			return InternalDecompress(bytes, useHeader, DefaultCompressionImplementation);
		}

		/// <summary>
		/// Decompress bytes using the supplied compression implementation and no header.
		/// </summary>  
		public byte[] Decompress(byte[] bytes, CompressionImplementation compressionImplementation)
		{
			return InternalDecompress(bytes, false, compressionImplementation);
		}

		/// <summary>
		/// Decompress bytes using the supplied compression implementation and optional header.
		/// </summary>  
		public byte[] Decompress(byte[] bytes, bool useHeader, CompressionImplementation compressionImplementation)
		{
			return InternalDecompress(bytes, useHeader, compressionImplementation);
		}
		

		private const int zLibCompressionAmount = 6;
		private static byte[] InternalCompress(byte[] bytes, bool useHeader, CompressionImplementation compressionImplementation)
		{
			switch (compressionImplementation)
			{
				case CompressionImplementation.ManagedZLib:
					return ManagedZLibWrapper.Compress(bytes, zLibCompressionAmount, useHeader);                    
				default:
					throw new ApplicationException(string.Format("Unknown compression implementation {0}", compressionImplementation));
			}
		}

		
		private static byte[] InternalDecompress(byte[] bytes, bool useHeader, CompressionImplementation compressionImplementation)
		{
			switch (compressionImplementation)
			{
				case CompressionImplementation.ManagedZLib:
					byte[] decompressed = null;
					try
					{
						decompressed = ManagedZLibWrapper.Decompress(bytes, useHeader);
					}
					catch (Exception e)
					{
						if (useHeader)
						{
							if (e.Message.Contains("Invalid GZip header"))
							{
								//we're probably trying to decompress data that has no header with the header flag. if it throws again, just let it go
								log.WarnFormat("Tried to decompress using header and got error {0}. Attempting decompress with no header.",
								               e.Message);
								try
								{
									decompressed = ManagedZLibWrapper.Decompress(bytes, false);
								}
								catch (Exception e2)
								{
									if (e2.Message.Contains("Z_DATA_ERROR")) //then the data is actually invalid; it may not be compressed at all. we'll try just returning the data as it was
									{
										log.WarnFormat("Tried to decompress with no header after getting error with header and got error {0}. Returning bytes as they were.", e2.Message);
										decompressed = bytes;
									}
									else
									{
										throw;
									}
								}
							}
							else
							{
								throw;
							}
						}
						else
						{
							if (e.Message.Contains("Z_DATA_ERROR"))
							{
								//probably the inverse; tried to decompress data with a header without one.
								log.WarnFormat("Tried to decompress with no header and got error {0}. Attempting decompress with header.",
								               e.Message);

								try
								{
									decompressed = ManagedZLibWrapper.Decompress(bytes, true);
								}
								catch (Exception e2)
								{
									if (e2.Message.Contains("Invalid GZip header"))
									{
										log.WarnFormat("Tried to decompress with header after getting error with header and got error {0}. Returning byte as they were.", e2.Message);
										decompressed = bytes;
									}
									else
									{
										throw;
									}
								}
							}
							else
							{
								throw;
							}
						}
					}
					return decompressed;
				default:
					throw new ApplicationException(string.Format("Unknown compression implementation {0}", compressionImplementation));
			}            
		}


		private static byte[] GetBytes(Stream stream, int initialLength, bool reset)
		{
			if (reset)
			{
				if (stream.Position > 0)
					stream.Position = 0;
			}

			// If we've been passed an unhelpful initial length, just
			// use 3K.
			if (initialLength < 1)
			{
				initialLength = 3768;
			}

			byte[] buffer = new byte[initialLength];
			int read = 0;

			int chunk;
			while ((chunk = stream.Read(buffer, read, (buffer.Length - read))) > 0)
			{
				read += chunk;

				// If we've reached the end of our buffer, check to see if there's
				// any more information
				if (read == buffer.Length)
				{
					int nextByte = stream.ReadByte();

					// End of stream? If so, we're done
					if (nextByte == -1)
					{
						return buffer;
					}

					// Nope. Resize the buffer, put in the byte we've just
					// read, and continue
					byte[] newBuffer = new byte[buffer.Length * 2];
					Buffer.BlockCopy(buffer, 0, newBuffer, 0, Buffer.ByteLength(buffer));
					newBuffer[read] = (byte)nextByte;
					buffer = newBuffer;
					read++;
				}
			}
			// Buffer is now too big. Shrink it.
			byte[] ret = new byte[read];
			Buffer.BlockCopy(buffer, 0, ret, 0, Buffer.ByteLength(ret));
			return ret;
		}
	}
}
