using System;

namespace MySpace.Common
{
	/// <summary>
	/// 	<para>An <see cref="EventArgs"/> implementation with data commonly
	/// 	associated with asynchronous operations.</para>
	/// </summary>
	public class AsyncEventArgs : EventArgs
	{
		/// <summary>
		///	<para>An instance of <see cref="AsyncEventArgs"/> with
		///	<see cref="CompletedSynchronously"/> set to <see langword="true"/>.</para>
		/// </summary>
		public static readonly AsyncEventArgs Synchronous = new AsyncEventArgs(true);

		/// <summary>
		///	<para>An instance of <see cref="AsyncEventArgs"/> with
		///	<see cref="CompletedSynchronously"/> set to <see langword="false"/>.</para>
		/// </summary>
		public static AsyncEventArgs Asynchronous = new AsyncEventArgs(false);

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="AsyncEventArgs"/> class.</para>
		/// </summary>
		/// <param name="completedSynchronously">
		///	<para><see langword="true"/> if the event was invoked synchronously
		///	on the subscibing thread; <see langword="false"/> otherwise.</para>
		/// </param>
		public AsyncEventArgs(bool completedSynchronously)
		{
			CompletedSynchronously = completedSynchronously;
		}

		/// <summary>
		/// 	<para>Gets or sets a value indicating whether the event fired synchronously on the subscriber thread.</para>
		/// </summary>
		/// <value>
		/// 	<para><see langword="true"/> if the event was invoked synchronously
		///	on the subscibing thread; <see langword="false"/> otherwise.</para>
		/// </value>
		public bool CompletedSynchronously { get; protected set; }
	}
}
