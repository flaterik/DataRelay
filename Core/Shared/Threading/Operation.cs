using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using MySpace.Common.HelperObjects;
using Action=System.Action;

namespace MySpace.Common.Threading
{
	/// <summary>
	/// 	<para>Operation is a framework to simiply expressing asynchronous code into a single function (referred to as an execution plan.</para>
	/// 	<para>See the wiki: <see href="https://mywiki.corp.myspace.com/index.php/MySpace.Common.Threading.Operation"/></para>
	/// 	<para>Each asynchronous code segment ends with a <c>yield return</c>, <c>yield break</c>, or end of the 
	/// 	execution plan function.</para>
	/// 	<para>yield return; means that the thread ends.  A new thread will pick up at the statement following <c>yield return</c>
	/// 	when the asynchronous operation completes.</para>
	/// 	<para><c>yield break;</c> (or the end of your execution plan) will stop the asynchronous execution and fire the completion
	/// 	delegate specified in the call to either <see cref="Start(MySpace.Common.Threading.AsynchronousExecutionPlan,System.Action{MySpace.Common.Threading.Operation})"/>
	/// 	or <see cref="Start(MySpace.Common.Threading.AsynchronousExecutionPlan,System.Action{MySpace.Common.Threading.Operation},System.Action,System.Action)"/>.</para>
	/// </summary>
	public abstract class Operation
	{
		#region private fields

		private Exception _exception;
		private bool _completed;
		private bool _completedSynchronously = true;
		private Action _contextSwitchEntryDelegate;
		private Action _contextSwitchExitDelegate;
		private Action<Operation> _completionCallback;
		[ThreadStatic]
		private static int _synchronousRecursiveDepth;

		#endregion 

		/// <summary>
		/// <para>Gets a value indicating whether this <see cref="Operation"/> timed out.</para>
		/// </summary>
		/// <value>
		/// <see langword="true"/> if the operation, or any of its child operations timed out.  Otherwise, <see langword="false"/>.
		/// </value>
		public bool TimedOut { get; private set; }

		/// <summary>
		/// Gets an exception if one occured during an asynchronous execution context.  If no exception occurred, the value will be null.
		/// </summary>
		/// <value>The exception.  It may be <see langword="null"/> if there were no exceptions caught during the execution of the operation.</value>
		public Exception AsynchronousException { get { return _exception; } }

		/// <summary>
		/// Gets a value indicating whether the operation was completed synchronously.
		/// </summary>
		/// <value>
		/// 	<see langword="true"/> if completed synchronously; otherwise, <see langword="false"/>.
		/// </value>
		public bool CompletedSynchronously
		{
			get { return _completed && _completedSynchronously; }
			internal set { _completedSynchronously = value; }
		}

		/// <summary>
		/// Gets a value indicating whether the operation was completed.
		/// </summary>
		/// <value>
		/// 	<see langword="true"/> if completed; otherwise, <see langword="false"/>.
		/// </value>
		protected internal bool Completed { get { return _completed; } }

