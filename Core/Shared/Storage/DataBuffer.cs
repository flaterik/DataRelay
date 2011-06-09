using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Reflection;
using MySpace.Common.IO;

namespace MySpace.Common.Storage
{
	/// <summary>
	/// General purpose encapsulator for a variety of data sources and sinks.
	/// </summary>
	public partial struct DataBuffer : IVersionSerializable, IEquatable<DataBuffer>
	{
		[DllImport("kernel32.dll", EntryPoint = "CopyMemory")]
		static extern void CopyMemory(IntPtr Destination, IntPtr Source, int Length);

		#region Auxilliary Classes
		private struct ObjectSegment
		{
			public int Offset;
			public int Length;
		}

		[StructLayout(LayoutKind.Explicit)]
		private struct NonobjectDataUnion
		{
			[FieldOffset(0)] public int Int32Value;
			[FieldOffset(0)] public long Int64Value;
			[FieldOffset(0)] public ObjectSegment SegmentValue;
		}
		#endregion

		#region Fields
		private object _object;
		private NonobjectDataUnion _dataUnion;
		private DataBufferType _type;
		private static readonly DataBuffer _empty = new DataBuffer(
			DataBufferType.Empty, new NonobjectDataUnion());
		#endregion

		#region Helper Methods
		private void AssertValidOnlyFor(DataBufferType type)
		{
			if (_type != type) throw InvalidOperationForType();
		}

		private void AssertValidOnlyForSegmentable()
		{
			if (_type == DataBufferType.Empty) return;
			if ((_type & DataBufferType.Segmentable) != DataBufferType.Segmentable)
			{
				throw InvalidOperationForType();
			}
		}

		private StringBuilder EnsureStringBuilderCapacity()
		{
			var sbd = (StringBuilder)_object;
			sbd.EnsureCapacity(_dataUnion.SegmentValue.Offset +
				_dataUnion.SegmentValue.Length);
			return sbd;
		}

		private unsafe int GetHashCodeForObjects()
		{
			int off, len;
			var o = GetObjectValue(out off, out len);
			var handle = GCHandle.Alloc(o, GCHandleType.Pinned);
			try
			{
				var p = ((byte*)handle.AddrOfPinnedObject().ToPointer() +
					off);
				var ret = 0x2ddefcf7;
				var np = (int*)p;
				for (; len >= sizeof(int); len -= sizeof(int), ++np)
				{
					ret = ((ret << 5) | (ret >> 27)) ^ *np;
				}
				if (len > 0)
				{
					ret = ((ret << 5) | (ret >> 27));
					switch (len)
					{
						case 1:
							ret ^= (*np & 0xff);
							break;
						case 2:
							ret ^= (*np & 0xffff);
							break;
						case 3:
							ret ^= (*np & 0xffffff);
							break;
					}
				}
				return ret;
			}
			finally
			{
				if (handle.IsAllocated)
				{
					handle.Free();
				}
			}
		}

		private unsafe int GetHashCodeForChars()
		{
			// adapted from implementation of String.GetHashCode
			fixed (char* p = &((char[])_object)[_dataUnion.SegmentValue.Offset])
			{
				var num = 0x15051505;
				var num2 = num;
				var numPtr = (int*)p;
				for (var i = _dataUnion.SegmentValue.Length; i > 0; i -= 4)
				{
					num = (((num << 5) + num) + (num >> 0x1b)) ^ numPtr[0];
					if (i <= 2)
					{
						break;
					}
					num2 = (((num2 << 5) + num2) + (num2 >> 0x1b)) ^ numPtr[1];
					numPtr += 2;
				}
				return (num + (num2 * 0x5d588b65));
			}
		}

		private unsafe int GetObjectIdForObjects()
		{
			int off, len;
			var o = GetObjectValue(out off, out len);
			var handle = GCHandle.Alloc(o, GCHandleType.Pinned);
			try
			{
				var p = ((byte*) handle.AddrOfPinnedObject().ToPointer() +
					off);
				var ip = (int*)p;
				// first 4 bytes (or fraction thereof)
				var ret = *ip;
				if (len <= 4)
				{
					switch(len)
					{
						case 1:
							ret &= 0xff;
							break;
						case 2:
							ret &= 0xffff;
							break;
						case 3:
							ret &= 0xffffff;
							break;
					}
					return ret;
				}
				// last 4 bytes
				ip = (int*) (p + len - 4);
				ret ^= *ip;
				if (len <= 8)
				{
					return ret;
				}
				// middle 4 bytes
				ip = (int*)(p + (len / 2) - 4);
				ret ^= *ip;
				return ret;
			}
			finally
			{
				if (handle.IsAllocated)
				{
					handle.Free();
				}
			}
		}

		private string MakeStringForStringBuilder()
		{
			var sbd = (StringBuilder)_object;
			if (_dataUnion.SegmentValue.Offset == 0 &&
				_dataUnion.SegmentValue.Length == sbd.Length)
			{
				return sbd.ToString();
			}
			return sbd.ToString(_dataUnion.SegmentValue.Offset,
				_dataUnion.SegmentValue.Length);
		}

		private static Exception InvalidOperationForType(DataBufferType type)
		{
			return new InvalidOperationException(string.Format(
				"This operation is not valid for type {0}", type));			
		}

		private Exception InvalidOperationForType()
		{
			return InvalidOperationForType(_type);
		}

		private static bool GetFieldAccessors<T>(string name, bool writeAlso,
			out Func<StringBuilder, T> getter, out Action<StringBuilder, T> setter)
		{
			var field = typeof(StringBuilder).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
			if (field == null)
			{
				getter = null;
				setter = null;
				return false;
			}
			var method = new DynamicMethodHelper(name + "_StringBuilder_getter",
				typeof(T), new[] { typeof(StringBuilder) },
				typeof(StringBuilder));
			method.GetField(0, field);
			method.Return();
			getter = method.Compile<Func<StringBuilder, T>>();
			if (writeAlso)
			{
				method = new DynamicMethodHelper(name + "_StringBuilder_setter",
					null, new[] { typeof(StringBuilder), typeof(T) },
					typeof(StringBuilder));
				method.PushArg(0);
				method.PushArg(1);
				method.SetField(field);
				method.Return();
				setter = method.Compile<Action<StringBuilder, T>>();				
			}
			else
			{
				setter = null;
			}
			return true;
		}
		#endregion

		#region Properties
		/// <summary>
		/// Gets an instance that can't contain any data.
		/// </summary>
		/// <value>
		/// An empty <see cref="DataBuffer"/>.
		/// </value>
		public static DataBuffer Empty { get { return _empty; } }
		
		/// <summary>
		/// Gets the type of data source or sink encapsulated by this instance.
		/// </summary>
		/// <value>
		/// The characteristic <see cref="DataBufferType"/>.
		/// </value>
		public DataBufferType Type { get { return _type; } }
		
		/// <summary>
		/// Gets whether this instance can't contain any data.
		/// </summary>
		/// <value>
		/// <see langword="true"/> if it can't contain any data, otherwise
		/// <see langword="false"/>.
		/// </value>
		public bool IsEmpty { get { return Equals(_empty); } }

		/// <summary>
		/// Gets whether this instance encapsulates a data sink.
		/// </summary>
		/// <value>
		/// <see langword="true"/> if it can be written to, otherwise
		/// <see langword="false"/>.
		/// </value>
		public bool IsWritable
		{
			get
			{
				return (_type & DataBufferType.Segmentable) ==
					DataBufferType.Segmentable;
			}
		}

		/// <summary>
		/// Gets whether this instance encapsulates a reference type
		/// instance.
		/// </summary>
		/// <value>
		/// <see langword="true"/> if this instance encapsulates a reference
		/// type instance; otherwise <see langword="false"/>.
		/// </value>
		public bool IsObject
		{
			get
			{
				return (_type & DataBufferType.Object) == DataBufferType.Object;
			}
		}

		/// <summary>
		/// Gets the byte length of the data encapsulated by this instance.
		/// </summary>
		/// <value>
		/// The number of bytes of the data encapsulated by this instance.
		/// </value>
		public int ByteLength
		{
			get
			{
				switch(_type)
				{
					case DataBufferType.Empty:
						return 0;
					case DataBufferType.Int32:
						return 4;
					case DataBufferType.Int64:
						return 8;
					case DataBufferType.String:
					case DataBufferType.StringBuilderSegment:
					case DataBufferType.CharArraySegment:
						return _dataUnion.SegmentValue.Length << _charShift;
					case DataBufferType.ByteArraySegment:
						return _dataUnion.SegmentValue.Length;
					case DataBufferType.Int32ArraySegment:
						return _dataUnion.SegmentValue.Length << _intShift;
					case DataBufferType.Int64ArraySegment:
						return _dataUnion.SegmentValue.Length << _longShift;
					default:
						throw InvalidOperationForType();
				}
			}
		}

		/// <summary>
		/// Gets the offset of the array data referenced in this instance.
		/// </summary>
		/// <value>
		/// The position within the data object at which the encapsulated reference
		/// begins.
		/// </value>
		/// <exception cref="InvalidOperationException">
		/// <para><see cref="Type"/> is a type that doesn't encapsulate any
		/// objects, i.e. <see cref="String"/>, or
		/// <see cref="DataBufferType.Int32"/>, or
		/// <see cref="DataBufferType.Int64"/>.</para>
		/// </exception>
		public int Offset
		{
			get
			{
				AssertValidOnlyForSegmentable();
				return _dataUnion.SegmentValue.Offset;
			}
		}

