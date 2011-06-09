using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySpace.Logging
{
	/// <summary>
	///		Provides formatted logging for the Universal Message Bus.
	/// </summary>
	public static class MessageBusTracing
	{
		private static LogWrapper _log = new LogWrapper();

		/// <summary>
		/// 	<para>Logs an event in the life cycle of a piece of data processed by the Universal Message Bus.</para>
		/// </summary>
		/// <param name="eventName">
		/// 	<para>The name of the event in the data life cycle.</para>
		/// </param>
		/// <param name="tags">
		/// 	<para>The configured tags of the logged event.  There must be at least one tag.</para>
		/// </param>
		/// <param name="additionalInformation">
		/// 	<para>Key-value pairs providing additional information of the event.  Optional.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="tags"/> is <see langword="null"/>, empty, or one of its elements is null or empty.</para>
		/// 	<para>-or-</para>
		/// 	<para>The argument <paramref name="eventName"/> is <see langword="null"/>.</para>
		/// </exception>
		public static void LogLifeCycleEvent(string eventName, string[] tags, params KeyValuePair<string, string>[] additionalInformation)
		{
			if (tags == null) throw new ArgumentNullException("tags");
			if (tags.Length == 0) throw new ArgumentNullException("tags");
			if (eventName == null) throw new ArgumentNullException("eventName");

			var builder = new StringBuilder("Tags=");

			for (var i = 0; i < tags.Length; i++)
			{
				var tag = tags[i];

				if (String.IsNullOrEmpty(tag)) throw new ArgumentNullException("tags");

				if (i != 0)
				{
					builder.Append(";");
				}

				builder.Append(tag);
			}

			builder.AppendLine();

			builder.Append("Event=" + eventName);

			builder.AppendLine();

			builder.Append("MachineName=" + Environment.MachineName);

			if (additionalInformation != null)
			{
				foreach (var pair in additionalInformation)
				{
					builder.AppendLine();
					builder.Append(pair.Key);
					builder.Append("=");
					builder.Append(pair.Value);
				}
			}

			_log.Info(builder.ToString());
		}
	}
}