		/// <summary>
		/// 	<para>Starts execution of your operation, which is a sequence of asynchronous steps, identified by a <see cref="AsynchronousExecutionPlan"/> delegate object. The timeout of this operation indicates one of the operations created by the exection plan timed out, which stops the operation whenever the timeout occurred.</para>
		/// </summary>
		/// <example> This example shows how to start an execution plan.
		/// <code>
		/// var waitHandle = new ManualResetEvent(false);
		/// try
		/// {
		///     Operation operation = Operation.Start(MyExecutionPlan, op => waitHandle.Set(), MyContextEntryDelegate, MyContextExitDelegate);
		///     waitHandle.WaitOne(TimeSpan.FromSeconds(5)); // wait 5 seconds for the execution plan to complete
		/// }
		/// finally
		/// {
		///     waitHandle.Close();
		/// }
		/// </code>
		/// </example>
		/// <param name="executionPlan">
		/// 	<para>The delegate object, that is the simplified approach to expressing a chain of asynchronous steps. This parameter cannot be null.</para>
		/// </param>
		/// <param name="doneCallback">
		/// 	<para>The callback that the operation will call when the operation completes.  doneCallback cannot be null.</para>
		/// </param>
		/// <param name="contextSwitchEntryDelegate">
		/// 	<para>The context switch entry delegate.  Whenever the operation may change threads, this delegate will be called before executing your code.  Must be non-null if the exit delegate is non-null.</para>
		/// </param>
		/// <param name="contextSwitchExitDelegate">
		/// 	<para>The context switch exit delegate. Whenever the operation may change threads, this delegate will be called after executing your code. Must be non-null if the entry delegate is non-null.</para>
		/// </param>
		/// <returns>
		/// 	<para>An operation that manages the asynchrnous execution and timeouts of the opeartions from Create().</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="executionPlan"/> is <see langword="null"/>.</para>
		/// 	<para>-or-</para>
		/// 	<para>The argument <paramref name="doneCallback"/> is <see langword="null"/>.</para>
		/// 	<para>-or-</para>
		/// 	<para>The argument <paramref name="contextSwitchEntryDelegate"/> is <see langword="null"/> while the argument <paramref name="contextSwitchExitDelegate"/> was not null.</para>
		/// 	<para>-or-</para>
		/// 	<para>The argument <paramref name="contextSwitchExitDelegate"/> is <see langword="null"/> while the argument <paramref name="contextSwitchEntryDelegate"/> was not null.</para>
		/// </exception>
		public static Operation Start(AsynchronousExecutionPlan executionPlan, Action<Operation> doneCallback, Action contextSwitchEntryDelegate, Action contextSwitchExitDelegate)
		{
			if (executionPlan == null) throw new ArgumentNullException("executionPlan");
			if (doneCallback == null) throw new ArgumentNullException("doneCallback");
			if (contextSwitchEntryDelegate == null && contextSwitchExitDelegate != null) throw new ArgumentNullException("contextSwitchEntryDelegate");
			if (contextSwitchEntryDelegate != null && contextSwitchExitDelegate == null) throw new ArgumentNullException("contextSwitchExitDelegate");

			var headOperation = CreateStartOperation(doneCallback,
				contextSwitchEntryDelegate, contextSwitchExitDelegate);
			var operationSteps = executionPlan(headOperation);
			headOperation.BeginNextStep(operationSteps.GetEnumerator());
			return headOperation;
		}

		private static Operation CreateStartOperation(Action<Operation> doneCallback,
			Action contextSwitchEntryDelegate, Action contextSwitchExitDelegate)
		{
			return new ExecuteAsyncOperation
			{
				_completionCallback = doneCallback,
				_contextSwitchEntryDelegate = contextSwitchEntryDelegate,
				_contextSwitchExitDelegate = contextSwitchExitDelegate
			};			
		}

//#if DEBUG
//        // to be used only for unit tests
//        internal bool _throwInParallelProcessing = false;
//#endif
	
		/// <summary>
		/// 	<para>Starts the parallel execution of multiple operations, each of
		///		which is a sequence of asynchronous steps, identified by a
		///		<see cref="AsynchronousExecutionPlan"/> delegate object. The
		///		timeout of this operation is the maximum time for all the
		///		operations to complete.</para>
		/// </summary>
		/// <example> This example shows how to use.
		/// <code>
		/// IEnumerable&lt;Operation&gt; TopLevelPlan(Operation op)
		/// {
		///		var opParallel = op.CreateParallel(TimeSpan.FromSeconds(20),
		///			Plan1, Plan2, Plan3);
		///		yield return opParallel;
		///		opParallel.End();
		/// }
		/// </code>
		/// </example>
		/// <param name="timeout">The timeout.</param>
		/// <param name="executionPlans">
		/// 	<para>The execution plans to execute in parallel. Cannot be null
		///		or empty.</para>
		/// </param>
		/// <returns>
		/// 	<para>An operation that manages the asynchronous execution of several
		///		execution plans in parallel.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="executionPlans"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// 	<para>The argument <paramref name="executionPlans"/> is empty.</para>
		/// </exception>
		public ParallelAsyncOperation CreateParallel(TimeSpan timeout, params AsynchronousExecutionPlan[] executionPlans)
		{
			if (executionPlans == null) throw new ArgumentNullException("executionPlans");
			if (executionPlans.Length == 0) throw new ArgumentOutOfRangeException("executionPlans");

			var len = executionPlans.Length;
			var headOperations = new Operation[len];
			var ret = new ParallelAsyncOperation(headOperations, executionPlans)
			{
				Timeout = timeout,
				_contextSwitchEntryDelegate = _contextSwitchEntryDelegate,
				_contextSwitchExitDelegate = _contextSwitchExitDelegate
			};
			var remaining = len;
			for (var idx = 0; idx < len; ++idx)
			{
				var i = idx;
				headOperations[i] = CreateStartOperation(op =>
					{
						try
						{
							if (remaining <= 0 || ret.Completed) return;
							bool? timedOut = null;
//#if DEBUG
//                            if (_throwInParallelProcessing)
//                            {
//                                throw new ApplicationException("Unit test");
//                            }
//#endif
							var exception = op.AsynchronousException;
							if (exception != null)
							{
								ret.HandleCaughtException(exception);
								timedOut = false;
							}
							else if (op.TimedOut)
							{
								timedOut = true;
							}
							if (timedOut.HasValue)
							{
								remaining = 0;
							}
							else
							{
								if (Interlocked.Decrement(ref remaining) == 0)
								{
									timedOut = false;
								}
							}
							if (timedOut.HasValue)
							{
								ret.DoCompletionOrTimeoutCallback(timedOut.Value);
							}
						} catch(Exception exc)
						{
							ret.HandleCaughtException(exc);
							for(var jdx = 0; jdx < len; ++jdx)
							{
								var headOperation = headOperations[jdx];
								if (!headOperation.Completed)
								{
									headOperation.SetCompleted();
								}
							}
							ret.DoCompletionOrTimeoutCallback(false);
						}
					},
					_contextSwitchEntryDelegate,
					_contextSwitchExitDelegate);
			}

			return ret;
		}

