using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using MySpace.Common.Threading;

namespace MySpace.Common
{
	/// <summary>
	/// An operation style method that completes a Future instance.
	/// </summary>
	public delegate IEnumerable<Operation> ExecuteFutureOperation<T>(Operation operation, FuturePublisher<T> futureBuilder);

	/// <summary>
	/// Encapsulates something that will complete some time in the future.
	/// </summary>
	public abstract class Future : IFuture, IFutureReaction
	{
		private static readonly Future _complete = (Future<int>)0;

		public static implicit operator Future(Exception error)
		{
			var result = new Future<int>();
			result.SetError(error);
			return result;
		}

		/// <summary>
		/// Gets a completed future.
		/// </summary>
		/// <value>A completed future.</value>
		public static Future Complete
		{
			get { return _complete; }
		}

		/// <summary>
		/// Creates a <see cref="Future"/> instance given APM begin and end methods.
		/// </summary>
		/// <param name="begin">The begin call.</param>
		/// <param name="end">The end call.</param>
		/// <returns>
		///	A <see cref="Future"/> instance that completes when the asynchronous operation completes.
		/// </returns>
		public static Future FromAsyncPattern(Action<AsyncCallback> begin, Action<IAsyncResult> end)
		{
			return FromAsyncPattern(begin, ar => { end(ar); return 0; });
		}

		/// <summary>
		/// Creates a <see cref="Future{T}"/> instance given APM begin and end methods.
		/// </summary>
		/// <param name="begin">The begin call.</param>
		/// <param name="end">The end call.</param>
		/// <returns>
		///	A <see cref="Future{T}"/> instance that completes when the asynchronous operation completes.
		/// </returns>
		public static Future<T> FromAsyncPattern<T>(Action<AsyncCallback> begin, Func<IAsyncResult, T> end)
		{
			var future = new Future<T>();

			try
			{
				begin(ar =>
				{
					T value;
					try
					{
						value = end(ar);
					}
					catch (Exception ex)
					{
						future.SetError(ex);
						return;
					}
					future.SetResult(value);
				});
			}
			catch (Exception ex)
			{
				future.SetError(ex);
			}

			return future;
		}

		public static Future<T> FromIterator<T>(Func<FuturePublisher<T>, IEnumerable<Future>> iteratorPattern, bool stopOnFutureErrors)
		{
			var publisher = new FuturePublisher<T>();
			var result = publisher.Future;

			var iteratorFuture = iteratorPattern(publisher)
				.ExecuteSequentially(stopOnFutureErrors);

			iteratorFuture.OnComplete(() =>
			{
			    if(result.IsComplete) return;

				var error = iteratorFuture.HasError
					? iteratorFuture.Error
					: new InvalidOperationException("The iterator pattern completed without publishing a result.");

			    result.SetError(error);
			});

			return result;
		}

		protected readonly object SyncRoot = new object();

		protected volatile FutureStatus _state;
		protected Exception _error;
		protected Action _onComplete;
		protected int _waiterCount;
		private bool _stackTracePreserved;

		protected Future()
		{
		}

		protected Exception GetErrorForRethrow()
		{
			if (_stackTracePreserved)
			{
				Thread.MemoryBarrier();
				return _error;
			}

			lock (SyncRoot)
			{
				if (_stackTracePreserved)
				{
					return _error;
				}

				if (_error == null)
				{
					throw new InvalidOperationException("GetErrorForRethrow may only be called if the future has completed successfully with an error.");
				}
				_error.PrepareForRethrow();
				Thread.MemoryBarrier();
				_stackTracePreserved = true;
			}

			return _error;
		}

		/// <summary>
		/// Waits for the future to complete.
		/// </summary>
		public void Wait()
		{
			if (IsComplete) return;

			lock (SyncRoot)
			{
				while (!IsComplete)
				{
					++_waiterCount;
					Monitor.Wait(SyncRoot);
					--_waiterCount;
				}
			}
		}

