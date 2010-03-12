using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.Common.Storage
{
	/// <summary>
	/// Specifies the type of data stored in a <see cref="DataBuffer"/>.
	/// </summary>
	public enum DataBufferType
	{
		// base types
		/// <summary>
		/// Specifies a <see cref="DataBuffer"/> that stores no data.
		/// </summary>
		Empty = 0x0,
		/// <summary>
		/// Specifies a <see cref="DataBuffer"/> that stores an
		/// <see cref="Int32"/>.
		/// </summary>
		Int32 = 0x1,
		/// <summary>
		/// Specifies a <see cref="DataBuffer"/> that stores an
		/// <see cref="Int64"/>.
		/// </summary>
		Int64 = 0x2,
		/// <summary>
		/// Specifies a <see cref="DataBuffer"/> that stores a
		/// <see cref="String"/>.
		/// </summary>
		String = 0x83,
		/// <summary>
		/// Specifies a <see cref="DataBuffer"/> that stores a
		/// <see cref="StringBuilder"/>.
		/// </summary>
		StringBuilderSegment = 0xA4,
		/// <summary>
		/// Specifies a <see cref="DataBuffer"/> that stores an
		/// <see cref="ArraySegment{Byte}"/>.
		/// </summary>
		ByteArraySegment = 0xE4,
		/// <summary>
		/// Specifies a <see cref="DataBuffer"/> that stores an
		/// <see cref="ArraySegment{Char}"/>.
		/// </summary>
		CharArraySegment = 0xE5,
		/// <summary>
		/// Specifies a <see cref="DataBuffer"/> that stores an
		/// <see cref="ArraySegment{Int32}"/>.
		/// </summary>
		Int32ArraySegment = 0xE6,
		/// <summary>
		/// Specifies a <see cref="DataBuffer"/> that stores an
		/// <see cref="ArraySegment{Int64}"/>.
		/// </summary>
		Int64ArraySegment = 0xE7,
		// informational masks
		/// <summary>
		/// Bit mask for <see cref="DataBufferType"/>s that specify that an
		/// object is stored.
		/// </summary>
		Object = 0x80,
		/// <summary>
		/// Bit mask for <see cref="DataBufferType"/>s that specify that
		/// segmentable data is stored, along with a specified segment.
		/// </summary>
		Segmentable = 0x20,
		/// <summary>
		/// Bit mask for <see cref="DataBufferType"/>s that specify that an
		/// <see cref="Array"/> is stored, along with a specified segment.
		/// </summary>
		ArraySegment = 0xE0,
	}
}