		/// <summary>
		/// Gets the length of the data referenced in this instance.
		/// </summary>
		/// <value>
		/// The length of the encapsulated reference of the object data.
		/// </value>
		/// <exception cref="InvalidOperationException">
		/// <para><see cref="Type"/> is a type that doesn't encapsulate any
		/// objects, i.e. <see cref="String"/>, or
		/// <see cref="DataBufferType.Int32"/>, or
		/// <see cref="DataBufferType.Int64"/>.</para>
		/// </exception>
		public int Length
		{
			get
			{
				AssertValidOnlyForSegmentable();
				return _dataUnion.SegmentValue.Length;				
			}
		}

		/// <summary>
		/// Gets the integer value of this instance.
		/// </summary>
		/// <value>
		/// The <see cref="Int32"/> encapsulated by this instance.
		/// </value>
		/// <exception cref="InvalidOperationException">
		/// <para><see cref="Type"/> isn't <see cref="DataBufferType.Int32"/></para>
		/// </exception>
		public int Int32Value {
			get
			{
				AssertValidOnlyFor(DataBufferType.Int32);
				return _dataUnion.Int32Value;
			}
		}

		/// <summary>
		/// Gets the long value of this instance.
		/// </summary>
		/// <value>
		/// The <see cref="Int64"/> encapsulated by this instance.
		/// </value>
		/// <exception cref="InvalidOperationException">
		/// <para><see cref="Type"/> isn't <see cref="DataBufferType.Int64"/></para>
		/// </exception>
		public long Int64Value
		{
			get
			{
				AssertValidOnlyFor(DataBufferType.Int64);
				return _dataUnion.Int64Value;
			}
		}

		/// <summary>
		/// Gets the string value of this instance.
		/// </summary>
		/// <value>
		/// The <see cref="String"/> encapsulated by this instance.
		/// </value>
		/// <exception cref="InvalidOperationException">
		/// <para><see cref="Type"/> isn't <see cref="DataBufferType.String"/></para>
		/// </exception>
		public string StringValue
		{
			get
			{
				AssertValidOnlyFor(DataBufferType.String);
				return (string) _object;
			}
		}

		/// <summary>
		/// Gets the char array segment value of this instance.
		/// </summary>
		/// <value>
		/// The <see cref="ArraySegment{Char}"/> encapsulated by this instance.
		/// </value>
		/// <exception cref="InvalidOperationException">
		/// <para><see cref="Type"/> isn't <see cref="DataBufferType.CharArraySegment"/></para>
		/// </exception>
		public ArraySegment<char> CharArraySegmentValue
		{
			get
			{
				AssertValidOnlyFor(DataBufferType.CharArraySegment);
				return new ArraySegment<char>((char[]) _object,
					_dataUnion.SegmentValue.Offset,
					_dataUnion.SegmentValue.Length);
			}			
		}

		/// <summary>
		/// Gets the byte array segment value of this instance.
		/// </summary>
		/// <value>
		/// The <see cref="ArraySegment{Byte}"/> encapsulated by this instance.
		/// </value>
		/// <exception cref="InvalidOperationException">
		/// <para><see cref="Type"/> isn't <see cref="DataBufferType.ByteArraySegment"/></para>
		/// </exception>
		public ArraySegment<byte> ByteArraySegmentValue
		{
			get
			{
				AssertValidOnlyFor(DataBufferType.ByteArraySegment);
				return new ArraySegment<byte>((byte[])_object,
					_dataUnion.SegmentValue.Offset,
					_dataUnion.SegmentValue.Length);
			}
		}

		/// <summary>
		/// Gets the int array segment value of this instance.
		/// </summary>
		/// <value>
		/// The <see cref="ArraySegment{Int32}"/> encapsulated by this instance.
		/// </value>
		/// <exception cref="InvalidOperationException">
		/// <para><see cref="Type"/> isn't <see cref="DataBufferType.Int32ArraySegment"/></para>
		/// </exception>
		public ArraySegment<int> Int32ArraySegmentValue
		{
			get
			{
				AssertValidOnlyFor(DataBufferType.Int32ArraySegment);
				return new ArraySegment<int>((int[])_object,
					_dataUnion.SegmentValue.Offset,
					_dataUnion.SegmentValue.Length);
			}
		}

		/// <summary>
		/// Gets the long array segment value of this instance.
		/// </summary>
		/// <value>
		/// The <see cref="ArraySegment{Int64}"/> encapsulated by this instance.
		/// </value>
		/// <exception cref="InvalidOperationException">
		/// <para><see cref="Type"/> isn't <see cref="DataBufferType.Int64ArraySegment"/></para>
		/// </exception>
		public ArraySegment<long> Int64ArraySegmentValue
		{
			get
			{
				AssertValidOnlyFor(DataBufferType.Int64ArraySegment);
				return new ArraySegment<long>((long[])_object,
					_dataUnion.SegmentValue.Offset,
					_dataUnion.SegmentValue.Length);
			}
		}

		/// <summary>
		/// Gets the string builder segment value of this instance.
		/// </summary>
		/// <value>
		/// The <see cref="StringBuilderSegment"/> encapsulated by this instance.
		/// </value>
		/// <exception cref="InvalidOperationException">
		/// <para><see cref="Type"/> isn't <see cref="DataBufferType.StringBuilderSegment"/></para>
		/// </exception>
		public StringBuilderSegment StringBuilderSegmentValue
		{
			get
			{
				AssertValidOnlyFor(DataBufferType.StringBuilderSegment);
				return new StringBuilderSegment((StringBuilder) _object,
					_dataUnion.SegmentValue.Offset,
					_dataUnion.SegmentValue.Length);
			}
		}

		/// <summary>
		/// Gets an underlying object for direct reading or writing of data.
		/// This object should be pinned for unmanaged usage.
		/// </summary>
		/// <param name="offset">Field for writing the byte position where
		/// reading or writing should start.</param>
		/// <param name="length">Field for writing the byte length of the data
		/// read or written.</param>
		/// <returns>The underlying <see cref="Object"/>.</returns>
		/// <exception cref="InvalidOperationException">
		/// <para><see cref="IsObject"/> is <see langword="false"/>.</para>
		/// </exception>
		public object GetObjectValue(out int offset, out int length)
		{
			switch(_type)
			{
				case DataBufferType.String:
					offset = 0;
					length = _dataUnion.SegmentValue.Length << _charShift;
					break;
				case DataBufferType.CharArraySegment:
					offset = _dataUnion.SegmentValue.Offset << _charShift;
					length = _dataUnion.SegmentValue.Length << _charShift;
					break;
				case DataBufferType.ByteArraySegment:
					offset = _dataUnion.SegmentValue.Offset;
					length = _dataUnion.SegmentValue.Length;
					break;
				case DataBufferType.Int32ArraySegment:
					offset = _dataUnion.SegmentValue.Offset << _intShift;
					length = _dataUnion.SegmentValue.Length << _intShift;
					break;
				case DataBufferType.Int64ArraySegment:
					offset = _dataUnion.SegmentValue.Offset << _longShift;
					length = _dataUnion.SegmentValue.Length << _longShift;
					break;
				case DataBufferType.StringBuilderSegment:
					var sbd = EnsureStringBuilderCapacity();
					offset = _dataUnion.SegmentValue.Offset << _charShift;
					length = _dataUnion.SegmentValue.Length << _charShift;
					return StringBuilderInternalAccessor.Instance.GetObject(sbd);
				default:
					throw InvalidOperationForType();
			}
			return _object;
		}
		#endregion

		#region Constructors
		private DataBuffer(DataBufferType type, object mObject, int offset, int length)
		{
			_type = type;
			_object = mObject;
			_dataUnion = new NonobjectDataUnion
			{
				SegmentValue = new ObjectSegment
				{
					Offset = offset,
					Length = length
				}
			};
		}

		private DataBuffer(DataBufferType type, NonobjectDataUnion dataUnion)
		{
			_type = type;
			_dataUnion = dataUnion;
			_object = null;
		}
		#endregion

		#region Creation/Conversion Ops
		/// <summary>
		/// Casts an integer to a data buffer.
		/// </summary>
		/// <param name="data">The <see cref="Int32"/> to be cast.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/> 
		/// <see cref="DataBufferType.Int32"/>.</returns>
		public static implicit operator DataBuffer(int data)
		{
			return new DataBuffer(DataBufferType.Int32,
				new NonobjectDataUnion {Int32Value = data});
		}

		/// <summary>
		/// Casts a long to a data buffer.
		/// </summary>
		/// <param name="data">The <see cref="Int64"/> to be cast.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/> 
		/// <see cref="DataBufferType.Int64"/>.</returns>
		public static implicit operator DataBuffer(long data)
		{
			return new DataBuffer(DataBufferType.Int64,
				new NonobjectDataUnion { Int64Value = data });
		}

		/// <summary>
		/// Casts a byte array to a data buffer.
		/// </summary>
		/// <param name="data">The <see cref="Byte"/> array to be cast.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/> 
		/// <see cref="DataBufferType.ByteArraySegment"/> if
		/// <paramref name="data"/> is not <see langword="null"/> and
		/// <see cref="Array.Length"/> is greater than 0; otherwise of
		/// <see cref="Type"/> <see cref="DataBufferType.Empty"/>.</returns>
		public static implicit operator DataBuffer(byte[] data)
		{
			if (data == null || data.Length == 0) return _empty;
			return new DataBuffer(DataBufferType.ByteArraySegment, data, 0, data.Length);
		}

