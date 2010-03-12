using System;
using System.Collections.Generic;
using System.Text;
using BerkeleyDbWrapper;
using MySpace.Common.Storage;

namespace MySpace.BerkeleyDb.Facade
{
	/// <summary>
	/// Encapsulates the options used for
	/// <see cref="BerkeleyDbStorage.GetEntry(Int16, Int32, DataBuffer, DataBuffer, GetOptions)"/> and
	/// <see cref="BerkeleyDbStorage.GetEntryLength(Int16, Int32, DataBuffer)"/> and other overloads.
	/// </summary>
	public struct GetOptions
	{
		[Flags]
		internal enum GetFlags
		{
			Default = 0, // keep this 0 for parameterless constructor
			Partial = 1,
		}

		private GetFlags _flags;
		private int _offset;
		private int _length;

		/// <summary>
		/// Gets the default <see cref="GetOptions"/>.
		/// </summary>
		public static GetOptions Default { get { return new GetOptions(); } }

		private bool IsPartial { get { return (_flags & GetFlags.Partial) == GetFlags.Partial; } }

		/// <summary>
		/// Gets the offset for partial data access.
		/// </summary>
		/// <value>Always returns -1 if this instance isn't partial.</value>
		internal int Offset { get { return IsPartial ? _offset : -1; } }

		/// <summary>
		/// Gets the length for partial data access.
		/// </summary>
		/// <value>Always returns -1 if this instance isn't partial.</value>
		internal int Length { get { return IsPartial ? _length : -1; } }

		/// <summary>
		/// Gets the <see cref="GetOpFlags"/> for data access.
		/// </summary>
		internal GetOpFlags Flags { get { return GetOpFlags.Default; } }

		/// <summary>
		/// Validates this instance's fields and throws appropriate exceptions, if any.
		/// </summary>
		/// <param name="paramName">The parameter name used for any thrown
		/// exceptions.</param>
		internal void AssertValid(string paramName)
		{
			if (IsPartial && _offset < 0)
			{
				throw new ArgumentOutOfRangeException(paramName);
			}
		}

		/// <summary>
		/// Creates a <see cref="GetOptions"/> representing partial data access.
		/// </summary>
		/// <param name="offset">The offset location for partial data access.</param>
		/// <returns>The created <see cref="GetOptions"/>.</returns>
		public static GetOptions Partial(int offset)
		{
			return new GetOptions
	       	{
	       		_flags = GetFlags.Partial,
				_offset = offset,
				_length = -1
	       	};
		}

		/// <summary>
		/// Creates a <see cref="GetOptions"/> representing partial data access.
		/// </summary>
		/// <param name="offset">The offset location for partial data access.</param>
		/// <param name="length">The length for partial data access.</param>
		/// <returns>The created <see cref="GetOptions"/>.</returns>
		public static GetOptions Partial(int offset, int length)
		{
			return new GetOptions
			{
				_flags = GetFlags.Partial,
				_offset = offset,
				_length = length
			};
		}

		/// <summary>
		/// Converts an <see cref="Int32"/> to a partial data access instance.
		/// </summary>
		/// <param name="offset">The offset location for partial data access.</param>
		/// <returns>The created <see cref="GetOptions"/>.</returns>
		public static implicit operator GetOptions(int offset)
		{
			return Partial(offset);
		}
	}

	/// Encapsulates the options used for
	/// <see cref="BerkeleyDbStorage.SaveEntry(Int16, Int32, DataBuffer, DataBuffer, PutOptions)"/>
	/// and other overloads.
	public struct PutOptions
	{
		[Flags]
		internal enum PutFlags
		{
			Default = 0, // keep this 0 for parameterless constructor
			Partial = 1,
		}

		private PutFlags _flags;
		private int _offset;
		private int _length;

		/// <summary>
		/// Gets the default <see cref="PutOptions"/>.
		/// </summary>
		public static PutOptions Default { get { return new PutOptions(); } }

		private bool IsPartial { get { return (_flags & PutFlags.Partial) == PutFlags.Partial; } }

		/// <summary>
		/// Gets the offset for partial data access.
		/// </summary>
		/// <value>Always returns -1 if this instance isn't partial.</value>
		internal int Offset { get { return IsPartial ? _offset : -1; } }

		/// <summary>
		/// Gets the length for partial data access.
		/// </summary>
		/// <value>Always returns -1 if this instance isn't partial.</value>
		internal int Length { get { return IsPartial ? _length : -1; } }

		/// <summary>
		/// Gets the <see cref="PutOpFlags"/> for data access.
		/// </summary>
		internal PutOpFlags Flags { get { return PutOpFlags.Default; } }

		/// <summary>
		/// Validates this instance's fields and throws appropriate exceptions, if any.
		/// </summary>
		/// <param name="paramName">The parameter name used for any thrown
		/// exceptions.</param>
		internal void AssertValid(string paramName)
		{
			if (IsPartial && _offset < 0)
			{
				throw new ArgumentOutOfRangeException(paramName);
			}
		}

		/// <summary>
		/// Creates a <see cref="GetOptions"/> representing partial data access.
		/// </summary>
		/// <param name="offset">The offset location for partial data access.</param>
		/// <returns>The created <see cref="GetOptions"/>.</returns>
		public static PutOptions Partial(int offset)
		{
			return new PutOptions
			{
				_flags = PutFlags.Partial,
				_offset = offset,
				_length = -1
			};
		}

		/// <summary>
		/// Creates a <see cref="GetOptions"/> representing partial data access.
		/// </summary>
		/// <param name="offset">The offset location for partial data access.</param>
		/// <param name="length">The length for partial data access.</param>
		/// <returns>The created <see cref="GetOptions"/>.</returns>
		public static PutOptions Partial(int offset, int length)
		{
			return new PutOptions
			{
				_flags = PutFlags.Partial,
				_offset = offset,
				_length = length
			};
		}

		/// <summary>
		/// Converts an <see cref="Int32"/> to a partial data access instance.
		/// </summary>
		/// <param name="offset">The offset location for partial data access.</param>
		/// <returns>The created <see cref="PutOptions"/>.</returns>
		public static implicit operator PutOptions(int offset)
		{
			return Partial(offset);
		}
	}
}
