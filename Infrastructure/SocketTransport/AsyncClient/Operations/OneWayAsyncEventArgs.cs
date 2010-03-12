using System;

namespace MySpace.SocketTransport
{
	/// <summary>
	/// 	<para>An <see cref="AsyncEventArgs"/> implementation for one-way socket operations.</para>
	/// </summary>
	public sealed class OneWayAsyncEventArgs : AsyncEventArgs
	{
		private Action<OneWayAsyncEventArgs> _completionAction;

		/// <summary>
		/// 	<para>Creates a new instance of the <see cref="OneWayAsyncEventArgs"/> class.</para>
		/// </summary>
		/// <param name="completedSynchronously">
		/// 	<para><see langword="true"/> if the operation completed synchronously; otherwise, <see langword="false"/>.</para>
		/// </param>
		/// <param name="error">
		/// 	<para>Gets the error if there was one; otherwise gets <see langword="null"/>.</para>
		/// </param>
		/// <param name="completionAction">The completion action that will be called when <see cref="ICompletion.Complete"/> is called.</param>
		/// <returns>
		///	<para>A new instance of the <see cref="OneWayAsyncEventArgs"/> class.</para>
		/// </returns>
		internal static OneWayAsyncEventArgs Create(
			bool completedSynchronously,
			Exception error,
			Action<OneWayAsyncEventArgs> completionAction)
		{
			return new OneWayAsyncEventArgs
			{
				CompletedSynchronously = completedSynchronously,
				Error = error,
				_completionAction = completionAction
			};
		}

		private OneWayAsyncEventArgs()
		{
		}

		/// <summary>
		/// Completes the operation.
		/// </summary>
		protected internal override void  PerformCompletion()
		{
			if(_completionAction != null) _completionAction(this);
		}
	}
}
