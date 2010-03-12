using System;

namespace MySpace.DataRelay
{
	/// <summary>
	/// Represents a <see cref="IAsyncResult"/> for <see cref="RelayNode"/> and <see cref="IAsyncDataHandler"/>.
	/// </summary>
	internal class RelayMessageAsyncResult : BaseAsyncResult
	{
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="RelayMessageAsyncResult"/> class.</para>
		/// </summary>
		/// <param name="message">The <see cref="RelayMessage"/>. Never <see langword="null"/>.</param>
		/// <param name="state">The caller's state.</param>
		/// <param name="callback">The callback. Never <see langword="null"/>.</param>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="message"/> is null or <see cref="callback"/> is null.
		/// </exception>
		public RelayMessageAsyncResult(RelayMessage message, object state, AsyncCallback callback) : base(state, callback)
		{
			if (message == null) throw new ArgumentNullException("message");
			Message = message;
		}

		/// <summary>
		/// Gets the <see cref="RelayMessage"/> to process.
		/// </summary>
		/// <value>The message; can't be null.</value>
		public RelayMessage Message { get; private set; }
	}
}
