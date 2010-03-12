using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.Common.Storage
{
	/// <summary>
	/// Delimits a section of a <see cref="StringBuilder"/>.
	/// </summary>
	public struct StringBuilderSegment : IEquatable<StringBuilderSegment>
	{
		public StringBuilder StringBuilder { get; private set; }

		public int Offset { get; private set; }

		public int Count { get; private set; }

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="StringBuilderSegment"/> structure.</para>
		/// </summary>
		/// <param name="builder">
		/// 	<para>The <see cref="StringBuilder"/> to delimit.</para>
		/// </param>
		/// <param name="offset">
		/// 	<para>The <see cref="Int32"/> position of the start of the delimited range.</para>
		/// </param>
		/// <param name="count">
		/// 	<para>The <see cref="Int32"/> length of the delimited range.</para>
		/// </param>
		public StringBuilderSegment(StringBuilder builder, int offset, int count) : this()
		{
			if (builder == null) throw new ArgumentNullException("builder");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset");
			if (count < 0) throw new ArgumentOutOfRangeException("count");
			StringBuilder = builder;
			Offset = offset;
			Count = count;
		}

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="StringBuilderSegment"/> structure
		///		delmited from the position 0 to the current length of
		///		<paramref name="builder"/>.</para>
		/// </summary>
		/// <param name="builder">
		/// 	<para>The <see cref="StringBuilder"/> to delimit.</para>
		/// </param>
		public StringBuilderSegment(StringBuilder builder) : this()
		{
			if (builder == null) throw new ArgumentNullException("builder");
			StringBuilder = builder;
			Offset = 0;
			Count = builder.Length;			
		}

		/// <summary>
		/// 	<para>Overriden. Returns the fully qualified type name of this instance.</para>
		/// </summary>
		/// <returns>
		/// 	<para>A <see cref="System.String"/> containing a fully qualified type name.</para>
		/// </returns>
		/// <filterpriority>
		/// 	<para>2</para>
		/// </filterpriority>
		public override string ToString()
		{
			if (StringBuilder == null) return string.Empty;
			return StringBuilder.ToString(Offset, Count);
		}

		/// <summary>
		/// 	<para>Indicates whether the current object is equal to another object of the same type.</para>
		/// </summary>
		/// <returns>
		/// 	<para>true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.</para>
		/// </returns>
		/// <param name="other">
		/// 	<para>An object to compare with this object.</para>
		/// </param>
		public bool Equals(StringBuilderSegment other)
		{
			return StringBuilder == other.StringBuilder &&
				Offset == other.Offset &&
				Count == other.Count;
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
			if (!(obj is StringBuilderSegment)) return false;
			return Equals((StringBuilderSegment) obj);
		}

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
			var hash = StringBuilder != null ? StringBuilder.GetHashCode() : 0;
			if (hash < 0)
			{
				hash <<= 1;
				++hash;
			} else
			{
				hash <<= 1;				
			}
			hash ^= Offset;
			if (hash < 0)
			{
				hash <<= 1;
				++hash;
			}
			else
			{
				hash <<= 1;
			}
			return hash ^ Count;
		}
	}
}