		/// <summary>
		/// Gets the current state. Does not block.
		/// </summary>
		/// <value>The current state.</value>
		public FutureStatus Status
		{
			get { return _state; }
		}

		/// <summary>
		/// Gets a value indicating whether this instance is complete.
		/// </summary>
		/// <value>
		/// 	<see langword="true"/> if this instance is complete; otherwise, <see langword="false"/>.
		/// </value>
		public bool IsComplete
		{
			get { return _state != FutureStatus.Incomplete; }
		}

		/// <summary>
		/// Gets a value indicating whether this instance was canceled by a consumer.
		/// </summary>
		/// <value>
		/// 	<see langword="true"/> if this instance was canceled by a consumer; otherwise, <see langword="false"/>.
		/// </value>
		public bool IsCanceled
		{
			get { return _state == FutureStatus.Canceled; }
		}

		/// <summary>
		/// Gets a value indicating whether this instance has an error. Will block if this instance is not yet complete.
		/// </summary>
		/// <value>
		/// 	<see langword="true"/> if this instance has an error; otherwise, <see langword="false"/>.
		/// </value>
		public bool HasError
		{
			get
			{
				Wait();
				return _state == FutureStatus.Failure;
			}
		}

		/// <summary>
		/// Gets a value indicating whether this instance has a result. Will block if this instance is not yet complete.
		/// </summary>
		/// <value>
		/// 	<see langword="true"/> if this instance has a result; otherwise, <see langword="false"/>.
		/// </value>
		public bool HasResult
		{
			get
			{
				Wait();
				return _state == FutureStatus.Success;
			}
		}

		/// <summary>
		/// Gets the error. Will block if this instance is not yet complete.
		/// </summary>
		/// <value>The error.</value>
		public Exception Error
		{
			get
			{
				Wait();
				return HasError ? _error : null;
			}
		}

		/// <summary>
		/// Executes the specified reaction when the future completes
		/// or executes the reaction immediately if the future has already completed.
		/// </summary>
		/// <param name="reaction">The reaction.</param>
		[Obsolete]
		public void ReactWith(IFutureReaction reaction)
		{
			ArgumentAssert.IsNotNull(reaction, "reaction");

			OnComplete(() =>
			{
				if (HasError)
				{
					reaction.OnError(Error);
				}
				else
				{
					reaction.OnCompleted();
				}
			});
		}

		/// <summary>
		/// Instructs the future to react with the specified error action.
		/// </summary>
		/// <param name="onError">The on error reaction.</param>
		/// <returns>This future.</returns>
		public Future OnError(Action<Exception> onError)
		{
			return (Future)OnComplete(() => { if (HasError) onError(Error); });
		}

		IFuture IFuture.OnComplete(Action onComplete)
		{
			return OnComplete(onComplete);
		}

		/// <summary>
		/// Instructs the future to react with the specified action when the future completes successfully or otherwise.
		/// </summary>
		/// <param name="onComplete">The on complete reaction.</param>
		/// <returns>This future.</returns>
		public Future OnComplete(Action onComplete)
		{
			if (!IsComplete)
			{
				lock (SyncRoot)
				{
					if (!IsComplete)
					{
						// note - it turns out that this is quite a bit faster than just using _onComplete += onComplete;
						if (_onComplete == null)
						{
							_onComplete = onComplete;
						}
						else
						{
							var a = _onComplete;
							var b = onComplete;
							_onComplete = () => { try { a(); } finally { b(); } };
						}
						onComplete = null;
					}
				}
			}
			if (onComplete != null)
			{
				onComplete();
			}
			return this;
		}

		/// <summary>
		/// Instructs the future to react with the specified action when the future completes successfully.
		/// </summary>
		/// <param name="onSuccess">The on success reaction.</param>
		/// <returns>This future.</returns>
		public Future OnSuccess(Action onSuccess)
		{
			OnComplete(() => { if (Status == FutureStatus.Success) onSuccess(); });
			return this;
		}

