using System;
using System.IO;
using MySpace.Common;

namespace MySpace.SocketTransport
{
	/// <summary>
	/// 	<para>An <see cref="AsyncEventArgs"/> implementation for two-way socket operations.</para>
	/// </summary>
	public sealed class RoundTripAsyncEventArgs : AsyncEventArgs
	{
		private Action<RoundTripAsyncEventArgs> _completionAction;
		private IPoolItem<MemoryStream> _response;
		private bool _responseDisposed;

		/// <summary>
		/// 	<para>Creates a new instance of the <see cref="RoundTripAsyncEventArgs"/> class.</para>
		/// </summary>
		/// <param name="completedSynchronously">
		/// 	<para><see langword="true"/> if the operation completed synchronously; otherwise, <see langword="false"/>.</para>
		/// </param>
		/// <param name="error">
		/// 	<para>Gets the error if there was one; otherwise gets <see langword="null"/>.</para>
		/// </param>
		/// <param name="response">The response data.</param>
		/// <param name="completionAction">The completion action that will be called when <see cref="ICompletion.Complete"/> is called.</param>
		/// <returns>
		///	<para>A new instance of the <see cref="RoundTripAsyncEventArgs"/> class.</para>
		/// </returns>
		internal static RoundTripAsyncEventArgs Create(
			bool completedSynchronously,
			Exception error,
			IPoolItem<MemoryStream> response,
			Action<RoundTripAsyncEventArgs> completionAction)
		{
			return new RoundTripAsyncEventArgs
			{
				CompletedSynchronously = completedSynchronously,
				Error = error,
				_response = response,
				_completionAction = completionAction
			};
		}

		/// <summary>
		/// 	<para>Gets the response stream that was sent from the server.</para>
		/// </summary>
		/// <value>
		/// 	<para>The response stream that was sent from the server.
		/// 	<see langword="null"/> if the response was empty.</para>
		/// </value>
		public Stream Response
		{
			get
			{
				if (_responseDisposed)
				{
					throw new InvalidOperationException("Response may not be accessed outside the callback method.");
				}
				return _response == null ? null : _response.Item;
			}
		}

		/// <summary>
		/// Completes the operation.
		/// </summary>
		protected internal override void  PerformCompletion()
		{
			try
			{
				if (_completionAction != null) _completionAction(this);
			}
			finally
			{
				_responseDisposed = true;
				if (_response != null)
				{
					_response.Dispose();
					_response = null;
				}
			}
		}
	}
}
