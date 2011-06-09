using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace MySpace.Common.Threading
{
	/// <summary>
	/// This is a specific implementation of <see cref="Operation"/> that allows for
	/// the execute/async-callback pattern used by MySpace Infrastructure's Hydrator
	/// and Microsoft's SmtpClient.
	/// Parent <see cref="Operation"/> objects are always of type ExecuteAsyncOperation.
	/// </summary>
	public sealed class ExecuteAsyncOperation : Operation
	{
		private ManualResetEvent _handle;
		private bool _completed;
		private object _syncRoot = new object();

		/// <summary>
		/// internal constructor to prevent public construction of this type.
		/// Use <see cref="Operation.Create(System.Action{System.Action},System.TimeSpan)"/> to
		/// instantiate one.
		/// </summary>
		internal ExecuteAsyncOperation() {}

		/// <summary>
		/// Set() stops the timeout timer used, and will release any threads waiting
		/// to call the completionOrTimeoutCallback.
		/// </summary>
		internal void Set()
		{
			lock (_syncRoot)
			{
				_completed = true;
				if (_handle != null) _handle.Set();
			}
		}

		/// <summary>
		/// OnCompletion is called by the Operation base class when the execution plan has been
		/// stoped.  This implementation closes the wait handle used for the timeout timer.
		/// </summary>
		protected override void OnCompletion()
		{
			lock (_syncRoot)
			{
				if (_handle != null)
				{
					if(!_completed) _handle.Set();
					_handle.Close();
					_handle = null;
				}
				_completed = true;
			}

		}

		/// <summary>
		/// 	<para>Overriden. Yield is called when the Operation wants to end the current thread because an 
		/// 	asynchronous operation is in progress.  
		/// 	Implements a timer that will call completionOrTimeoutCallback after the asynchronous operation completes or times out.</para>
		/// </summary>
		/// <param name="completionOrTimeoutCallback">
		/// 	<para>The completion or timeout callback.</para>
		/// </param>
		/// <param name="operationEnumerator">
		/// 	<para>The operation enumerator.</para>
		/// </param>
		protected override internal void Yield(WaitOrTimerCallback completionOrTimeoutCallback, IEnumerator<Operation> operationEnumerator)
		{
			bool callTheDelegateSynchronously = true;
			lock (_syncRoot)
			{
				if (!_completed)
				{
					_handle = new ManualResetEvent(false);
					callTheDelegateSynchronously = false;
					CompletedSynchronously = false;
					ThreadPool.RegisterWaitForSingleObject(_handle, completionOrTimeoutCallback, operationEnumerator, Timeout, true);
				}
			}

			// don't call a delegate from inside a lock.
			if (callTheDelegateSynchronously)
			{
				// the operation has completed before yield, so it was completed synchronously.
				completionOrTimeoutCallback(operationEnumerator, false);
			}
		}
	}
}