		/// <summary>
		/// Instructs the future to execute the specified action when a consumer successfully cancels the operation.
		/// </summary>
		/// <param name="onCancel">The <see cref="Action"/> to run when the future is successfully canceled.</param>
		/// <returns>This future.</returns>
		public Future OnCancel(Action onCancel)
		{
			return (Future)OnComplete(() => { if (IsCanceled) onCancel(); });
		}

		protected abstract bool CompleteFromBase(FutureStatus state, Exception error);

		/// <summary>
		/// Attempts to cancel the operation. Returns <see langword="true"/> if successful or <see langword="false"/> otherwise.
		/// A cancelation attempt will typically fail if the future completes before <see cref="Cancel"/> is called.
		/// </summary>
		/// <returns><see langword="true"/> if successful or <see langword="false"/> otherwise.</returns>
		public bool Cancel()
		{
			return CompleteFromBase(FutureStatus.Canceled, null);
		}

		[Obsolete]
		void IFutureReaction.OnError(Exception error)
		{
			CompleteFromBase(FutureStatus.Failure, error);
		}

		[Obsolete]
		void IFutureReaction.OnCompleted()
		{
			CompleteFromBase(FutureStatus.Success, null);
		}
	}

	/// <summary>
	/// Encapsulates a value that will be computed at some point in the future.
	/// </summary>
	/// <typeparam name="T">The type of value.</typeparam>
	public class Future<T> : Future, IFutureReaction<T>, IFuture<T>
	{
		/// <summary>
		/// Performs an implicit conversion from <see cref="Future{T}"/> to <typeparamref name="T"/>.
		/// </summary>
		/// <param name="future">The future.</param>
		/// <returns>The result of the conversion.</returns>
		public static implicit operator T(Future<T> future)
		{
			ArgumentAssert.IsNotNull(future, "future");

			return future.Result;
		}

		/// <summary>
		/// Performs an implicit conversion from <typeparamref name="T"/> to <see cref="Future{T}"/>.
		/// </summary>
		/// <param name="value">The value.</param>
		/// <returns>The result of the conversion.</returns>
		public static implicit operator Future<T>(T value)
		{
			var result = new Future<T>();
			result.SetResult(value);
			return result;
		}

		/// <summary>
		/// Performs an implicit conversion from <see cref="System.Exception"/> to <see cref="Future{T}"/>.
		/// </summary>
		/// <param name="error">The error.</param>
		/// <returns>The result of the conversion.</returns>
		public static implicit operator Future<T>(Exception error)
		{
			var result = new Future<T>();
			result.SetError(error);
			return result;
		}

		/// <summary>
		/// Creates a <see cref="Future{T}"/> instance given an iterator pattern that uses the <see cref="Operation"/> class.
		/// </summary>
		/// <param name="operationPattern">The operation's iterator pattern.</param>
		/// <returns>
		///		<para>A <see cref="Future{T}"/> instance that completes when the operation completes.</para>
		/// </returns>
		public static Future<T> FromOperationPattern(ExecuteFutureOperation<T> operationPattern)
		{
			var publisher = new FuturePublisher<T>();
			var result = publisher.Future;

			try
			{
				Operation.Start(o => operationPattern(o, publisher), o =>
				{
					if (o.AsynchronousException != null)
					{
						result.SetError(o.AsynchronousException);
					}
					else if (o.TimedOut)
					{
						result.SetError(new TimeoutException(string.Format("The operation - '{0}' timed-out.", operationPattern.Method.Name)));
					}
					else
					{
						if (!result.IsComplete)
						{
							result.SetError(new InvalidOperationException(string.Format("The operation - '{0}' exited without publishing a result.", operationPattern.Method.Name)));
						}
					}
				});
			}
			catch (Exception ex)
			{
				result.SetError(ex);
			}

			return result;
		}

		private T _result;

		/// <summary>
		/// Initializes a new instance of the <see cref="Future{T}"/> class.
		/// </summary>
		protected internal Future() { }

