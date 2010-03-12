using System;

namespace MySpace.Common
{
	/// <summary>
	/// 	<para>Encapsulates methods similar to those in the <see cref="BitConverter"/> class
	/// 	but methods in this class allow you to write primitives to <see cref="Byte"/>[] instances
	/// 	that have been already allocated. This allows users to avoid unnecessary memory allocations.</para>
	/// </summary>
	public static class BitConverterEx
	{
		/// <summary>
		/// Writes the specified <see cref="Boolean"/> value to <paramref name="destination"/>.
		/// </summary>
		/// <param name="destination">The destination <see cref="Byte"/>[].</param>
		/// <param name="offset">The offset to begin writing at.</param>
		/// <param name="value">A <see cref="Boolean"/> value.</param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="destination"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///	<para><paramref name="offset"/> is less than zero or there is not enough
		///	room to write <paramref name="value"/>.</para>
		/// </exception>
		public static void WriteBytes(byte[] destination, int offset, bool value)
		{
			if (destination == null) throw new ArgumentNullException("destination");
			if (offset < 0 || offset >= destination.Length)
			{
				throw new ArgumentOutOfRangeException("offset", "offset must be greater than or equal to zero and less than destination.Length");
			}
			destination[offset] = value ? ((byte)1) : ((byte)0);
		}

		/// <summary>
		/// Writes the specified <see cref="Int16"/> value to <paramref name="destination"/>.
		/// </summary>
		/// <param name="destination">The destination <see cref="Byte"/>[].</param>
		/// <param name="offset">The offset to begin writing at.</param>
		/// <param name="value">A <see cref="Int16"/> value.</param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="destination"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///	<para><paramref name="offset"/> is less than zero or there is not enough
		///	room to write <paramref name="value"/>.</para>
		/// </exception>
		public static unsafe void WriteBytes(byte[] destination, int offset, short value)
		{
			if (destination == null) throw new ArgumentNullException("destination");
			if (offset < 0 || offset > destination.Length - sizeof(short))
			{
				throw new ArgumentOutOfRangeException("offset", "offset must be greater than or equal to zero and less than destination.Length - 2");
			}

			const int allignmentMask = sizeof(short) - 1;

			fixed (byte* ptr = &(destination[offset]))
			{
				if ((allignmentMask & offset) == 0)
				{
					*((short*)(ptr)) = value;
				}
				else
				{
					if (BitConverter.IsLittleEndian)
					{
						ptr[0] = (byte)value;
						ptr[1] = (byte)(value >> 0x8);
					}
					else
					{
						ptr[0] = (byte)(value >> 0x8);
						ptr[1] = (byte)value;
					}
				}
			}
		}

		/// <summary>
		/// Writes the specified <see cref="UInt16"/> value to <paramref name="destination"/>.
		/// </summary>
		/// <param name="destination">The destination <see cref="Byte"/>[].</param>
		/// <param name="offset">The offset to begin writing at.</param>
		/// <param name="value">A <see cref="UInt16"/> value.</param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="destination"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///	<para><paramref name="offset"/> is less than zero or there is not enough
		///	room to write <paramref name="value"/>.</para>
		/// </exception>
		public static void WriteBytes(byte[] destination, int offset, ushort value)
		{
			unchecked
			{
				WriteBytes(destination, offset, (short)value);
			}
		}

		/// <summary>
		/// Writes the specified <see cref="Int32"/> value to <paramref name="destination"/>.
		/// </summary>
		/// <param name="destination">The destination <see cref="Byte"/>[].</param>
		/// <param name="offset">The offset to begin writing at.</param>
		/// <param name="value">A <see cref="Int32"/> value.</param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="destination"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///	<para><paramref name="offset"/> is less than zero or there is not enough
		///	room to write <paramref name="value"/>.</para>
		/// </exception>
		public static unsafe void WriteBytes(byte[] destination, int offset, int value)
		{
			if (destination == null) throw new ArgumentNullException("destination");
			if (offset < 0 || offset > destination.Length - sizeof(int))
			{
				throw new ArgumentOutOfRangeException("offset", "offset must be greater than or equal to zero and less than destination.Length - 4");
			}

			const int allignmentMask = sizeof(int) - 1;

			fixed (byte* ptr = &(destination[offset]))
			{
				if ((allignmentMask & offset) == 0)
				{
					*((int*)ptr) = value;
				}
				else
				{
					if (BitConverter.IsLittleEndian)
					{
						ptr[0] = (byte)value;
						ptr[1] = (byte)(value >> 0x8);
						ptr[2] = (byte)(value >> 0x10);
						ptr[3] = (byte)(value >> 0x18);
					}
					else
					{
						ptr[0] = (byte)(value >> 0x18);
						ptr[1] = (byte)(value >> 0x10);
						ptr[2] = (byte)(value >> 0x8);
						ptr[3] = (byte)value;
					}
				}
			}
		}

