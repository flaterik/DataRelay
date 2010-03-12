using System;
using System.Xml.Serialization;

namespace MySpace.Common
{
	/// <summary>
	/// 	<para>Configuration settings for <see cref="Pool{T}"/> instances.</para>
	/// </summary>
	[XmlRoot("Pool", Namespace = "http://myspace.com/PoolConfig.xsd")]
	public class PoolConfig
	{
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="PoolConfig"/> class.</para>
		/// </summary>
		public PoolConfig()
		{
			FetchOrder = PoolFetchOrder.Lifo; // stack-like
			LoanCapacity = 0; // unlimited
			PoolCapacity = 0; // unlimited
			MaxUses = 0; // unlimited
			MaxLifespan = 0; // unlimited
			FinalizeLeaks = false;
		}

		/// <summary>
		/// 	<para>Gets or sets the fetch order for the pool. This can be either
		/// 	<see cref="PoolFetchOrder.Fifo"/> or <see cref="PoolFetchOrder.Lifo"/>.
		/// 	<see cref="PoolFetchOrder.Lifo"/> is recommended for items that
		/// 	make use of large amounts of managed memory because items in
		/// 	lower generations are more likely to be discarded than those in higher
		/// 	generations.</para>
		/// </summary>
		/// <value>
		/// 	<para>The fetch order for the pool.
		/// 	This can be either <see cref="PoolFetchOrder.Fifo"/>
		/// 	or <see cref="PoolFetchOrder.Lifo"/>. The default
		/// 	is <see cref="PoolFetchOrder.Lifo"/>.</para>
		/// </value>
		[XmlElement("FetchOrder")]
		public PoolFetchOrder FetchOrder { get; set; }

		/// <summary>
		/// 	<para>Gets or sets the maximum number of items that can be
		/// 	loaned out to consumers at one time. If a consumer attempt
		/// 	to borrow an item from the pool and the loan capacity is saturated
		/// 	the invoking thread will block until items are returned. If less than
		/// 	or equal to zero an unlimited number of items can be loaned to
		/// 	consumers at once. The default is zero (unlimited).</para>
		/// </summary>
		/// <value>
		/// 	<para>The maximum number of items that can be loaned out to
		/// 	consumers at one time. If less than or equal to zero an unlimited
		/// 	number of items can be loaned to consumers at once. The default
		/// 	is zero (unlimited).</para>
		/// </value>
		[XmlElement("LoanCapacity")]
		public int LoanCapacity { get; set; }

		/// <summary>
		/// 	<para>Gets or sets the maximum number of items the pool can hold.
		/// 	If items are returned to the pool but the pool is already holding a
		/// 	number of items equal to the max count the item will be discarded.
		/// 	If a number less than or equal to zero the pool will store an unlimited
		/// 	number of items.</para>
		/// </summary>
		/// <value>
		/// 	<para>The maximum number of items the pool can hold.
		/// 	 If less than or equal to zero the pool will store an unlimited
		/// 	 number of items. The default is zero (unlimited).</para>
		/// </value>
		[XmlElement("PoolCapacity")]
		public int PoolCapacity { get; set; }

		/// <summary>
		/// 	<para>Gets or sets the maximum lifespan of each item in seconds.
		/// 	If less than or equal to zero the pool will allow the item to exist
		/// 	for an unlimited duration.</para>
		/// </summary>
		/// <value>
		/// 	<para>The maximum lifespan of each item in seconds.
		/// 	If less than or equal to zero the pool will allow the item to exist
		/// 	for an unlimited duration. The default is zero (unlimited).</para>
		/// </value>
		[XmlElement("MaxLifespan")]
		public int MaxLifespan { get; set; }

		/// <summary>
		/// 	<para>Gets or sets the maximum number of times an item
		/// 	in the pool can be used before it is removed and destroyed.
		/// 	If less than or equal to zero the max number of uses is unlimited.</para>
		/// </summary>
		/// <value>
		/// 	<para>The maximum number of times an item
		/// 	in the pool can be used before it is removed and destroyed.
		/// 	If less than or equal to zero the max number of uses is unlimited.
		/// 	 The default is zero (unlimited).</para>
		/// </value>
		[XmlElement("MaxUses")]
		public int MaxUses { get; set; }

		/// <summary>
		/// 	<para>Gets or sets a value that indicates whether or not to finalize
		/// 	leaks. A leak is when an item is not returned to the pool as expected
		/// 	and could potentially lead to deadlocks if a maximum
		/// 	<see cref="LoanCapacity"/> is set. If <see cref="LoanCapacity"/>
		/// 	is unlimited this option will be ignored.</para>
		/// </summary>
		/// <value>
		/// 	<para><see langword="true"/> to finalize leaks;
		/// 	<see langword="false"/> otherwise.</para>
		/// </value>
		[XmlElement("FinalizeLeaks")]
		public bool FinalizeLeaks { get; set; }
	}
}
