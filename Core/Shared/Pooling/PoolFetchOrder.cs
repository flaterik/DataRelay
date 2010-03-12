using System;

namespace MySpace.Common
{
	/// <summary>
	///	<para>The order in which items are retrieved from <see cref="Pool{T}"/> instances.</para>
	/// </summary>
	public enum PoolFetchOrder
	{
		/// <summary>
		///	<para>Use a first in first out algorithm and works like a queue.</para>
		/// </summary>
		Fifo,
		/// <summary>
		///	<para>Use a last in first out algorithm and works like a stack.</para>
		/// </summary>
		Lifo
	}
}
