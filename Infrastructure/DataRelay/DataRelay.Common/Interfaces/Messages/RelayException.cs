using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySpace.DataRelay
{
	/// <summary>
	/// The exception that is thrown to encapsulate error conditions enumerated by
	/// <see cref="RelayErrorType"/>.
	/// </summary>
	public class RelayException : Exception
	{
		/// <summary>
		/// Gets the type of error condition.
		/// </summary>
		/// <value>The <see cref="RelayErrorType"/> encapsulated by this instance.</value>
		public RelayErrorType Type { get; private set; }

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="RelayException"/> class.</para>
		/// </summary>
		/// <param name="type">
		/// 	<para>The encapsulated <see cref="RelayErrorType"/>.</para>
		/// </param>
		public RelayException(RelayErrorType type) :
			base(string.Format("{0} condition occurred.", type))
		{
			Type = type;
		}
	}
}
