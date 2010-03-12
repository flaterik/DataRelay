using System;

namespace MySpace.Common
{
	/// <summary>
	///	<para>Encapsulates the current phase of <see cref="IPoolItem{T}"/> instances.</para>
	/// </summary>
	public enum PoolItemPhase
	{
		/// <summary>
		///	<para>The <see cref="IPoolItem{T}"/> is leaving the <see cref="Pool{T}"/>.</para>
		/// </summary>
		Leaving,
		/// <summary>
		///	<para>The <see cref="IPoolItem{T}"/> is returning to the <see cref="Pool{T}"/>.</para>
		/// </summary>
		Returning
	}
}
