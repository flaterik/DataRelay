using System;
using System.Threading;
using MySpace.Common;

namespace MySpace.DataRelay.Transports
{
	/// <summary>
	/// 	<para>A simple <see cref="IAsyncResult"/> implementation that
	/// 	more complex implementations may derive from.</para>
	/// </summary>
	internal class SimpleAsyncResult : IAsyncResult
	{
		/// <summary>
		///	<para>Indicates the current state of the async operation.</para>
		/// </summary>
		private enum State
		{
			/// <summary>
			///	<para>The operation is incomplete.</para>
			/// </summary>
			Incomplete,
			/// <summary>
			///	<para>The operation completed synchronously.</para>
			/// </summary>
			CompletedSync,
			/// <summary>
			///	<para>The operation completed asynchronously.</para>
			/// </summary>
			CompletedAsync
		}

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="SimpleAsyncResult"/> class.</para>
		/// </summary>
		/// <param name="callback">The callback. <see langword="null"/> if not available.</param>
		/// <param name="asyncState">
		///	<para>A user-defined state object that will be returned by <see cref="IAsyncResult.AsyncState"/>.</para>
		/// </param>
		public SimpleAsyncResult(AsyncCallback callback, object asyncState)
		{
			_callback = callback;
			_asyncState = asyncState;
		}

		private readonly AsyncCallback _callback;
		private readonly object _asyncState;
		private readonly MonitorWaitHandle _handle = new MonitorWaitHandle(false, EventResetMode.ManualReset);
		private State _state = State.Incomplete;

		/// <summary>
		/// 	<para>Gets or sets an error that occurred durring the asynchronous operation.</para>
		/// </summary>
		/// <value>
		/// 	<para>The error that occurred durring the asynchronous operation. <see langword="null"/>
		/// 	if no error occurred.</para>
		/// </value>
		public Exception Error { get; set; }

		/// <summary>
		///	<para>Completes the operation and invokes the callback, if a callback exists.</para>
		/// </summary>
		/// <param name="wasSynchronous">
		///	<para><see langword="true"/> if the operation completed synchronously;
		///	<see langword="false"/> otherwise.</para>
		/// </param>
		public void CompleteOperation(bool wasSynchronous)
		{
			_state = wasSynchronous ? State.CompletedSync : State.CompletedAsync;
			Thread.MemoryBarrier();
			_handle.Set();
			if (_callback != null) _callback(this);
		}

		#region IAsyncResult Members

		object IAsyncResult.AsyncState
		{
			get { return _asyncState; }
		}

		WaitHandle IAsyncResult.AsyncWaitHandle
		{
			get { return _handle; }
		}

		bool IAsyncResult.CompletedSynchronously
		{
			get { return _state == State.CompletedSync; }
		}

		bool IAsyncResult.IsCompleted
		{
			get { return _state != State.Incomplete; }
		}

		#endregion
	}
}