		/// <summary>
		/// Casts a byte array segment to a data buffer.
		/// </summary>
		/// <param name="data">The <see cref="ArraySegment{Byte}"/> to be cast.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/> 
		/// <see cref="DataBufferType.ByteArraySegment"/> if
		/// <see cref="ArraySegment{Byte}.Count"/> of <paramref name="data"/>
		/// is greater than 0; otherwise of <see cref="Type"/>
		/// <see cref="DataBufferType.Empty"/>.</returns>
		public static implicit operator DataBuffer(ArraySegment<byte> data)
		{
			if (data.Count == 0) return _empty;
			return new DataBuffer(DataBufferType.ByteArraySegment, data.Array,
				data.Offset, data.Count);
		}

		/// <summary>
		/// Casts a char array to a data buffer.
		/// </summary>
		/// <param name="data">The <see cref="Char"/> array to be cast.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/> 
		/// <see cref="DataBufferType.CharArraySegment"/> if
		/// <paramref name="data"/> is not <see langword="null"/> and
		/// <see cref="Array.Length"/> is greater than 0; otherwise of
		/// <see cref="Type"/> <see cref="DataBufferType.Empty"/>.</returns>
		public static implicit operator DataBuffer(char[] data)
		{
			if (data == null || data.Length == 0 ) return _empty;
			return new DataBuffer(DataBufferType.CharArraySegment, data, 0,
				data.Length);
		}

		/// <summary>
		/// Casts a char array segment to a data buffer.
		/// </summary>
		/// <param name="data">The <see cref="ArraySegment{Char}"/> to be cast.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/> 
		/// <see cref="DataBufferType.CharArraySegment"/> if
		/// <see cref="ArraySegment{Char}.Count"/> of <paramref name="data"/>
		/// is greater than 0; otherwise of <see cref="Type"/>
		/// <see cref="DataBufferType.Empty"/>.</returns>
		public static implicit operator DataBuffer(ArraySegment<char> data)
		{
			if (data.Count == 0) return _empty;
			return new DataBuffer(DataBufferType.CharArraySegment, data.Array,
				data.Offset, data.Count);
		}

		/// <summary>
		/// Casts an integer array to a data buffer.
		/// </summary>
		/// <param name="data">The <see cref="Int32"/> array to be cast.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/> 
		/// <see cref="DataBufferType.Int32ArraySegment"/> if
		/// <paramref name="data"/> is not <see langword="null"/> and
		/// <see cref="Array.Length"/> is greater than 0; otherwise of
		/// <see cref="Type"/> <see cref="DataBufferType.Empty"/>.</returns>
		public static implicit operator DataBuffer(int[] data)
		{
			if (data == null || data.Length == 0) return _empty;
			return new DataBuffer(DataBufferType.Int32ArraySegment, data, 0,
				data.Length);
		}

		/// <summary>
		/// Casts an integer array segment to a data buffer.
		/// </summary>
		/// <param name="data">The <see cref="ArraySegment{Int32}"/> to be cast.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/> 
		/// <see cref="DataBufferType.Int32ArraySegment"/> if
		/// <see cref="ArraySegment{Int32}.Count"/> of <paramref name="data"/>
		/// is greater than 0; otherwise of <see cref="Type"/>
		/// <see cref="DataBufferType.Empty"/>.</returns>
		public static implicit operator DataBuffer(ArraySegment<int> data)
		{
			if (data.Count == 0) return _empty;
			return new DataBuffer(DataBufferType.Int32ArraySegment, data.Array,
				data.Offset, data.Count);
		}

		/// <summary>
		/// Casts a long array to a data buffer.
		/// </summary>
		/// <param name="data">The <see cref="Int64"/> array to be cast.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/> 
		/// <see cref="DataBufferType.Int64ArraySegment"/> if
		/// <paramref name="data"/> is not <see langword="null"/> and
		/// <see cref="Array.Length"/> is greater than 0; otherwise of
		/// <see cref="Type"/> <see cref="DataBufferType.Empty"/>.</returns>
		public static implicit operator DataBuffer(long[] data)
		{
			if (data == null || data.Length == 0) return _empty;
			return new DataBuffer(DataBufferType.Int64ArraySegment, data, 0,
				data.Length);
		}

		/// <summary>
		/// Casts a long array segment to a data buffer.
		/// </summary>
		/// <param name="data">The <see cref="ArraySegment{Int64}"/> to be cast.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/> 
		/// <see cref="DataBufferType.Int64ArraySegment"/> if
		/// <see cref="ArraySegment{Int64}.Count"/> of <paramref name="data"/>
		/// is greater than 0; otherwise of <see cref="Type"/>
		/// <see cref="DataBufferType.Empty"/>.</returns>
		public static implicit operator DataBuffer(ArraySegment<long> data)
		{
			if (data.Count == 0) return _empty;
			return new DataBuffer(DataBufferType.Int64ArraySegment, data.Array,
				data.Offset, data.Count);
		}

		/// <summary>
		/// Casts a string to a data buffer.
		/// </summary>
		/// <param name="data">The <see cref="String"/> to be cast.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/> 
		/// <see cref="DataBufferType.String"/> if
		/// <see cref="String.IsNullOrEmpty"/> of <paramref name="data"/>
		/// is <see langword="false"/>; otherwise of <see cref="Type"/>
		/// <see cref="DataBufferType.Empty"/>.</returns>
		public static implicit operator DataBuffer(string data)
		{
			if (string.IsNullOrEmpty(data)) return _empty;
			return new DataBuffer(DataBufferType.String, data, 0, data.Length);
		}

		/// <summary>
		/// Casts a string builder segment to a data buffer.
		/// </summary>
		/// <param name="data">The <see cref="StringBuilderSegment"/> to be cast.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/> 
		/// <see cref="DataBufferType.StringBuilderSegment"/> if
		/// <paramref name="data"/> is not <see langword="null"/> and
		/// <see cref="StringBuilderSegment.Count"/> is greater than 0; otherwise of
		/// <see cref="Type"/> <see cref="DataBufferType.Empty"/>.</returns>
		public static implicit operator DataBuffer(StringBuilderSegment data)
		{
			if (data.StringBuilder == null || data.Count == 0) return _empty;
			return new DataBuffer(DataBufferType.StringBuilderSegment, data.StringBuilder, data.Offset, data.Count);
		}

		/// <summary>
		/// Casts a string builder to a data buffer.
		/// </summary>
		/// <param name="data">The <see cref="StringBuilder"/> to be cast.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/> 
		/// <see cref="DataBufferType.StringBuilderSegment"/> if
		/// <paramref name="data"/> is not <see langword="null"/> and
		/// <see cref="StringBuilder.Length"/> of <paramref name="data"/> is
		/// greater than 0; otherwise of <see cref="Type"/>
		/// <see cref="DataBufferType.Empty"/>.</returns>
		/// <remarks>
		/// <para>The thread affine internal string buffer of
		/// <see cref="StringBuilder"/> is used for read/write operations.
		/// Hence, no other thread should perform operations on
		/// <paramref name="data"/> while one thread is using the returned
		/// <see cref="DataBuffer"/> instance.</para>
		/// </remarks>
		public static implicit operator DataBuffer(StringBuilder data)
		{
			if (data == null || data.Length == 0) return _empty;
			return new DataBuffer(DataBufferType.StringBuilderSegment, data, 0, data.Length);
		}
		/// <summary>
		/// Creates a data buffer of specified length from a char array.
		/// </summary>
		/// <param name="data">The <see cref="Char"/> array data source.</param>
		/// <param name="length">The length of the segment of
		/// <paramref name="data"/> to use.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/>
		/// <see cref="DataBufferType.CharArraySegment"/>, sourced from
		/// <paramref name="data"/> with an offset of 0 and length of
		/// <paramref name="length"/> if <paramref name="length"/> is greater
		/// than 0; other of <see cref="Type"/> <see cref="DataBufferType.Empty"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="data"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="length"/> is less than 0.</para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <para><paramref name="length"/> is greater than the
		/// <see cref="Array.Count"/> of <paramref name="data"/>.</para>
		/// </exception>
		public static DataBuffer Create(char[] data, int length)
		{
			return new ArraySegment<char>(data, 0, length);
		}

		/// <summary>
		/// Creates a data buffer of specified offset and length from a char array.
		/// </summary>
		/// <param name="data">The <see cref="Char"/> array data source.</param>
		/// <param name="offset">The offset of the segment of
		/// <paramref name="data"/> to use.</param>
		/// <param name="length">The length of the segment of
		/// <paramref name="data"/> to use.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/>
		/// <see cref="DataBufferType.CharArraySegment"/>, sourced from
		/// <paramref name="data"/> with an offset of <paramref name="offset"/>
		/// and length of <paramref name="length"/> if <paramref name="length"/>
		/// is greater than 0; other of <see cref="Type"/>
		/// <see cref="DataBufferType.Empty"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="data"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is less than 0.</para>
		/// <para>-or-</para>
		/// <para><paramref name="length"/> is less than 0.</para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <para>The segment specified by <paramref name="offset"/> and
		/// <paramref name="length"/> stretches beyond the
		/// <see cref="Array.Count"/> of <paramref name="data"/>.</para>
		/// </exception>
		public static DataBuffer Create(char[] data, int offset, int length)
		{
			return new ArraySegment<char>(data, offset, length);
		}


		/// <summary>
		/// Creates a data buffer of specified length from a byte array.
		/// </summary>
		/// <param name="data">The <see cref="Byte"/> array data source.</param>
		/// <param name="length">The length of the segment of
		/// <paramref name="data"/> to use.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/>
		/// <see cref="DataBufferType.ByteArraySegment"/>, sourced from
		/// <paramref name="data"/> with an offset of 0 and length of
		/// <paramref name="length"/> if <paramref name="length"/> is greater
		/// than 0; other of <see cref="Type"/> <see cref="DataBufferType.Empty"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="data"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="length"/> is less than 0.</para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <para><paramref name="length"/> is greater than the
		/// <see cref="Array.Count"/> of <paramref name="data"/>.</para>
		/// </exception>
		public static DataBuffer Create(byte[] data, int length)
		{
			return new ArraySegment<byte>(data, 0, length);
		}