		/// <summary>
		/// Writes the specified <see cref="UInt32"/> value to <paramref name="destination"/>.
		/// </summary>
		/// <param name="destination">The destination <see cref="Byte"/>[].</param>
		/// <param name="offset">The offset to begin writing at.</param>
		/// <param name="value">A <see cref="UInt32"/> value.</param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="destination"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///	<para><paramref name="offset"/> is less than zero or there is not enough
		///	room to write <paramref name="value"/>.</para>
		/// </exception>
		public static void WriteBytes(byte[] destination, int offset, uint value)
		{
			unchecked
			{
				WriteBytes(destination, offset, (int)value);
			}
		}

		/// <summary>
		/// Writes the specified <see cref="Single"/> value to <paramref name="destination"/>.
		/// </summary>
		/// <param name="destination">The destination <see cref="Byte"/>[].</param>
		/// <param name="offset">The offset to begin writing at.</param>
		/// <param name="value">A <see cref="Single"/> value.</param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="destination"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///	<para><paramref name="offset"/> is less than zero or there is not enough
		///	room to write <paramref name="value"/>.</para>
		/// </exception>
		public static unsafe void WriteBytes(byte[] destination, int offset, float value)
		{
			WriteBytes(destination, offset, *((int*)&value));
		}

		/// <summary>
		/// Writes the specified <see cref="Int64"/> value to <paramref name="destination"/>.
		/// </summary>
		/// <param name="destination">The destination <see cref="Byte"/>[].</param>
		/// <param name="offset">The offset to begin writing at.</param>
		/// <param name="value">A <see cref="Int64"/> value.</param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="destination"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///	<para><paramref name="offset"/> is less than zero or there is not enough
		///	room to write <paramref name="value"/>.</para>
		/// </exception>
		public static unsafe void WriteBytes(byte[] destination, int offset, long value)
		{
			if (destination == null) throw new ArgumentNullException("destination");
			if (offset < 0 || offset > destination.Length - sizeof(long))
			{
				throw new ArgumentOutOfRangeException("offset", "offset must be greater than or equal to zero and less than destination.Length - 8");
			}

			const int allignmentMask = sizeof(long) - 1;

			fixed (byte* ptr = &(destination[offset]))
			{
				if ((allignmentMask & offset) == 0)
				{
					*((long*)ptr) = value;
				}
				else
				{
					if (BitConverter.IsLittleEndian)
					{
						ptr[0] = (byte)value;
						ptr[1] = (byte)(value >> 8);
						ptr[2] = (byte)(value >> 0x10);
						ptr[3] = (byte)(value >> 0x18);
						ptr[4] = (byte)(value >> 0x20);
						ptr[5] = (byte)(value >> 0x28);
						ptr[6] = (byte)(value >> 0x30);
						ptr[7] = (byte)(value >> 0x38);
					}
					else
					{
						ptr[0] = (byte)(value >> 0x38);
						ptr[1] = (byte)(value >> 0x30);
						ptr[2] = (byte)(value >> 0x28);
						ptr[3] = (byte)(value >> 0x20);
						ptr[4] = (byte)(value >> 0x18);
						ptr[5] = (byte)(value >> 0x10);
						ptr[6] = (byte)(value >> 8);
						ptr[7] = (byte)value;
					}
				}
			}
		}

		/// <summary>
		/// Writes the specified <see cref="UInt64"/> value to <paramref name="destination"/>.
		/// </summary>
		/// <param name="destination">The destination <see cref="Byte"/>[].</param>
		/// <param name="offset">The offset to begin writing at.</param>
		/// <param name="value">A <see cref="UInt64"/> value.</param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="destination"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///	<para><paramref name="offset"/> is less than zero or there is not enough
		///	room to write <paramref name="value"/>.</para>
		/// </exception>
		public static void WriteBytes(byte[] destination, int offset, ulong value)
		{
			unchecked
			{
				WriteBytes(destination, offset, (long)value);
			}
		}

		/// <summary>
		/// Writes the specified <see cref="Double"/> value to <paramref name="destination"/>.
		/// </summary>
		/// <param name="destination">The destination <see cref="Byte"/>[].</param>
		/// <param name="offset">The offset to begin writing at.</param>
		/// <param name="value">A <see cref="Double"/> value.</param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="destination"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///	<para><paramref name="offset"/> is less than zero or there is not enough
		///	room to write <paramref name="value"/>.</para>
		/// </exception>
		public static unsafe void WriteBytes(byte[] destination, int offset, double value)
		{
			WriteBytes(destination, offset, *((long*)&value));
		}
	}
}
