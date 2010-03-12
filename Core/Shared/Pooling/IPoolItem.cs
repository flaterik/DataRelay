using System;

namespace MySpace.Common
{
	/// <summary>
	/// 	<para>An item belonging to a <see cref="Pool{T}"/> instance.</para>
	/// </summary>
	/// <typeparam name="T">
	///	<para>The type of item encapsulated by this class.</para>
	/// </typeparam>
	public interface IPoolItem<T> : IDisposable
	{
		/// <summary>
		/// 	<para>Gets the owning <see cref="Pool{T}"/> instance.</para>
		/// </summary>
		/// <value>
		/// 	<para>The owning <see cref="Pool{T}"/> instance.</para>
		/// </value>
		Pool<T> Owner { get; }

		/// <summary>
		/// 	<para>Gets the item.</para>
		/// </summary>
		/// <value>
		/// 	<para>The item.</para>
		/// </value>
		T Item { get; }

		/// <summary>
		/// 	<para>Gets or sets a value indicating whether this instance is corrupted
		/// 	and should be removed from the owning <see cref="Pool{T}"/> instance.</para>
		/// </summary>
		/// <value>
		/// 	<para><see langword="true"/> if this instance is corrupted and should be removed
		/// 	from the owning <see cref="Pool{T}"/> instance; otherwise, <see langword="false"/>.</para>
		/// </value>
		bool IsCorrupted { get; set; }
	}
}