		/// <summary>
		/// Creates a data buffer of specified offset and length from a byte array.
		/// </summary>
		/// <param name="data">The <see cref="Byte"/> array data source.</param>
		/// <param name="offset">The offset of the segment of
		/// <paramref name="data"/> to use.</param>
		/// <param name="length">The length of the segment of
		/// <paramref name="data"/> to use.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/>
		/// <see cref="DataBufferType.ByteArraySegment"/>, sourced from
		/// <paramref name="data"/> with an offset of <paramref name="offset"/>
		/// and length of <paramref name="length"/> if <paramref name="length"/>
		/// is greater than 0; other of <see cref="Type"/>
		/// <see cref="DataBufferType.Empty"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="data"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is less than 0.</para>
		/// <para>-or-</para>
		/// <para><paramref name="length"/> is less than 0.</para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <para>The segment specified by <paramref name="offset"/> and
		/// <paramref name="length"/> stretches beyond the
		/// <see cref="Array.Count"/> of <paramref name="data"/>.</para>
		/// </exception>
		public static DataBuffer Create(byte[] data, int offset, int length)
		{
			return new ArraySegment<byte>(data, offset, length);
		}


		/// <summary>
		/// Creates a data buffer of specified length from an integer array.
		/// </summary>
		/// <param name="data">The <see cref="Int32"/> array data source.</param>
		/// <param name="length">The length of the segment of
		/// <paramref name="data"/> to use.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/>
		/// <see cref="DataBufferType.Int32ArraySegment"/>, sourced from
		/// <paramref name="data"/> with an offset of 0 and length of
		/// <paramref name="length"/> if <paramref name="length"/> is greater
		/// than 0; other of <see cref="Type"/> <see cref="DataBufferType.Empty"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="data"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="length"/> is less than 0.</para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <para><paramref name="length"/> is greater than the
		/// <see cref="Array.Count"/> of <paramref name="data"/>.</para>
		/// </exception>
		public static DataBuffer Create(int[] data, int length)
		{
			return new ArraySegment<int>(data, 0, length);
		}

		/// <summary>
		/// Creates a data buffer of specified offset and length from an integer array.
		/// </summary>
		/// <param name="data">The <see cref="Int32"/> array data source.</param>
		/// <param name="offset">The offset of the segment of
		/// <paramref name="data"/> to use.</param>
		/// <param name="length">The length of the segment of
		/// <paramref name="data"/> to use.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/>
		/// <see cref="DataBufferType.Int32ArraySegment"/>, sourced from
		/// <paramref name="data"/> with an offset of <paramref name="offset"/>
		/// and length of <paramref name="length"/> if <paramref name="length"/>
		/// is greater than 0; other of <see cref="Type"/>
		/// <see cref="DataBufferType.Empty"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="data"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is less than 0.</para>
		/// <para>-or-</para>
		/// <para><paramref name="length"/> is less than 0.</para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <para>The segment specified by <paramref name="offset"/> and
		/// <paramref name="length"/> stretches beyond the
		/// <see cref="Array.Count"/> of <paramref name="data"/>.</para>
		/// </exception>
		public static DataBuffer Create(int[] data, int offset, int length)
		{
			return new ArraySegment<int>(data, offset, length);
		}


		/// <summary>
		/// Creates a data buffer of specified length from a long array.
		/// </summary>
		/// <param name="data">The <see cref="Int64"/> array data source.</param>
		/// <param name="length">The length of the segment of
		/// <paramref name="data"/> to use.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/>
		/// <see cref="DataBufferType.Int64ArraySegment"/>, sourced from
		/// <paramref name="data"/> with an offset of 0 and length of
		/// <paramref name="length"/> if <paramref name="length"/> is greater
		/// than 0; other of <see cref="Type"/> <see cref="DataBufferType.Empty"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="data"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="length"/> is less than 0.</para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <para><paramref name="length"/> is greater than the
		/// <see cref="Array.Count"/> of <paramref name="data"/>.</para>
		/// </exception>
		public static DataBuffer Create(long[] data, int length)
		{
			return new ArraySegment<long>(data, 0, length);
		}

		/// <summary>
		/// Creates a data buffer of specified offset and length from a long array.
		/// </summary>
		/// <param name="data">The <see cref="Int64"/> array data source.</param>
		/// <param name="offset">The offset of the segment of
		/// <paramref name="data"/> to use.</param>
		/// <param name="length">The length of the segment of
		/// <paramref name="data"/> to use.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/>
		/// <see cref="DataBufferType.Int64ArraySegment"/>, sourced from
		/// <paramref name="data"/> with an offset of <paramref name="offset"/>
		/// and length of <paramref name="length"/> if <paramref name="length"/>
		/// is greater than 0; other of <see cref="Type"/>
		/// <see cref="DataBufferType.Empty"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="data"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is less than 0.</para>
		/// <para>-or-</para>
		/// <para><paramref name="length"/> is less than 0.</para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <para>The segment specified by <paramref name="offset"/> and
		/// <paramref name="length"/> stretches beyond the
		/// <see cref="Array.Count"/> of <paramref name="data"/>.</para>
		/// </exception>
		public static DataBuffer Create(long[] data, int offset, int length)
		{
			return new ArraySegment<long>(data, offset, length);
		}

		/// <summary>
		/// Creates a data buffer of specified length from a string builder.
		/// </summary>
		/// <param name="data">The <see cref="StringBuilder"/> data source.</param>
		/// <param name="length">The length of the segment of
		/// <paramref name="data"/> to use.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/>
		/// <see cref="DataBufferType.StringBuilder"/>, sourced from
		/// <paramref name="data"/> with an offset of 0 and length of
		/// <paramref name="length"/> if <paramref name="length"/> is greater
		/// than 0; other of <see cref="Type"/> <see cref="DataBufferType.Empty"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="data"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="length"/> is less than 0.</para>
		/// </exception>
		/// <remarks>
		/// <para>The thread affine internal string buffer of
		/// <see cref="StringBuilder"/> is used for read/write operations.
		/// Hence, no other thread should perform operations on
		/// <paramref name="data"/> while one thread is using the returned
		/// <see cref="DataBuffer"/> instance.</para>
		/// <para>The <see cref="StringBuilder.Capacity"/> of
		/// <paramref name="data"/> will be increased as necessary on demand
		/// to accomodate the segment specified by <paramref name="length"/>.</para>
		/// </remarks>
		public static DataBuffer Create(StringBuilder data, int length)
		{
			return new StringBuilderSegment(data, 0, length);
		}

		/// <summary>
		/// Creates a data buffer of specified offset and length from a
		/// string builder.
		/// </summary>
		/// <param name="data">The <see cref="StringBuilder"/> data source.</param>
		/// <param name="offset">The offset of the segment of
		/// <paramref name="data"/> to use.</param>
		/// <param name="length">The length of the segment of
		/// <paramref name="data"/> to use.</param>
		/// <returns>A <see cref="DataBuffer"/> of <see cref="Type"/>
		/// <see cref="DataBufferType.StringBuilderSegment"/>, sourced from
		/// <paramref name="data"/> with an offset of <paramref name="offset"/>
		/// and length of <paramref name="length"/> if <paramref name="length"/>
		/// is greater than 0; other of <see cref="Type"/>
		/// <see cref="DataBufferType.Empty"/>.
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="data"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is less than 0.</para>
		/// <para>-or-</para>
		/// <para><paramref name="length"/> is less than 0.</para>
		/// </exception>
		/// <remarks>
		/// <para>The thread affine internal string buffer of
		/// <see cref="StringBuilder"/> is used for read/write operations.
		/// Hence, no other thread should perform operations on
		/// <paramref name="data"/> while one thread is using the returned
		/// <see cref="DataBuffer"/> instance.</para>
		/// <para>The <see cref="StringBuilder.Capacity"/> of
		/// <paramref name="data"/> will be increased as necessary on demand
		/// to accomodate the segment specified by <paramref name="offset"/>
		/// and <paramref name="length"/>.</para>
		/// </remarks>
		public static DataBuffer Create(StringBuilder data, int offset, int length)
		{
			return new StringBuilderSegment(data, offset, length);
		}

		/// <summary>
		/// Creates a new data buffer with a shorter reference to the same data
		/// in the existing instance.
		/// </summary>
		/// <param name="length">The length to shorten to, measured in per
		/// type lengths, not necessarily bytes.</param>
		/// <returns>A new <see cref="DataBuffer"/> containing the same data object
		/// and <see cref="Offset"/> as the existing offset, but with the <see cref="Length"/>
		/// shortened to <paramref name="length"/>. If <paramref name="length"/> is 0,
		/// then <see cref="Empty"/> is returned, as is the case when
		/// <see cref="Empty"/> itself is restricted.</returns>
		/// <exception cref="InvalidOperationException">
		/// <para><see cref="Type"/> is a non-object type, aside from
		/// <see cref="DataBufferType.Empty"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="length"/> is less than 0.</para>
		/// <para>-or-</para>
		/// <para><paramref name="length"/> is greater than the existing
		/// <see cref="Length"/>.</para>
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// <para><see cref="Type"/> is a type that doesn't encapsulate any
		/// objects, i.e. <see cref="DataBufferType.Int32"/>, or
		/// <see cref="DataBufferType.Int64"/>.</para>
		/// </exception>
		public DataBuffer Restrict(int length)
		{
			AssertValidOnlyForSegmentable();
			if (length == 0) return Empty;
			if (length < 0 || length > _dataUnion.SegmentValue.Length)
			{
				throw new ArgumentOutOfRangeException("length");
			}
			return new DataBuffer(_type, _object, _dataUnion.SegmentValue.Offset,
				length);
		}

