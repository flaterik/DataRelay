using System;
using System.IO;

namespace MySpace.Common.IO
{
    public enum CompressionImplementation
    {
        ManagedZLib
    }

    public class Compressor
	{
        public const CompressionImplementation DefaultCompressionImplementation = CompressionImplementation.ManagedZLib;

        #region Singleton implementation

		private static Compressor instance;
		private static int numOfReferences;

		public static Compressor GetInstance()
		{
			if (instance == null)
			{
				instance = new Compressor();
			}
			numOfReferences++;
			return instance;
		}

		public static int References
		{
			get
			{
				return numOfReferences;
			}
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
        

        private readonly int zLibCompressionAmount = 6;
        private byte[] InternalCompress(byte[] bytes, bool useHeader, CompressionImplementation compressionImplementation)
        {
            switch (compressionImplementation)
            {
                case CompressionImplementation.ManagedZLib:
                    return ManagedZLib.Compress(bytes, zLibCompressionAmount, useHeader);                    
                default:
                    throw new ApplicationException(string.Format("Unknown compression implementation {0}", compressionImplementation));
            }
        }

        
        private byte[] InternalDecompress(byte[] bytes, bool useHeader, CompressionImplementation compressionImplementation)
        {
            switch (compressionImplementation)
            {
                case CompressionImplementation.ManagedZLib:
                    return ManagedZLib.Decompress(bytes, useHeader);                    
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
