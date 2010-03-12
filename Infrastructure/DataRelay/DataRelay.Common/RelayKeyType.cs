using System;
using System.Text;
using MySpace.Common;

namespace MySpace.DataRelay
{
	/// <summary>
	/// Indicates what type of key is encoded in a <see cref="RelayMessage"/> and where/how it can be extracted.
	/// </summary>
	public enum RelayKeyType
	{
		/// <summary>
		/// Indicates that <see cref="RelayMessage.Id"/> is the key.
		/// </summary>
		Int32 = 0,
		/// <summary>
		/// Indicates that <see cref="RelayMessage.ExtendedId"/> is the key.
		/// </summary>
		ByteArray = 1,
		/// <summary>
		/// Indicates that <see cref="RelayMessage.ExtendedId"/> is the key
		/// serialized with <see cref="UTF8Encoding"/> with no header.
		/// This is the default way that data relay converts strings into
		/// extended id bytes when objects implement <see cref="IExtendedCacheParameter"/>.
		/// </summary>
		String = 2
	}
}