		/// <summary>
		/// Gets the result. Will block if this instance is not yet complete.
		/// </summary>
		/// <value>The result.</value>
		public T Result
		{
			get
			{
				Wait();
				if (HasResult) return _result;
				if (HasError) throw GetErrorForRethrow();
				throw new InvalidOperationException(string.Format("Result is not available because this instance of Future<{0}> was canceled.", typeof(T).Name));
			}
		}

		/// <summary>
		/// Executes the specified reaction when the future completes
		/// or executes the reaction immediately if the future has already completed.
		/// </summary>
		/// <param name="reaction">The reaction.</param>
		[Obsolete]
		public void ReactWith(IFutureReaction<T> reaction)
		{
			ArgumentAssert.IsNotNull(reaction, "reaction");

			OnComplete(() =>
			{
				if (HasError)
				{
					reaction.OnError(Error);
				}
				else
				{
					reaction.OnCompleted(Result);
				}
			});
		}

		/// <summary>
		/// Instructs the future to react with the specified error action.
		/// </summary>
		/// <param name="onError">The on error reaction.</param>
		/// <returns>This future.</returns>
		new public Future<T> OnError(Action<Exception> onError)
		{
			base.OnError(onError);
			return this;
		}

		/// <summary>
		/// Instructs the future to react with the specified action when the future completes successfully or otherwise.
		/// </summary>
		/// <param name="onComplete">The on complete reaction.</param>
		/// <returns>This future.</returns>
		new public Future<T> OnComplete(Action onComplete)
		{
			base.OnComplete(onComplete);
			return this;
		}

		/// <summary>
		/// Instructs the future to react with the specified action when the future completes successfully.
		/// </summary>
		/// <param name="onSuccess">The on success reaction.</param>
		/// <returns>This future.</returns>
		public Future<T> OnSuccess(Action<T> onSuccess)
		{
			return OnComplete(() =>
			{
				if (!HasError) onSuccess(Result);
			});
		}

		/// <summary>
		/// Instructs the future to execute the specified action when a consumer successfully cancels the operation.
		/// </summary>
		/// <param name="onCancel">The <see cref="Action"/> to run when the future is successfully canceled.</param>
		/// <returns>This future.</returns>
		new public Future<T> OnCancel(Action onCancel)
		{
			base.OnCancel(onCancel);
			return this;
		}

		protected override bool CompleteFromBase(FutureStatus state, Exception error)
		{
			return Complete(state, default(T), error);
		}

		/// <summary>
		/// Completes the future with the specified state/value/error.
		/// </summary>
		/// <param name="state">The state.</param>
		/// <param name="value">The value.</param>
		/// <param name="error">The error.</param>
		protected bool Complete(FutureStatus state, T value, Exception error)
		{
			if (state == FutureStatus.Incomplete)
			{
				throw new ArgumentException("state may not be Incomplete", "state");
			}

			if (state == FutureStatus.Failure && error == null)
			{
				error = new Exception("An unknown error occurred.");
			}

			Action onComplete;
			lock (SyncRoot)
			{
				Debug.Assert(!IsComplete || state == FutureStatus.Canceled, "This future has already completed.");
				if (IsComplete) return false;

				_error = error;
				_result = value;

				onComplete = _onComplete;
				_onComplete = null;

				// note - must be set after other state variables for concurrency reasons
				_state = state;

				if (_waiterCount > 0)
				{
					Monitor.PulseAll(SyncRoot);
				}
			}
			if (onComplete != null) onComplete();
			return true;
		}

		[Obsolete]
		void IFutureReaction<T>.OnError(Exception error)
		{
			SetError(error);
		}

		[Obsolete]
		void IFutureReaction<T>.OnCompleted(T value)
		{
			SetResult(value);
		}

		protected internal bool SetResult(T value)
		{
			return Complete(FutureStatus.Success, value, null);
		}

		protected internal bool SetError(Exception error)
		{
			return Complete(FutureStatus.Failure, default(T), error);
		}
	}
}
