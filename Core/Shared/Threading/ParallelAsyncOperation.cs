using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace MySpace.Common.Threading
{
	/// <summary>
	/// This implementation of <see cref="Operation"/> supports the parallel execution of
	/// several execution plans.
	/// </summary>
	public sealed class ParallelAsyncOperation : Operation
	{
		private readonly Operation[] _headOperations;
		private readonly int _numberOfOperations;
		private readonly AsynchronousExecutionPlan[] _executionPlans;
		private ManualResetEvent _handle;
		private bool _childTimedout;

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="ParallelAsyncOperation"/> class.</para>
		/// </summary>
		/// <param name="headOperations">
		/// 	<para>The operations to be executed in parallel, generated from
		///		<paramref name="executionPlans"/>.</para>
		/// </param>
		/// <param name="executionPlans">
		/// 	<para>The plans to be executed in parallel.</para>
		/// </param>
		internal ParallelAsyncOperation(Operation[] headOperations,
			AsynchronousExecutionPlan[] executionPlans)
		{
			_headOperations = headOperations;
			_executionPlans = executionPlans;
			_numberOfOperations = executionPlans.Length;
		}

		/// <summary>
		/// 	<para>Overriden. OnCompletion is called by the Operation base class when the execution plan has been stoped.  Derived classes should implement this method to dispose their resources, if any.</para>
		/// </summary>
		protected override void OnCompletion()
		{
			_handle.Close();
			foreach(var headOperation in _headOperations)
			{
				if (!headOperation.Completed)
				{
					headOperation.SetCompleted();
				}
			}
		}

		/// <summary>
		/// Perform the completion or timeout previously passed in <see cref="Yield"/>.
		/// </summary>
		/// <param name="timedout">Whether the process timedout.</param>
		internal void DoCompletionOrTimeoutCallback(bool timedout)
		{
			_childTimedout = timedout;
			_handle.Set();
		}

		/// <summary>
		/// To be called in the execution plan after yield returning the instance.
		/// </summary>
		/// <remarks>Necessary to throw any exception from the parallel execution,
		/// so it reported up to the top level exception.</remarks>
		public void End()
		{
			if (AsynchronousException != null)
			{
				throw AsynchronousException;
			}
		}

		/// <summary>
		/// 	<para>Overriden. Yield is called when the <see cref="Operation"/> wants to end the current thread because an asynchronous operation is in progress.  The derived class should implement this function that will call completionOrTimeoutCallback after the asynchronous operation completes or times out.</para>
		/// </summary>
		/// <param name="completionOrTimeoutCallback">
		/// 	<para>The completion or timeout callback.</para>
		/// </param>
		/// <param name="operationEnumerator">
		/// 	<para>The operation enumerator.</para>
		/// </param>
		protected internal override void Yield(WaitOrTimerCallback completionOrTimeoutCallback, IEnumerator<Operation> operationEnumerator)
		{
			// launch the parallel operations
			_handle = new ManualResetEvent(false);
			for (var idx = 0; idx < _numberOfOperations; ++idx)
			{
				var headOperation = _headOperations[idx];
				var operationSteps = _executionPlans[idx](headOperation);
				headOperation.QueueNextStep(operationSteps.GetEnumerator());
			}
			ThreadPool.RegisterWaitForSingleObject(_handle, (s, t) =>
				completionOrTimeoutCallback(s, _childTimedout || t), operationEnumerator, Timeout, true);
		}
	}
}
