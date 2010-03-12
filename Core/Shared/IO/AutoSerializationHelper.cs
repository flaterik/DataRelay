using System;
using System.Collections;
using System.Drawing;

namespace MySpace.Common.IO
{
    public static class AutoSerializationHelper
    {
        /// <summary>
        /// Serializes a BitArray into the writer.
        /// </summary>
        /// <param name="bitArray">The bit array</param>
        /// <param name="writer">The primitive writer</param>
        public static void SerializeBitArray(BitArray bitArray, IPrimitiveWriter writer)
        {
            // Write the byte length
            if (bitArray == null)
            {
                writer.Write(byte.MaxValue); // byte.MaxValue represents null
                return;
            }

            int currentByteLength = (bitArray.Count + 7) / 8;
            if (currentByteLength >= byte.MaxValue)
            {
                throw new ArgumentException("BitArray is too big to be serialized.", "bitArray");
            }
            // Write the byte length
            writer.Write((byte)currentByteLength);
            // Write only if we need to
            if (currentByteLength > 0)
            {
                // Copy the bitarray into a byte array
                byte[] bitArrayBytes = new byte[currentByteLength];
                bitArray.CopyTo(bitArrayBytes, 0);
                // Serialize
                writer.Write(bitArrayBytes);
            }
        }

        /// <summary>
        /// Deserializes a BitArray from the reader.
        /// </summary>
        /// <param name="bitArray">The bit array</param>
        /// <param name="reader">The primitive writer</param>
        public static void DeserializeBitArray(out BitArray bitArray, IPrimitiveReader reader)
        {
            byte byteCount = reader.ReadByte();

            // Is the BitArray null?
            if (byteCount == byte.MaxValue)
            {
                bitArray = null;
                return;
            }

            // Does the BitArray contain any values?
            if (byteCount == 0)
            {
                bitArray = new BitArray(0, false);
                return;
            }

            // Instantiate the bit array
            bitArray = new BitArray(reader.ReadBytes(byteCount));
        }

		/// <summary>
		/// Serializes a color into the writer.
		/// </summary>
		/// <param name="color">The Color struct.</param>
		/// <param name="writer">The primitive writer.</param>
		public static void SerializeColor(Color color, IPrimitiveWriter writer)
		{
			writer.Write(color.ToArgb());
		}

		/// <summary>
		/// Deserializes a color from the reader.
		/// </summary>
		/// <param name="color">The Color struct.</param>
		/// <param name="reader">The primitive reader.</param>
		public static void DeserializeColor(out Color color, IPrimitiveReader reader)
		{
			color = Color.FromArgb(reader.ReadInt32());
		}
	}
}
