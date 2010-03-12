using System;
using System.Diagnostics;

namespace MySpace.SocketTransport
{
	/// <summary>
	/// 	<para>An <see cref="EventArgs"/> implementation for asyncrhonous operations.</para>
	/// </summary>
	public abstract class AsyncEventArgs : EventArgs, ICompletion
	{
		private bool _completed;

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="AsyncEventArgs"/> class.</para>
		/// </summary>
		protected AsyncEventArgs() { }

		/// <summary>
		/// 	<para>Gets a value indicating whether the async operation completed synchronously.</para>
		/// </summary>
		/// <value>
		/// 	<para><see langword="true"/> if the operation completed synchronously; otherwise, <see langword="false"/>.</para>
		/// </value>
		public bool CompletedSynchronously { get; internal set; }

		/// <summary>
		/// 	<para>Gets the error if there was one; otherwise gets <see langword="null"/>.</para>
		/// </summary>
		/// <value>
		/// 	<para>The error, if one occurred; <see langword="null"/> otherwise.</para>
		/// </value>
		public Exception Error { get; internal set; }

		#region ICompletion Members

		/// <summary>
		/// Completes the operation.
		/// </summary>
		void ICompletion.Complete()
		{
			if (!_completed)
			{
				_completed = true;
				PerformCompletion();
			}
			else
			{
				Debug.Fail("This shouldn't get called more than once.");
			}
		}

		/// <summary>
		/// Called when the operation completes.
		/// </summary>
		protected internal abstract void PerformCompletion();

		#endregion
	}
}
