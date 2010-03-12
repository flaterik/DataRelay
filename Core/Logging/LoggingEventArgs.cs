using System;
using System.Collections.Generic;
using System.Text;
using log4net.Core;

namespace MySpace.Logging
{
	/// <summary>
	/// Provides data for handlers of <see cref="DelegatedAppender.Logged"/>.
	/// </summary>
	public class LoggingEventArgs : EventArgs
	{
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="LoggingEventArgs"/> class.</para>
		/// </summary>
		/// <param name="evt">
		/// 	<para>The event logged to the instance of
		///		<see cref="DelegatedAppender"/>. Can be <see langword="null"/> since
		///		log4net doesn't guarantee that the logged event isn't
		///		<see langword="null"/>.</para>
		/// </param>
		public LoggingEventArgs(LoggingEvent evt)
		{
			Event = evt;
		}

		/// <summary>
		/// Gets the <see cref="LoggingEvent"/> sent to the
		/// <see cref="DelegatedAppender"/>.
		/// </summary>
		public LoggingEvent Event { get; private set; }
	}
}