		/// <summary>
		/// Starts execution of your operation, which is a sequence of asynchronous steps, identified by a <see cref="AsynchronousExecutionPlan"/> delegate object.
		/// The timeout of this operation indicates one of the operations created by the exection plan timed out, which stops the
		/// operation whenever the timeout occurred.
		/// </summary>
		/// <example> This example shows how to start an execution plan.
		/// 	<code>
		/// 	var waitHandle = new ManualResetEvent(false);
		/// 	try
		/// 	{
		/// 	    Operation operation = Operation.Start(MyExecutionPlan, op => waitHandle.Set());
		/// 	    waitHandle.WaitOne(TimeSpan.FromSeconds(5)); // wait 5 seconds for the execution plan to complete
		/// 	}
		/// 	finally
		/// 	{
		/// 	    waitHandle.Close();
		/// 	}
		/// 	</code>
		/// </example>
		/// <param name="executionPlan">The delegate object, that is the simplified approach to expressing a chain of asynchronous steps. This parameter cannot be null.</param>
		/// <param name="doneCallback">The callback that the operation will call when the operation completes.  doneCallback cannot be null.</param>
		/// <returns>
		/// An <see cref="Operation"/> that manages the asynchrnous execution and timeouts of the opeartions from Create().
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="executionPlan"/> is <see langword="null"/>.</para>
		/// 	<para>-or-</para>
		/// 	<para>The argument <paramref name="doneCallback"/> is <see langword="null"/>.</para>
		/// </exception>
		public static Operation Start(AsynchronousExecutionPlan executionPlan, Action<Operation> doneCallback)
		{
			return Start(executionPlan, doneCallback, null, null);
		}

		/// <summary>
		/// 	<para>Creates an asynchronous operation which will complete or timeout.  Use <c>yield return</c> to return the thread to 
		/// the pool and then resume when the task has completed or timed out.</para>
		/// 	<para>You should keep the return value to access the <see cref="IAsyncResult"/> from your asynchrouns task,
		/// to pass into your End* completion function.</para>
		/// 	<para>When your code resumes execution after "yield return", the <see cref="Operation"/> object will indicate if the task has
		/// completed (allowing you to dispatch many tasks at once and determine which have completed) and if it completed due to a timeout.</para>
		/// </summary>
		/// <param name="asyncResult">
		/// 	<para>The result of a Begin* style async call that starts an asynchronous task.</para>
		/// </param>
		/// <param name="timeout">
		/// 	<para>A <see cref="TimeSpan"/> value for when the <see cref="Operation"/> will be timed out if it hasn't completed.</para>
		/// </param>
		/// <returns>
		/// 	<para>An <see cref="AsyncResultOperation"/>, which you need to <c>yield return</c>.  This value contains asynchronous state information about the dispatched task, 
		/// and should be used to obtain the correct <see cref="IAsyncResult"/> to pass into your End* function, which would follow the <c>yield return</c>.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="asyncResult"/> is <see langword="null"/>.</para>
		/// </exception>
		public AsyncResultOperation Create(IAsyncResult asyncResult, TimeSpan timeout)
		{
			if(asyncResult == null) throw new ArgumentNullException("asyncResult");
			return new AsyncResultOperation {AsyncResult = asyncResult, Timeout = timeout};
		}

