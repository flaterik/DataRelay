using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Repository;

namespace MySpace.Logging
{
	/// <summary>
	/// A thread safe, log appender that passes the logged event data to an event
	/// handler. It is intended for use in unit tests to test log output. The
	/// application configuration file must declare this appender, for example as
	/// follows:
	/// 
	/// &lt;log4net&gt;
	///	&lt;appender name=&quot;DelegatedAppender&quot; type=&quot;MySpace.Logging.DelegatedAppender, MySpace.Logging&quot;&gt;
	///	&lt;/appender&gt;
	///	&lt;root&gt;
	///		&lt;level value=&quot;ALL&quot; /&gt;
	///		&lt;appender-ref ref=&quot;DelegatedAppender&quot;/&gt;
	///	&lt;/root&gt;
	/// &lt;/log4net&gt;
	/// 
	/// Logged events are placed in a queue (FIFO).
	/// </summary>
	public class DelegatedAppender : AppenderSkeleton
	{
		private readonly object _lockObject = new object();

		private EventHandler<LoggingEventArgs> _logged;

		/// <summary>
		/// Occurs when a <see cref="LoggingEvent"/> is sent to this instance.
		/// </summary>
		public event EventHandler<LoggingEventArgs> Logged
		{
			add
			{
				lock(_lockObject)
				{
					_logged += value;
				}
			}
			remove
			{
				lock(_lockObject)
				{
					_logged -= value;
				}
			}
		}

		/// <summary>
		/// Sets the handler for <see cref="Logged"/> to be only
		/// <paramref name="handler"/>. Useful for clearing out all previous
		/// handlers set via event accessors.
		/// </summary>
		/// <param name="handler">The <see cref="EventHandler{LoggingEventArgs}"/>
		/// that will handle <see cref="Logged"/>.</param>
		public void SetLoggedHandler(EventHandler<LoggingEventArgs> handler)
		{
			lock(_lockObject)
			{
				_logged = handler;
			}
		}

		/// <summary>
		/// Overriden. Performs actual logging of an event.
		/// </summary>
		/// <param name="loggingEvent">The <see cref="LoggingEvent"/> to append.</param>
		protected override void Append(LoggingEvent loggingEvent)
		{
			lock (_lockObject)
			{
				if (_logged != null)
				{
					_logged(this, new LoggingEventArgs(loggingEvent));
				}
			}
		}

		/// <summary>
		/// Overriden. Performs actual logging of a bulk array of events.
		/// </summary>
		/// <param name="loggingEvents">The <see cref="LoggingEvent"/> array to append.</param>
		protected override void Append(LoggingEvent[] loggingEvents)
		{
			lock (_lockObject)
			{
				if (_logged != null)
				{
					foreach (var loggingEvent in loggingEvents)
					{
						_logged(this, new LoggingEventArgs(loggingEvent));
					}
				}
			}
		}

		/// <summary>
		/// Gets the first configured <see cref="DelegatedAppender"/> by
		/// iterating over all <see cref="ILoggerRepository"/>s then their
		/// <see cref="IAppender"/>s.
		/// </summary>
		/// <returns>The first configured <see cref="DelegatedAppender"/>,
		/// <see langword="null"/> if no <see cref="DelegatedAppender"/>
		/// configured.</returns>
		public static DelegatedAppender GetAppender()
		{
			// get log4net config objects
			new LogWrapper(); // Needed to initialize log4net
			foreach (var repository in LogManager.GetAllRepositories())
			{
				foreach (var appender in repository.GetAppenders())
				{
					var delegatedAppender = appender as DelegatedAppender;
					if (delegatedAppender != null) return delegatedAppender;
				}
			}
			return null;
		}

		/// <summary>
		/// Creates a scope that attaches an event handler delegate to this
		/// instance, and removes that delegate on disposal. Intended for use
		/// with <see langword="using"/>.
		/// </summary>
		/// <param name="handler">The <see cref="EventHandler{LoggingEventArgs}"/>
		/// to be attached, cannot be <see langword="null"/>.</param>
		/// <returns>An <see cref="IDisposable"/> that detaches <paramref name="handler"/>
		/// when <see cref="IDisposable.Dispose"/> is called on it.</returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="handler"/> is <see langword="null"/>.</para>
		/// </exception>
		public IDisposable CreateScope(EventHandler<LoggingEventArgs> handler)
		{
			if (handler == null)
				throw new ArgumentNullException("handler");
			return new DelegateScope(this, handler);
		}

		/// <summary>
		/// Creates a scope that adds this instance's logged events to a collection
		/// of events, and stops adding logged events on disposal. Intended for use
		/// with <see langword="using"/>.
		/// </summary>
		/// <param name="eventCollection">The <see cref="ICollection{LoggingEventArgs}"/>
		/// to which these logged events are added, cannot be <see langword="null"/>.</param>
		/// <returns>An <see cref="IDisposable"/> that stops the addition of
		/// logged events to <paramref name="eventCollection"/> when
		/// <see cref="IDisposable.Dispose"/> is called on it.</returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="eventCollection"/> is <see langword="null"/>.</para>
		/// </exception>
		public IDisposable CreateScope(ICollection<LoggingEventArgs> eventCollection)
		{
			if (eventCollection == null)
				throw new ArgumentNullException("eventCollection");
			return CreateScope((sender, args) => eventCollection.Add(args));
		}

		private class DelegateScope : IDisposable
		{
			private readonly DelegatedAppender _appender;
			private EventHandler<LoggingEventArgs> _handler;

			public DelegateScope(DelegatedAppender appender,
				EventHandler<LoggingEventArgs> handler)
			{
				_appender = appender;
				_handler = handler;
				_appender.Logged += _handler;
			}

			#region IDisposable Members

			public void Dispose()
			{
				_appender.Logged -= _handler;
			}

			#endregion
		}
	}
}