		/// <summary>
		/// Creates a new data buffer refering to the same data in the existing
		/// instance, but with the starting offset moved up and the end point
		/// moved down.
		/// </summary>
		/// <param name="offsetIncrement">The amount to increase the offset by, measured in per
		/// type lengths, not necessarily bytes.</param>
		/// <param name="length">The length to shorten to, measured in per
		/// type lengths, not necessarily bytes.</param>
		/// <returns>A new <see cref="DataBuffer"/> containing the same data object
		/// , but with the <see cref="Offset"/> moved up by
		/// <paramref name="offsetIncrement"/>  and the <see cref="Length"/> shortened to
		/// <paramref name="length"/>. If <paramref name="length"/> is 0, then
		/// <see cref="Empty"/> is returned, as is the case when <see cref="Empty"/>
		/// itself is restricted.</returns>
		/// <exception cref="InvalidOperationException">
		/// <para><see cref="Type"/> is a type that doesn't encapsulate any
		/// objects, i.e. <see cref="DataBufferType.Int32"/>, or
		/// <see cref="DataBufferType.Int64"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="offsetIncrement"/> is less than 0.</para>
		/// <para>-or-</para>
		/// <para><paramref name="offsetIncrement"/> is greater than <see cref="Length"/>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="length"/> is less than 0.</para>
		/// <para>-or-</para>
		/// <para><paramref name="length"/> is greater than <see cref="Length"/>
		/// minus <paramref name="offsetIncrement"/>.</para>
		/// <para>-or-</para>
		/// </exception>
		public DataBuffer Restrict(int offsetIncrement, int length)
		{
			AssertValidOnlyForSegmentable();
			if (offsetIncrement < 0)
			{
				throw new ArgumentOutOfRangeException("offsetIncrement");
			}
			var maxLength = _dataUnion.SegmentValue.Length - offsetIncrement;
			if (maxLength < 0)
			{
				throw new ArgumentOutOfRangeException("offsetIncrement");
			}
			if (length == 0) return Empty;
			if (length < 0 || length > maxLength)
			{
				throw new ArgumentOutOfRangeException("length");				
			}
			return new DataBuffer(_type, _object, _dataUnion.SegmentValue.Offset +
				offsetIncrement, length);
		}

		/// <summary>
		/// Creates a new data buffer refering to the same data in the existing
		/// instance, but with the starting offset moved up and the same end point.
		/// </summary>
		/// <param name="offsetIncrement">The amount to increase the offset by, measured in per
		/// type lengths, not necessarily bytes.</param>
		/// <returns>A new <see cref="DataBuffer"/> containing the same data object,
		/// but with the offset moved up by <paramref name="offsetIncrement"/> while
		/// the end point is retained. If the increment would shorten the length to 0,
		/// then <see cref="Empty"/> is returned.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="offsetIncrement"/> is less than 0.</para>
		/// <para>-or-</para>
		/// <para><paramref name="offsetIncrement"/> is greater than the existing
		/// <see cref="Length"/>.</para>
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// <para><see cref="Type"/> is a type that doesn't encapsulate any
		/// objects, i.e. <see cref="DataBufferType.Int32"/>, or
		/// <see cref="DataBufferType.Int64"/>.</para>
		/// </exception>
		public DataBuffer RestrictOffset(int offsetIncrement)
		{
			AssertValidOnlyForSegmentable();
			if (offsetIncrement < 0)
			{
				throw new ArgumentOutOfRangeException("offsetIncrement");
			}
			var newLength = _dataUnion.SegmentValue.Length - offsetIncrement;
			if (newLength == 0) return Empty;
			if (newLength < 0)
			{
				throw new ArgumentOutOfRangeException("offsetIncrement");
			}
			return new DataBuffer(_type, _object, _dataUnion.SegmentValue.Offset +
				offsetIncrement, newLength);
		}

		private static readonly int _charShift = (int) Math.Log(sizeof(char), 2);
		private static readonly int _intShift = (int) Math.Log(sizeof(int), 2);
		private static readonly int _longShift = (int) Math.Log(sizeof(long), 2);
		private const int _intMask = sizeof(int) - 1;
		private const int _longMask = sizeof(long) - 1;

		/// <summary>
		/// Converts the string representation of a data buffer to its equivalent.
		/// A return value indicates whether the operation succeeded.
		/// </summary>
		/// <param name="type">The <see cref="DataBufferType"/> desired.</param>
		/// <param name="s">The <see cref="String"/> representation.</param>
		/// <param name="result">The equivalent <see cref="DataBuffer"/> output.
		/// If the conversion fails, then the value will be set to
		/// <see cref="Empty"/>.</param>
		/// <returns>Whether or not the conversion succeeded.</returns>
		/// <remarks><paramref name="result"/> is not guaranteed to have the same
		/// <see cref="Type"/> as <paramref name="type"/>, for example
		/// if <paramref name="s"/> is empty then the type of the equivalent
		/// <see cref="DataBuffer"/> may be <see cref="DataBufferType.Empty"/>.</remarks>
		public static unsafe bool TryParse(DataBufferType type, string s,
			out DataBuffer result)
		{
			switch(type)
			{
				case DataBufferType.Empty:
					result = Empty;
					return true;
				case DataBufferType.Int32:
					int i;
					if (int.TryParse(s, out i))
					{
						result = i;
						return true;
					}
					break;
				case DataBufferType.Int64:
					long l;
					if (long.TryParse(s, out l))
					{
						result = l;
						return true;
					}
					break;
				case DataBufferType.Int32ArraySegment:
					if (string.IsNullOrEmpty(s))
					{
						result = Empty;
						return true;
					}
					i = s.Length << _charShift;
					if ((i & _intMask) == 0)
					{
						var ints = new int[i >> _intShift];
						fixed(int *p = &ints[0])
						{
							var handle = GCHandle.Alloc(s, GCHandleType.Pinned);
							try
							{
								CopyMemory(new IntPtr(p), handle.AddrOfPinnedObject(), i);
							} finally
							{
								if (handle.IsAllocated) handle.Free();
							}
						}
						result = ints;
						return true;
					}
					break;
				case DataBufferType.Int64ArraySegment:
					if (string.IsNullOrEmpty(s))
					{
						result = Empty;
						return true;
					}
					i = s.Length << _charShift;
					if ((i & _longMask) == 0)
					{
						var longs = new long[i >> _longShift];
						fixed (long* p = &longs[0])
						{
							var handle = GCHandle.Alloc(s, GCHandleType.Pinned);
							try
							{
								CopyMemory(new IntPtr(p), handle.AddrOfPinnedObject(), i);
							}
							finally
							{
								if (handle.IsAllocated) handle.Free();
							}
						}
						result = longs;
						return true;
					}
					break;
				case DataBufferType.String:
					result = s;
					return true;
				case DataBufferType.CharArraySegment:
					result = string.IsNullOrEmpty(s) ? Empty : s.ToCharArray();
					return true;
				case DataBufferType.ByteArraySegment:
					result = string.IsNullOrEmpty(s) ? Empty : Encoding.Unicode.GetBytes(s);
					return true;
				case DataBufferType.StringBuilderSegment:
					result = string.IsNullOrEmpty(s) ? Empty : new StringBuilder(s);
					return true;
				default:
					throw InvalidOperationForType(type);
			}
			result = Empty;
			return false;
		}

		/// <summary>
		/// Converts the string representation of a data buffer to its equivalent.
		/// </summary>
		/// <param name="type">The <see cref="DataBufferType"/> desired.</param>
		/// <param name="s">The <see cref="String"/> representation.</param>
		/// <returns>The equivalent <see cref="DataBuffer"/>.</returns>
		/// <exception cref="FormatException">
		/// <para><paramref name="s"/> could not be converted.</para>
		/// </exception>
		/// <remarks>The equivalent is not guaranteed to have the same
		/// <see cref="Type"/> as <paramref name="type"/>, for example
		/// if <paramref name="s"/> is empty then the type of the equivalent
		/// <see cref="DataBuffer"/> may be <see cref="DataBufferType.Empty"/>.</remarks>
		public static DataBuffer Parse(DataBufferType type, string s)
		{
			DataBuffer result;
			if (TryParse(type, s, out result)) return result;
			throw new FormatException();
		}
		#endregion

		#region Mapping Methods
		/// <summary>
		/// 	<para>Overriden. Returns the hash code for this instance.</para>
		/// </summary>
		/// <returns>
		/// 	<para>A 32-bit signed integer that is the hash code for this instance.</para>
		/// </returns>
		/// <filterpriority>
		/// 	<para>2</para>
		/// </filterpriority>
		public override int GetHashCode()
		{
			switch (_type)
			{
				case DataBufferType.Empty:
					return 0;
				case DataBufferType.Int32:
					return _dataUnion.Int32Value;
				case DataBufferType.Int64:
					return _dataUnion.Int64Value.GetHashCode();
				case DataBufferType.String:
					return _object.GetHashCode();
				case DataBufferType.CharArraySegment:
					return GetHashCodeForChars();
				case DataBufferType.Int32ArraySegment:
				case DataBufferType.Int64ArraySegment:
				case DataBufferType.ByteArraySegment:
					return GetHashCodeForObjects();
				case DataBufferType.StringBuilderSegment:
					return MakeStringForStringBuilder().GetHashCode();
				default:
					throw InvalidOperationForType();
			}
		}