		/// <summary>
		/// Creates an operation that will complete or timeout.  The delegate passed in is required 
		/// and is will be called when the <see cref="Action"/> completes.
		/// </summary>
		/// <exception cref="ArgumentNullException"><para>The argument <paramref name="executingActionWithCompletionCallback"/> is <see langword="null"/>.</para></exception>
		/// <param name="executingActionWithCompletionCallback">The execute action with completion callback.</param>
		/// <param name="timeout">The timeout.</param>
		/// <returns>
		/// <para>An <see cref="ExecuteAsyncOperation"/>, which you need to <c>yield return</c>.  This value contains asynchronous state information about the dispatched task.
		/// Execution will resume at the statement following the <c>yield return</c> after the action has completed or a timeout occurs.</para>
		/// </returns>
		public ExecuteAsyncOperation Create(Action<Action> executingActionWithCompletionCallback, TimeSpan timeout)
		{
			if(executingActionWithCompletionCallback == null) throw new ArgumentNullException("executingActionWithCompletionCallback");

			var operation = new ExecuteAsyncOperation { Timeout = timeout };

			executingActionWithCompletionCallback(
				() =>
				{
					// this callback will be called by executeActionWithCompletionCallback()
					// when it has finished.  
					try
					{
						operation.Set();  // this will prevent timeout, and call the competion method to get executed.
					}
					catch (Exception e)
					{
						HandleCaughtException(e);
					}
				});

			return operation;
		}

		/// <summary>
		/// Creates an operation that will complete or timeout.  The delegate passed in is required 
		/// and is will be called when the Action completes.
		/// </summary>
		/// <param name="timeout">The timeout.</param>
		/// <returns>
		/// <para>An <see cref="ExecuteAsyncOperation"/>, which you need to "yield return".  This value contains asynchronous state information about the dispatched task.
		/// Execution will resume at the statement following the "yield return" after the action has completed or a timeout occurs.</para>
		/// </returns>
		public ExecuteAsyncOperation Create(TimeSpan timeout)
		{
			var operation = new ExecuteAsyncOperation { Timeout = timeout };
			return operation;
		}

		/// <summary>
		/// Sets the state of the operation to Completed, which will invoke completion delegates and end the operation.
		/// This is useful for the asynchronous model that SmtpClient requires.
		/// </summary>
		/// <example>
		/// <code>
		/// private static IEnumerable{Operation} SmtpAsyncTest(Operation operation)
		/// {
		/// 	var smtp = new System.Net.Mail.SmtpClient();
		/// 	var op = operation.Create(TimeSpan.FromSeconds(20));
		/// 	
		/// 	// Call Operation.SetCompleted() from the SMTP SendCompleted event handler:
		/// 	smtp.SendCompleted += (sender, e) => op.SetCompleted();
		/// 	
		/// 	smtp.SendAsync("pkinkade@myspace-inc.com", "kinkadep@gmail.com", "Sending from Operation", "Hi!", null);
		/// 	yield return op;
		/// }
		/// </code>
		/// </example>
		public void SetCompleted()
		{
			SetCompleted(false);
		}

		/// <summary>
		/// Sets the state of the opeartion to Completed and Timed Out, which will invoke completion delgates
		/// and end the opearation (which will have <see cref="TimedOut"/> set to <see langword="true"/>).
		/// </summary>
		public void SetTimedOut()
		{
			SetCompleted(true);
		}

		#region abstract methods implemented by derived classes

		/// <summary>
		/// Yield is called when the <see cref="Operation"/> wants to end the current thread because an asynchronous operation
		/// is in progress.  The derived class should implement this function that will call completionOrTimeoutCallback
		/// after the asynchronous operation completes or times out.
		/// </summary>
		/// <param name="completionOrTimeoutCallback">The completion or timeout callback.</param>
		/// <param name="operationEnumerator">The operation enumerator.</param>
		protected abstract internal void Yield(WaitOrTimerCallback completionOrTimeoutCallback, IEnumerator<Operation> operationEnumerator);

		/// <summary>
		/// OnCompletion is called by the Operation base class when the execution plan has been
		/// stoped.  Derived classes should implement this method to dispose their resources, if any.
		/// </summary>
		protected abstract void OnCompletion();

