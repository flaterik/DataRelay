using System;
using System.Threading;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// 	<para>A simple <see cref="IAsyncResult"/> implementation for synchronous completion.</para>
	/// </summary>
	internal class SynchronousAsyncResult : IAsyncResult
	{
		private static readonly SynchronousAsyncResult _resultWithNullState = new SynchronousAsyncResult(null);

		/// <summary>
		/// Creates a synchronous async result and immeadiatly completes it.
		/// </summary>
		/// <param name="callback">The callback to invoke.</param>
		/// <param name="asyncState">A user-defined state object.</param>
		/// <returns>The new synchronous async reuslt object.</returns>
		public static SynchronousAsyncResult CreateAndComplete(AsyncCallback callback, object asyncState)
		{
			var result = asyncState == null ? _resultWithNullState : new SynchronousAsyncResult(asyncState);
			if (callback != null) callback(result);
			return result;
		}

		private readonly object _asyncState;

		protected SynchronousAsyncResult(object asyncState)
		{
			_asyncState = asyncState;
		}

		#region IAsyncResult Members

		object IAsyncResult.AsyncState
		{
			get { return _asyncState; }
		}

		WaitHandle IAsyncResult.AsyncWaitHandle
		{
			get { throw new NotSupportedException(); }
		}

		bool IAsyncResult.CompletedSynchronously
		{
			get { return true; }
		}

		bool IAsyncResult.IsCompleted
		{
			get { return true; }
		}

		#endregion
	}
}