		/// <summary>
		/// Gets an integer identifier suitable for modding for partitioning.
		/// </summary>
		/// <returns>The <see cref="Int32"/> partitioning identifier.</returns>
		public int GetObjectId()
		{
			switch(_type)
			{
				case DataBufferType.Empty:
					return 0;
				case DataBufferType.Int32:
					return _dataUnion.Int32Value;
				case DataBufferType.Int64:
					return _dataUnion.Int64Value.GetHashCode();
				case DataBufferType.String:
				case DataBufferType.CharArraySegment:
				case DataBufferType.ByteArraySegment:
				case DataBufferType.Int32ArraySegment:
				case DataBufferType.Int64ArraySegment:
				case DataBufferType.StringBuilderSegment:
					return GetObjectIdForObjects();
				default:
					throw InvalidOperationForType();
			}
		}

		/// <summary>
		/// 	<para>Overriden. Returns a string representation
		/// of this instance.</para>
		/// </summary>
		/// <returns>
		/// 	<para>A <see cref="String"/> representing
		/// the data contained in this instance.</para>
		/// </returns>
		/// <filterpriority>
		/// 	<para>2</para>
		/// </filterpriority>
		public unsafe override string ToString()
		{
			switch(_type)
			{
				case DataBufferType.Empty:
					return string.Empty;
				case DataBufferType.Int32:
					return _dataUnion.Int32Value.ToString();
				case DataBufferType.Int64:
					return _dataUnion.Int64Value.ToString();
				case DataBufferType.String:
					return (string)_object;
				case DataBufferType.CharArraySegment:
					return new string((char[])_object,
						_dataUnion.SegmentValue.Offset,
						_dataUnion.SegmentValue.Length);
				case DataBufferType.ByteArraySegment:
					return Encoding.Unicode.GetString((byte[])_object,
						_dataUnion.SegmentValue.Offset,
						_dataUnion.SegmentValue.Length);
				case DataBufferType.Int32ArraySegment:
					var l = _dataUnion.SegmentValue.Length << _intShift;
					var o = _dataUnion.SegmentValue.Offset << _intShift;
					goto CopyToString;
				case DataBufferType.Int64ArraySegment:
					l = _dataUnion.SegmentValue.Length << _longShift;
					o = _dataUnion.SegmentValue.Offset << _longShift;
				CopyToString:
					var s = StringBuilderInternalAccessor.Instance.AllocateString(l >> _charShift);
					var h1 = GCHandle.Alloc(s, GCHandleType.Pinned);
					try
					{
						var h2 = GCHandle.Alloc(_object, GCHandleType.Pinned);
						try
						{
							var src = h2.AddrOfPinnedObject();
							if (o > 0)
							{
								src = new IntPtr(((byte*) src.ToPointer()) + o);
							}
							CopyMemory(h1.AddrOfPinnedObject(), src, l);
						} finally
						{
							if (h2.IsAllocated) h2.Free();
						}
					}
					finally
					{
						if (h1.IsAllocated) h1.Free();
					}
					return s;
				case DataBufferType.StringBuilderSegment:
					return MakeStringForStringBuilder();
				default:
					throw InvalidOperationForType();
			}
		}

		/// <summary>
		/// Gets a byte array corresponding to the data contained in this
		/// instance.
		/// </summary>
		/// <returns>A <see cref="Byte"/> array copy of the data contained in
		/// this instance.</returns>
		public byte[] GetBinary()
		{
			var len = ByteLength;
			var ret = new byte[len];
			if (len == 0) return ret;
			CopyBinary(new ArraySegment<byte>(ret));
			return ret;
		}

		/// <summary>
		/// Converts the buffer value to an integer.
		/// </summary>
		/// <remarks>
		/// <para>If the data buffer holds a byte array segment, then
		/// bit conversion is used.</para>
		/// <para>If the data buffer holds a numeric array segment, the first
		/// value, i.e. at the array position of offset, is used.</para>
		/// <para>If the data buffer holds text, then the text is parsed.</para>
		/// </remarks>
		/// <returns>The resulting <see cref="Int32"/>.</returns>
		/// <exception cref="OverflowException">
		/// <para>The <see cref="Type"/> is
		/// <see cref="DataBufferType.Int64"/> or
		/// <see cref="DataBufferType.Int64ArraySegment"/>
		/// , but the value is too large for a <see cref="Int32"/>.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="Type"/> is
		/// <see cref="DataBufferType.String"/> or
		/// <see cref="DataBufferType.CharArraySegment"/> or
		/// <see cref="DataBufferType.StringBuilderSegment"/>
		/// , but the text specified value is too large for a
		/// <see cref="Int32"/>.</para>
		/// </exception>
		/// <exception cref="FormatException">
		/// <para>The <see cref="Type"/> is
		/// <see cref="DataBufferType.String"/> or
		/// <see cref="DataBufferType.CharArraySegment"/> or
		/// <see cref="DataBufferType.StringBuilderSegment"/>
		/// , but the contained text is not in a valid format for
		/// <see cref="Int32"/>.</para>
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// <para>The <see cref="Type"/> can't be cast to an
		/// <see cref="Int32"/>.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="Type"/> is
		/// <see cref="DataBufferType.ByteArraySegment"/>, but 
		/// <see cref="Length"/> is less than the size of an
		/// <see cref="Int32"/>.</para>
		/// </exception>
		public int ToInt32()
		{
			switch (_type)
			{
				case DataBufferType.Int32:
					return _dataUnion.Int32Value;
				case DataBufferType.Int64:
					return checked((int)_dataUnion.Int64Value);
				case DataBufferType.Int32ArraySegment:
					return ((int[])_object)[
						_dataUnion.SegmentValue.Offset];
				case DataBufferType.Int64ArraySegment:
					return checked((int)((long[])_object)[
						_dataUnion.SegmentValue.Offset]);
				case DataBufferType.ByteArraySegment:
					var segValue = _dataUnion.SegmentValue;
					if (segValue.Length < sizeof(int))
					{
						throw new InvalidOperationException(string.Format(
							"Length of byte array segment is {0}, which is too short",
							segValue.Length));
					}
					return BitConverter.ToInt32((byte[])_object,
						segValue.Offset);
				case DataBufferType.CharArraySegment:
					var s = new string((char[]) _object,
					   _dataUnion.SegmentValue.Offset,
					   _dataUnion.SegmentValue.Length);
					goto ParseString;
				case DataBufferType.StringBuilderSegment:
					var sbd = EnsureStringBuilderCapacity();
					s = sbd.ToString(_dataUnion.SegmentValue.Offset,
						_dataUnion.SegmentValue.Length);
					goto ParseString;
				case DataBufferType.String:
					s = (string)_object;
				ParseString:
					return int.Parse(s);
				default:
					throw InvalidOperationForType();
			}
		}

		/// <summary>
		/// Converts the buffer value to a long.
		/// </summary>
		/// <remarks>
		/// <para>If the data buffer holds a byte array segment, then
		/// bit conversion is used.</para>
		/// <para>If the data buffer holds a numeric array segment, the first
		/// value, i.e. at the array position of offset, is used.</para>
		/// <para>If the data buffer holds text, then the text is parsed.</para>
		/// </remarks>
		/// <returns>The resulting <see cref="Int64"/>.</returns>
		/// <exception cref="OverflowException">
		/// <para>The <see cref="Type"/> is
		/// <see cref="DataBufferType.String"/> or
		/// <see cref="DataBufferType.CharArraySegment"/> or
		/// <see cref="DataBufferType.StringBuilderSegment"/>
		/// , but the text specified value is too large for a
		/// <see cref="Int64"/>.</para>
		/// </exception>
		/// <exception cref="FormatException">
		/// <para>The <see cref="Type"/> is
		/// <see cref="DataBufferType.String"/> or
		/// <see cref="DataBufferType.CharArraySegment"/> or
		/// <see cref="DataBufferType.StringBuilderSegment"/>
		/// , but the contained text is not in a valid format for
		/// <see cref="Int64"/>.</para>
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// <para>The <see cref="Type"/> can't be cast to an
		/// <see cref="Int64"/>.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="Type"/> is
		/// <see cref="DataBufferType.ByteArraySegment"/>, but 
		/// <see cref="Length"/> is less than the size of an
		/// <see cref="Int64"/>.</para>
		/// </exception>
		public long ToInt64()
		{
			switch (_type)
			{
				case DataBufferType.Int32:
					return _dataUnion.Int32Value;
				case DataBufferType.Int64:
					return _dataUnion.Int64Value;
				case DataBufferType.Int32ArraySegment:
					return ((int[])_object)[
						_dataUnion.SegmentValue.Offset];
				case DataBufferType.Int64ArraySegment:
					return ((long[])_object)[
						_dataUnion.SegmentValue.Offset];
				case DataBufferType.ByteArraySegment:
					var segValue = _dataUnion.SegmentValue;
					if (segValue.Length < sizeof(long))
					{
						throw new InvalidOperationException(string.Format(
							"Length of byte array segment is {0}, which is too short",
							segValue.Length));
					}
					return BitConverter.ToInt64((byte[])_object,
						segValue.Offset);
				case DataBufferType.CharArraySegment:
					var s = new string((char[])_object,
					   _dataUnion.SegmentValue.Offset,
					   _dataUnion.SegmentValue.Length);
					goto ParseString;
				case DataBufferType.StringBuilderSegment:
					var sbd = EnsureStringBuilderCapacity();
					s = sbd.ToString(_dataUnion.SegmentValue.Offset,
						_dataUnion.SegmentValue.Length);
					goto ParseString;
				case DataBufferType.String:
					s = (string)_object;
				ParseString:
					return long.Parse(s);
				default:
					throw InvalidOperationForType();
			}
		}

