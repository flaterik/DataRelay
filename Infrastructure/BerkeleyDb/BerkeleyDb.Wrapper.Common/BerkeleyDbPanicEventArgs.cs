using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BerkeleyDbWrapper
{
	/// <summary>
	/// Provides data for the <see cref="Environment.PanicCall"/> event.
	/// </summary>
	public class BerkeleyDbPanicEventArgs : EventArgs
	{
		/// <summary>
		/// Gets the error prefix.
		/// </summary>
		/// <value>The <see cref="String"/> error prefix used for logging the panic call.</value>
		public string ErrorPrefix { get; private set; }
		/// <summary>
		/// Gets the error message.
		/// </summary>
		/// <value>The <see cref="String"/> error message.</value>
		public string Message { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="BerkeleyDbPanicEventArgs"/> class.
		/// </summary>
		/// <param name="errorPrefix">The error prefix.</param>
		/// <param name="message">The error message.</param>
		public BerkeleyDbPanicEventArgs(string errorPrefix, string message)
		{
			ErrorPrefix = errorPrefix;
			Message = message;
		}
	}
}
