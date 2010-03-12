using System;
using System.Threading;
using MySpace.Common;

namespace MySpace.DataRelay
{
	/// <summary>
	/// Represents a base class for <see cref="IAsyncResult"/>.  DO NOT USE 
	/// <see cref="WaitHandle.WaitAll(WaitHandle[])"/> or <see cref="WaitHandle.WaitAny(WaitHandle[])"/>
	/// on <see cref="AsyncWaitHandle"/>.
	/// </summary>
	internal abstract class BaseAsyncResult : IAsyncResult
	{
		private readonly object _asyncState = null;
		private readonly AsyncCallback _callback;
		private CallState _callState = CallState.Incomplete;
		private readonly MonitorWaitHandle _handle = new MonitorWaitHandle(false, EventResetMode.ManualReset);

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="SocketHandlerAsyncResult"/> class.</para>
		/// </summary>
		/// <param name="asyncState">The client's state. Never <see langword="null"/>.</param>
		/// <param name="callback">The callback. May be <see langword="null"/>.</param>
		protected BaseAsyncResult(object asyncState, AsyncCallback callback)
		{
			if (callback == null) throw new ArgumentNullException("callback");
			_callback = callback;
			_asyncState = asyncState;
		}

		/// <summary>
		/// Gets or sets the exception that was thrown during begin.
		/// </summary>
		/// <value>The exception.</value>
		internal Exception Exception { get; set; }

		/// <summary>
		///	<para>Indicates the current state of the async operation.</para>
		/// </summary>
		protected enum CallState
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

		object IAsyncResult.AsyncState
		{
			get { return _asyncState; }
		}

		WaitHandle IAsyncResult.AsyncWaitHandle
		{
			get { return _handle;}
		}

		bool IAsyncResult.CompletedSynchronously
		{
			get { return _callState == CallState.CompletedSync; }
		}

		bool IAsyncResult.IsCompleted
		{
			get { return _callState != CallState.Incomplete; }
		}

		/// <summary>
		///	<para>Completes the operation and invokes the callback, if a callback exists.</para>
		/// </summary>
		/// <param name="wasSynchronous">
		///	<para><see langword="true"/> if the operation completed synchronously;
		///	<see langword="false"/> otherwise.</para>
		/// </param>
		public void CompleteOperation(bool wasSynchronous)
		{
			_callState = wasSynchronous ? CallState.CompletedSync : CallState.CompletedAsync;
			Thread.MemoryBarrier();
			_handle.Set();
			_callback(this);
		}
	}
}