		/// <summary>
		/// Copies the data contained in this instance to a binary segment.
		/// </summary>
		/// <param name="dst">The <see cref="Byte"/> pointer  pointing to the
		/// unmanaged buffer to copy the data to.</param>
		/// <param name="length">The length of the buffer pointed to by <paramref name="dst"/>.</param>
		/// <returns>The length of the data written, same as <see cref="ByteLength"/>.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="length"/> is less than <see cref="ByteLength"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="dst"/> is <see langword="null"/>.</para>
		/// </exception>
		public unsafe int CopyUnmanaged(byte* dst, int length)
		{
			if (dst == null) throw new ArgumentNullException("dst");
			var len = ByteLength;
			if (len > length)
			{
				throw new ArgumentOutOfRangeException("length");
			}
			if (len == 0) return 0;
			switch (_type)
			{
				case DataBufferType.Int32:
					*((int*)dst) = _dataUnion.Int32Value;
					return len;
				case DataBufferType.Int64:
					*((long*)dst) = _dataUnion.Int64Value;
					return len;
				case DataBufferType.StringBuilderSegment:
					EnsureStringBuilderCapacity();
					break;
			}
			if (IsObject)
			{
				var handle = new GCHandle();
				try
				{
					int off;
					var obj = GetObjectValue(out off, out len);
					handle = GCHandle.Alloc(obj, GCHandleType.Pinned);
					var ptrSrc = handle.AddrOfPinnedObject();
					if (off > 0)
					{
						ptrSrc = new IntPtr(((byte*) ptrSrc.ToPointer()) + off);
					}
					CopyMemory(new IntPtr(dst), ptrSrc, len);
					return len;
				}
				finally
				{
					if (handle.IsAllocated)
					{
						handle.Free();
					}
				}
			}
			throw new ApplicationException(string.Format("Type {0} not handled", _type));
		}

		/// <summary>
		/// Copies the data contained in this instance to a binary segment.
		/// </summary>
		/// <param name="segment">The <see cref="ArraySegment{Byte}"/> to copy the data to.</param>
		/// <returns>The number of bytes copied.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><see cref="ArraySegment{Byte}.Count"/> of <paramref name="segment"/> is less than the
		/// <see cref="ByteLength"/> of this instance.</para>
		/// </exception>
		public unsafe int CopyBinary(ArraySegment<byte> segment)
		{
			var len = ByteLength;
			if (len == 0) return 0;
			if (len > segment.Count)
			{
				throw new ArgumentOutOfRangeException("segment");
			}
			switch(_type)
			{
				case DataBufferType.Int32:
					fixed (byte* p = &segment.Array[segment.Offset])
					{
						*((int*) p) = _dataUnion.Int32Value;
					}
					return len;
				case DataBufferType.Int64:
					fixed (byte* p = &segment.Array[segment.Offset])
					{
						*((long*) p) = _dataUnion.Int64Value;
					}
					return len;
				case DataBufferType.StringBuilderSegment:
					EnsureStringBuilderCapacity();
					break;
			}
			if (IsObject)
			{
				var handle = new GCHandle();
				try
				{
					int off;
					var obj = GetObjectValue(out off, out len);
					handle = GCHandle.Alloc(obj, GCHandleType.Pinned);
					fixed (byte* ptrDst = &segment.Array[segment.Offset])
					{
						var ptrSrc = handle.AddrOfPinnedObject();
						if (off > 0)
						{
							ptrSrc = new IntPtr(((byte*)ptrSrc.ToPointer()) + off);
						}
						CopyMemory(new IntPtr(ptrDst), ptrSrc, len);
					}
					return len;
				}
				finally
				{
					if (handle.IsAllocated)
					{
						handle.Free();
					}
				}
			}
			throw new ApplicationException(string.Format("Type {0} not handled", _type));
		}

		/// <summary>
		/// Determines whether the data contained in this instance is the same as the data
		/// contained in another instance.
		/// </summary>
		/// <param name="other">The other <see cref="DataBuffer"/> instance.</param>
		/// <returns><see langword="true"/> if the two contain the same data, otherwise
		/// <see langword="false"/>.</returns>
		public unsafe bool EquivalentTo(DataBuffer other)
		{
			var len = ByteLength;
			if (len != other.ByteLength) return false;
			if (len == 0) return true;
			byte *p1 = null;
			byte* p2 = null;
			var handle1 = new GCHandle();
			var handle2 = new GCHandle();
			try
			{
				switch(_type)
				{
					case DataBufferType.Int32:
						// can't take address of _dataUnion.Int32Value directly,
						// since this instance might be boxed.
						var i = _dataUnion.Int32Value;
						p1 = (byte*)&i;
						break;
					case DataBufferType.Int64:
						var l = _dataUnion.Int64Value;
						p1 = (byte*)&l;
						break;
				}
				if ((_type & DataBufferType.Object) == DataBufferType.Object)
				{
					int off;
					var obj = GetObjectValue(out off, out len);
					handle1 = GCHandle.Alloc(obj, GCHandleType.Pinned);
					p1 = ((byte*)handle1.AddrOfPinnedObject().ToPointer()) + off;
				}
				if (p1 == null)
				{
					throw new ApplicationException(string.Format("Type {0} not handled", _type));
				}
				switch (other._type)
				{
					case DataBufferType.Int32:
						// "other" is on stack, so can take address
						p2 = (byte*)&other._dataUnion.Int32Value;
						break;
					case DataBufferType.Int64:
						p2 = (byte*)&other._dataUnion.Int64Value;
						break;
				}
				if ((other._type & DataBufferType.Object) == DataBufferType.Object)
				{
					int off;
					var obj = other.GetObjectValue(out off, out len);
					handle2 = GCHandle.Alloc(obj, GCHandleType.Pinned);
					p2 = ((byte*)handle2.AddrOfPinnedObject().ToPointer()) + off;
				}
				if (p2 == null)
				{
					throw new ApplicationException(string.Format("Other data buffer type {0} not handled", other._type));
				}
				for(; len > 0; --len, ++p1, ++p2)
				{
					if (*p1 != *p2) return false;
				}
				return true;
			}
			finally
			{
				if (handle1.IsAllocated) handle1.Free();
				if (handle2.IsAllocated) handle2.Free();
			}
		}
		#endregion

		#region Binary Serializing

		/// <summary>
		/// 	<para>Serialize the class data to a stream.</para>
		/// </summary>
		/// <param name="writer">
		/// 	<para>The <see cref="IPrimitiveWriter"/> that writes to the stream.</para>
		/// </param>
		unsafe public void Serialize(IPrimitiveWriter writer)
		{
			int l;
			writer.Write((int)_type);
			switch(_type)
			{
				case DataBufferType.Empty:
					break;
				case DataBufferType.Int32:
					writer.Write(_dataUnion.Int32Value);
					break;
				case DataBufferType.Int64:
					writer.Write(_dataUnion.Int64Value);
					break;
				case DataBufferType.StringBuilderSegment:
					var sbd = EnsureStringBuilderCapacity();
					l = _dataUnion.SegmentValue.Length;
					var ca = new char[l];
					sbd.CopyTo(_dataUnion.SegmentValue.Offset, ca, 0, l);
					writer.Write(l);
					writer.Write(ca);
					break;
				case DataBufferType.String:
					writer.Write((string)_object);
					break;
				case DataBufferType.CharArraySegment:
					l = _dataUnion.SegmentValue.Length;
					writer.Write(l);
					writer.Write((char[])_object, _dataUnion.SegmentValue.Offset, l);
					break;
				case DataBufferType.ByteArraySegment:
					l = _dataUnion.SegmentValue.Length;
					writer.Write(l);
					writer.Write((byte[])_object, _dataUnion.SegmentValue.Offset, l);
					break;
				case DataBufferType.Int32ArraySegment:
					l = _dataUnion.SegmentValue.Length;
					writer.Write(l);
					fixed (int* p = &((int[])_object)[_dataUnion.SegmentValue.Offset])
					{
						for(var pl = p; l > 0; ++pl, --l)
						{
							writer.Write(*pl);
						}
					}
					break;
				case DataBufferType.Int64ArraySegment:
					l = _dataUnion.SegmentValue.Length;
					writer.Write(l);
					fixed (long* p = &((long[])_object)[_dataUnion.SegmentValue.Offset])
					{
						for (var pl = p; l > 0; ++pl, --l)
						{
							writer.Write(*pl);
						}
					}
					break;
				default:
					throw InvalidOperationForType();
			}
		}

		private void SetForObject(object o, int l)
		{
			_object = o;
			_dataUnion = new NonobjectDataUnion
			{
				SegmentValue = new ObjectSegment
				{
					Offset = 0,
					Length = l
				}
			};			
		}

