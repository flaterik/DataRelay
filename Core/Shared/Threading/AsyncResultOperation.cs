using System;
using System.Collections.Generic;
using System.Threading;

namespace MySpace.Common.Threading
{
	/// <summary>
	/// This is a specific implementation of <see cref="Operation"/> that allows for
	/// IAsyncResult pattern used by many Microsoft APIs, such as 
	/// HttpWebRequest.BeginGetRequestStream() / HttpWebRequest.EndGetRequestStream().
	/// </summary>
	public sealed class AsyncResultOperation : Operation
	{
		/// <summary>
		/// <para>The <see cref="IAsyncResult"/> that contains the result of the asynchrous task.</para>
		/// </summary>
		/// <value>The IAsyncResult captured from the "Begin" aynchronous operation call. </value>
		public IAsyncResult AsyncResult { get; internal set; }

		/// <summary>
		/// internal constructor to prevent public construction of this type.
		/// Use <see cref="Operation.Create(System.IAsyncResult,System.TimeSpan)"/> to
		/// instantiate one.
		/// </summary>
		internal AsyncResultOperation() { }

		/// <summary>
		/// OnCompletion is called by the Operation base class when the execution plan has been
		/// stoped.  This implementation closes the wait handle used for the timeout timer, if 
		/// one was ever created.
		/// </summary>
		protected override void OnCompletion()
		{
			if (!AsyncResult.CompletedSynchronously)
			{
				AsyncResult.AsyncWaitHandle.Close();
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
			if (AsyncResult.CompletedSynchronously)
			{
				// we have already completed so no need to start a timer, just use the completion callback now.
				completionOrTimeoutCallback(operationEnumerator, false);
			}
			else
			{
				CompletedSynchronously = false;
				ThreadPool.RegisterWaitForSingleObject(AsyncResult.AsyncWaitHandle, completionOrTimeoutCallback, operationEnumerator, Timeout, true);
			}
		}
	}
}