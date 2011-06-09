using System;

namespace MySpace.Common.Dynamic
{
	/// <summary>
	/// 	<para>Encapsulates a label that can be defined, referenced, and marked by <see cref="IMsilWriter"/> implementations.</para>
	/// </summary>
	public interface ILabel
	{
		/// <summary>
		/// 	<para>Marks this label at the current location of the <see cref="IMsilWriter"/> in which this instance was defined.</para>
		/// </summary>
		void Mark();

		/// <summary>
		/// 	<para>Gets a value indicating whether this instance has been referenced by any branch instructions.</para>
		/// </summary>
		/// <value>
		/// 	<para><see langword="true"/> if this instance has been referenced; otherwise, <see langword="false"/>.</para>
		/// </value>
		bool IsReferenced { get; }

		/// <summary>
		/// 	<para>Gets a value indicating whether this instance has been marked.</para>
		/// </summary>
		/// <value>
		/// 	<para><see langword="true"/> if this instance has been marked; otherwise, <see langword="false"/>.</para>
		/// </value>
		bool IsMarked { get; }
	}
}