		/// <summary>
		/// 	<para>Deserialize the class data from a stream.</para>
		/// </summary>
		/// <param name="reader">
		/// 	<para>The <see cref="IPrimitiveReader"/> that extracts used to extra data from a stream.</para>
		/// </param>
		/// <param name="version">
		/// 	<para>The value of <see cref="CurrentVersion"/> that was written to the stream when it was originally serialized to a stream; the version of the <paramref name="reader"/> data.</para>
		/// </param>
		unsafe public void Deserialize(IPrimitiveReader reader, int version)
		{
			if (version == 1)
			{
				_type = (DataBufferType) reader.ReadInt32();
				switch(_type)
				{
					case DataBufferType.Empty:
						_object = null;
						_dataUnion = new NonobjectDataUnion();
						break;
					case DataBufferType.Int32:
						_object = null;
						_dataUnion = new NonobjectDataUnion
		             	{
		             		Int32Value = reader.ReadInt32()
		             	};
						break;
					case DataBufferType.Int64:
						_object = null;
						_dataUnion = new NonobjectDataUnion
						{
							Int64Value = reader.ReadInt64()
						};
						break;
					case DataBufferType.StringBuilderSegment:
						var l = reader.ReadInt32();
						var ca = reader.ReadChars(l);
						SetForObject(new StringBuilder(new string(ca)), l);
						break;
					case DataBufferType.String:
						var s = reader.ReadString();
						SetForObject(s, s.Length);
						break;
					case DataBufferType.CharArraySegment:
						l = reader.ReadInt32();
						ca = reader.ReadChars(l);
						SetForObject(ca, l);
						break;
					case DataBufferType.ByteArraySegment:
						l = reader.ReadInt32();
						var ba = reader.ReadBytes(l);
						SetForObject(ba, l);
						break;
					case DataBufferType.Int32ArraySegment:
						l = reader.ReadInt32();
						var ia = new int[l];
						SetForObject(ia, l);
						fixed (int* p = &ia[0])
						{
							for (var pl = p; l > 0; ++pl, --l)
							{
								*pl = reader.ReadInt32();
							}
						}
						break;
					case DataBufferType.Int64ArraySegment:
						l = reader.ReadInt32();
						var la = new long[l];
						SetForObject(la, l);
						fixed (long* p = &la[0])
						{
							for (var pl = p; l > 0; ++pl, --l)
							{
								*pl = reader.ReadInt64();
							}
						}
						break;
					default:
						throw InvalidOperationForType();
				}
			}
			else
			{
				reader.Response = SerializationResponse.Unhandled;
			}
		}

		private const int currentVersion = 1;

		/// <summary>
		/// Returns a data buffer instance deserialized from a stream using the
		/// format version number read from the stream.
		/// </summary>
		/// <param name="reader">
		/// 	<para>The <see cref="IPrimitiveReader"/> that extracts used to extra data from a stream.</para>
		/// </param>
		/// <returns>The <see cref="DataBuffer"/> deserialized from
		/// <paramref name="reader"/>.</returns>
		/// <exception cref="ApplicationException">
		/// <para>The deserialization failed.</para>
		/// </exception>
		public static DataBuffer DeserializeValue(IPrimitiveReader reader)
		{
			var ret = new DataBuffer();
			var version = reader.ReadInt32();
			ret.Deserialize(reader, version);
			if (reader.Response != SerializationResponse.Success)
			{
				throw new ApplicationException("Could not deserialize");
			}
			return ret;
		}

		/// <summary>
		/// Writes the format version number to a stream then serializes the
		/// instance to the stream.
		/// </summary>
		/// <param name="writer">
		/// 	<para>The <see cref="IPrimitiveWriter"/> that writes to the stream.</para>
		/// </param>
		public void SerializeValue(IPrimitiveWriter writer)
		{
			writer.Write(CurrentVersion);
			Serialize(writer);
		}

		/// <summary>
		/// 	<para>Gets the current serialization data version of your object.  The <see cref="Serialize"/> method will write to the stream the correct format for this version.</para>
		/// </summary>
		public int CurrentVersion
		{
			get { return currentVersion; }
		}

		/// <summary>
		/// 	<para>Deprecated. Has no effect.</para>
		/// </summary>
		public bool Volatile
		{
			get { return false; }
		}

		void ICustomSerializable.Deserialize(IPrimitiveReader reader)
		{
			throw new NotImplementedException();
		}

		#endregion

		#region Equality

		/// <summary>
		/// 	<para>Indicates whether the current object is equal to another object of the same type.</para>
		/// </summary>
		/// <returns>
		/// 	<para>true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.</para>
		/// </returns>
		/// <param name="other">
		/// 	<para>An object to compare with this object.</para>
		/// </param>
		unsafe public bool Equals(DataBuffer other)
		{
			if (_type != other._type) return false;
			switch(_type)
			{
				case DataBufferType.Empty:
					return true;
				case DataBufferType.Int32:
					return _dataUnion.Int32Value == other._dataUnion.Int32Value;
				case DataBufferType.Int64:
					return _dataUnion.Int64Value == other._dataUnion.Int64Value;
				case DataBufferType.String:
					return _object.Equals(other._object);
				case DataBufferType.StringBuilderSegment:
					var sbd = (StringBuilder)_object;
					var sbdOther = (StringBuilder)other._object;
					var l = _dataUnion.SegmentValue.Length;
					if (l != other._dataUnion.SegmentValue.Length) return false;
					var off = _dataUnion.SegmentValue.Offset;
					var offOther = other._dataUnion.SegmentValue.Offset;
					if (ReferenceEquals(_object, other._object) && off == offOther) return true;
					sbd.EnsureCapacity(off + l);
					sbdOther.EnsureCapacity(offOther + l);
					return sbd.ToString(off, l) == sbdOther.ToString(offOther, l);
				case DataBufferType.CharArraySegment:
					l = _dataUnion.SegmentValue.Length;
					if (l != other._dataUnion.SegmentValue.Length) return false;
					off = _dataUnion.SegmentValue.Offset;
					offOther = other._dataUnion.SegmentValue.Offset;
					if (ReferenceEquals(_object, other._object) && off == offOther) return true;
					fixed (char* p = &((char[])_object)[off],
						po = &((char[])other._object)[offOther])
					{
						for(char * pl = p, pol = po; l > 0; --l, ++pl, ++pol)
						{
							if (*pl != *pol) return false;
						}
					}
					return true;
				case DataBufferType.ByteArraySegment:
					l = _dataUnion.SegmentValue.Length;
					if (l != other._dataUnion.SegmentValue.Length) return false;
					off = _dataUnion.SegmentValue.Offset;
					offOther = other._dataUnion.SegmentValue.Offset;
					if (ReferenceEquals(_object, other._object) && off == offOther) return true;
					fixed (byte* p = &((byte[])_object)[off],
						po = &((byte[])other._object)[offOther])
					{
						for (byte* pl = p, pol = po; l > 0; --l, ++pl, ++pol)
						{
							if (*pl != *pol) return false;
						}
					}
					return true;
				case DataBufferType.Int32ArraySegment:
					l = _dataUnion.SegmentValue.Length;
					if (l != other._dataUnion.SegmentValue.Length) return false;
					off = _dataUnion.SegmentValue.Offset;
					offOther = other._dataUnion.SegmentValue.Offset;
					if (ReferenceEquals(_object, other._object) && off == offOther) return true;
					fixed (int* p = &((int[])_object)[off],
						po = &((int[])other._object)[offOther])
					{
						for (int* pl = p, pol = po; l > 0; --l, ++pl, ++pol)
						{
							if (*pl != *pol) return false;
						}
					}
					return true;
				case DataBufferType.Int64ArraySegment:
					l = _dataUnion.SegmentValue.Length;
					if (l != other._dataUnion.SegmentValue.Length) return false;
					off = _dataUnion.SegmentValue.Offset;
					offOther = other._dataUnion.SegmentValue.Offset;
					if (ReferenceEquals(_object, other._object) && off == offOther) return true;
					fixed (long* p = &((long[])_object)[off],
						po = &((long[])other._object)[offOther])
					{
						for (long* pl = p, pol = po; l > 0; --l, ++pl, ++pol)
						{
							if (*pl != *pol) return false;
						}
					}
					return true;
				default:
					throw InvalidOperationForType();
			}
		}

		/// <summary>
		/// 	<para>Overriden. Indicates whether this instance and a specified object are equal.</para>
		/// </summary>
		/// <returns>
		/// 	<para>true if <paramref name="obj"/> and this instance are the same type and represent the same value; otherwise, false.</para>
		/// </returns>
		/// <param name="obj">
		/// 	<para>Another object to compare to.</para>
		/// </param>
		/// <filterpriority>
		/// 	<para>2</para>
		/// </filterpriority>
		public override bool Equals(object obj)
		{
			if (obj == null) return false;
			if (!(obj is DataBuffer)) return false;
			return Equals((DataBuffer)obj);
		}

		/// <summary>
		/// Performs equality comparison of two data buffers.
		/// </summary>
		/// <param name="buffer1">The first <see cref="DataBuffer"/>.</param>
		/// <param name="buffer2">The second <see cref="DataBuffer"/>.</param>
		/// <returns><see langword="true"/> if <paramref name="buffer1"/>
		/// and <paramref name="buffer2"/> are equal; otherwise
		/// <see langword="false"/>.</returns>
		public static bool operator ==(DataBuffer buffer1, DataBuffer buffer2)
		{
			return buffer1.Equals(buffer2);
		}

		/// <summary>
		/// Performs inequality comparison of two data buffers.
		/// </summary>
		/// <param name="buffer1">The first <see cref="DataBuffer"/>.</param>
		/// <param name="buffer2">The second <see cref="DataBuffer"/>.</param>
		/// <returns><see langword="true"/> if <paramref name="buffer1"/>
		/// and <paramref name="buffer2"/> are not equal; otherwise
		/// <see langword="false"/>.</returns>
		public static bool operator !=(DataBuffer buffer1, DataBuffer buffer2)
		{
			return !buffer1.Equals(buffer2);
		}
		#endregion
	}
}
