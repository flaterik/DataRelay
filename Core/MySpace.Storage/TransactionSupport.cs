using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySpace.Storage
{
	/// <summary>
	/// Describes what transactionality the <see cref="IObjectStorage"/> supports.
	/// </summary>
	public enum TransactionSupport
	{
		/// <summary>
		/// Does not support transactions.
		/// </summary>
		None = 0,

		/// <summary>
		/// Supports non-durable transactions.
		/// </summary>
		Volatile = 1,

		/// <summary>
		/// Supports durable transactions.
		/// </summary>
		Durable = 2
	}
}