		#endregion 

		#region internal class state

		private void SetCompleted(bool timedOut)
		{
			TimedOut = timedOut;
			if (_completed==false)
			{
				_completed = true;
				OnCompletion();
			}
		}

		/// <summary>
		/// Gets or sets the <see cref="TimeSpan"/> timeout for this instance.
		/// </summary>
		internal TimeSpan Timeout { get; set; }

		#endregion

		#region private state management methods

		private void CompletionOrTimeoutCallback(object state, bool timedOutStatus)
		{
			var operationEnumerator = (IEnumerator<Operation>)state;
			var operation = operationEnumerator.Current;
			// if any sub-operation doesn't complete synchronously, the overall flag is cleared.
			if (!operation._completedSynchronously) CompletedSynchronously = false;

			if (!timedOutStatus)
			{
				operation.SetCompleted();
				BeginNextStep(operationEnumerator);
			}
			else
			{
				operation.SetTimedOut();
				StopExecutionPlan(operationEnumerator, true);
			}

			// Any exceptions that may be present here are serious enough to halt the process because
			// would indicate a flaw in the framework.
		}

		private void StopExecutionPlan(IDisposable operationEnumerator, bool timedOutStatus)
		{
			try
			{
				if (timedOutStatus)
				{
					SetTimedOut();
				}
				else
				{
					SetCompleted();
				}

				operationEnumerator.Dispose();
			}
			finally
			{
				CompletionCallback();
			}
		}

		private void CompletionCallback()
		{
				EnterThreadContextState();
			try
			{
				_completionCallback(this);
			}
			finally
			{
				ExitThreadContextState();
			}
		}

		private void HandleCaughtException(Exception e)
		{
			// only capture the first exception that occurs.
			// I don't think subsequent exceptions would be important,
			// but if so, we could build a container of subsquent ones, or only log them here.
			if (_exception == null)
			{
				_exception = e;
			}
		}

		/// <summary>
		/// Schedules the next step of an operation enumeration.
		/// </summary>
		/// <param name="operationEnumerator">The operation enumeration.</param>
		internal protected void QueueNextStep(IEnumerator<Operation> operationEnumerator)
		{
			ThreadPool.UnsafeQueueUserWorkItem(o => BeginNextStep((operationEnumerator)), null);			
		}

		private void BeginNextStep(IEnumerator<Operation> operationEnumerator)
		{
			if (_synchronousRecursiveDepth > 100)
			{
				try
				{
					// Stack Overflow Protection:
					// prevent stack depth on synchronous recursion from going deeper than 10
					// without scheduling to a new thread.
					QueueNextStep(operationEnumerator);
				}
				catch (Exception e)
				{
					HandleCaughtException(e);
					StopExecutionPlan(operationEnumerator, false);
				}
			}
			else
			{
				try
				{
					++_synchronousRecursiveDepth;

					if (MoveNext(operationEnumerator))
					{
						var operation = operationEnumerator.Current;
						if (operation.Equals(this))
						{
							throw new InvalidOperationException("An Operation cannot yield return itself.");
						}

						operation.Yield(CompletionOrTimeoutCallback, operationEnumerator);
					}
					else
					{
						StopExecutionPlan(operationEnumerator, false);
						return;
					}
				}
				catch (Exception e)
				{
					HandleCaughtException(e);
					StopExecutionPlan(operationEnumerator, false);
				}
				finally
				{
					--_synchronousRecursiveDepth;
					Debug.Assert(_synchronousRecursiveDepth >= 0);
				}
			}
		}

		private bool MoveNext(IEnumerator operationEnumerator)
		{
			EnterThreadContextState();
			try
		{
				return operationEnumerator.MoveNext();
		}
			finally
			{
				ExitThreadContextState();
			}
		}

		private void EnterThreadContextState()
		{
			if (_contextSwitchEntryDelegate != null)
			{
				try
				{
					_contextSwitchEntryDelegate();
				}
				catch (Exception e)
				{
					HandleCaughtException(e);
				}
			}
		}

		private void ExitThreadContextState()
		{
			if (_contextSwitchExitDelegate != null)
			{
				try
				{
					_contextSwitchExitDelegate();
				}
				catch (Exception e)
				{
					HandleCaughtException(e);
				}
			}
		}

		#endregion 
	}
}
