using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BerkeleyDbWrapper
{
	/// <summary>
	/// Provides data for the <see cref="Environment.MessageCall"/> event.
	/// </summary>
	public class BerkeleyDbMessageEventArgs : EventArgs
	{
		/// <summary>
		/// Gets the message being sent.
		/// </summary>
		/// <value>The <see cref="String"/> message being sent.</value>
		public string Message { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="BerkeleyDbMessageEventArgs"/> class.
		/// </summary>
		/// <param name="message">The message.</param>
		public BerkeleyDbMessageEventArgs(string message)
		{
			Message = message;
		}
	}
}
