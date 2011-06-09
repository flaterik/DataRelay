using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BerkeleyDbWrapper
{
	/// <summary>
	/// The exception that is thrown when an error occurs in Berkeley Db.
	/// </summary>
	public class BdbException : ApplicationException
	{
		/// <summary>
		/// Gets the error code.
		/// </summary>
		/// <value>The <see cref="Int32"/> Berkeley Db error code.</value>
		public int Code { get; private set; }
		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="BdbException"/> has been handled.
		/// </summary>
		/// <value>
		/// 	<see langword="true"/> if handled; otherwise, <see langword="false"/>.
		/// </value>
		public bool Handled { get; set; }
		/// <summary>
		/// Initializes a new instance of the <see cref="BdbException"/> class.
		/// </summary>
		/// <param name="code">The error code.</param>
		/// <param name="message">The error message.</param>
		public BdbException(int code, string message) : base(message)
		{
			Code = code;
		}
	}
}
