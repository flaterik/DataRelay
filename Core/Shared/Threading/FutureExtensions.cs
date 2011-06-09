using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using MySpace.Common.Dynamic;
using MySpace.Common.Dynamic.Reflection;
using MySpace.Common.Threading;
using MySpace.Logging;

namespace MySpace.Common
{
	/// <summary>
	/// Encapsulates extension methods that operation on <see cref="IFuture{T}"/>, <see cref="IFutureReaction{T}"/>, and <see cref="Future{T}"/>.
	/// </summary>
	public static class FutureExtensions
	{
		private static readonly LogWrapper _log = new LogWrapper();
		private static readonly Func<Exception, Exception> _prepareForRemoting;

		static FutureExtensions()
		{
			try
			{
				var prepForRemotingMethod = typeof(Exception).GetMethod("PrepForRemoting", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);

				if (prepForRemotingMethod == null)
				{
					_prepareForRemoting = e => e;
					Trace.TraceWarning("Couldn't get internal method for preserving stack traces. Stack traces may not contain all necessary information.");
				}
				else
				{
					var dm = new DynamicMethod("Exception_PrepForRemotingWrapper", typeof(Exception), new[] { typeof(Exception) }, true);
					var g = new MethodGenerator(new MsilWriter(dm));
					g.Load(g.GetParameter(0));
					g.Call(prepForRemotingMethod);
					if (prepForRemotingMethod.ReturnType != typeof(Exception))
					{
						g.Load(g.GetParameter(0));
					}
					g.Return();
					_prepareForRemoting = (Func<Exception, Exception>)dm.CreateDelegate(typeof(Func<Exception, Exception>));
				}
			}
			catch (Exception ex)
			{
				_prepareForRemoting = e => e;
				Trace.TraceWarning("Couldn't construct method for preserving stack traces. Stack traces may not contain all necessary information. - " + ex);
			}
		}

		/// <summary>
		/// Combines the specified futures into one. If all succeed the result future will succeed. Otherwise the future will fail.
		/// </summary>
		/// <param name="futures">The futures.</param>
		/// <returns>A combined future.</returns>
		public static Future Combine(this IEnumerable<IFuture> futures)
		{
			var publisher = new FuturePublisher<int>();

			bool failed = false;
			List<IFuture> incompleteFutures = null;
			foreach (var future in futures)
			{
				if (future.Status == FutureStatus.Failure)
				{
					_log.Error(future.Error);
					failed = true;
				}
				else
				{
					if (incompleteFutures == null)
					{
						incompleteFutures = new List<IFuture>();
					}
					incompleteFutures.Add(future);
				}
			}

			if (incompleteFutures == null)
			{
				if (failed)
				{
					publisher.SetError(new ApplicationException("One or more futures failed. See log for details."));
				}
				else
				{
					publisher.SetResult(0);
				}
				return publisher.Future;
			}

			var latch = new CountDownLatch(incompleteFutures.Count, false);

			latch.Signaled += (sender, args) =>
			{
				if (failed)
				{
					publisher.SetError(new ApplicationException("One or more futures failed. See log for details."));
				}
				else
				{
					publisher.SetResult(0);
				}
			};

			foreach (var future in incompleteFutures)
			{
				var temp = future;
				future.OnComplete(() =>
				{
					if (temp.Status == FutureStatus.Failure)
					{
						failed = true;
						_log.Error(temp.Error);
					}
					latch.Decrement();
				});
			}

			return publisher.Future;
		}

		/// <summary>
		/// Instructs the future to react with the specified error action.
		/// </summary>
		/// <param name="future">The future to subscribe to.</param>
		/// <param name="onError">The on error reaction.</param>
		/// <returns>This future.</returns>
		public static IFuture OnError(this IFuture future, Action<Exception> onError)
		{
			ArgumentAssert.IsNotNull(future, "future");
			ArgumentAssert.IsNotNull(onError, "onError");

			return future.OnComplete(() => { if (future.Status == FutureStatus.Failure) onError(future.Error); });
		}

		/// <summary>
		/// Instructs the future to react with the specified error action.
		/// </summary>
		/// <param name="future">The future to subscribe to.</param>
		/// <param name="onError">The on error reaction.</param>
		/// <returns>This future.</returns>
		public static IFuture<T> OnError<T>(this IFuture<T> future, Action<Exception> onError)
		{
			ArgumentAssert.IsNotNull(future, "future");
			ArgumentAssert.IsNotNull(onError, "onError");

			future.OnComplete(() => { if (future.Status == FutureStatus.Failure) onError(future.Error); });
			return future;
		}

		/// <summary>
		/// Instructs the future to react with the specified action when the future completes successfully.
		/// </summary>
		/// <param name="future">The future to subscribe to.</param>
		/// <param name="onSuccess">The on success reaction.</param>
		/// <returns>This future.</returns>
		public static IFuture OnSuccess(this IFuture future, Action onSuccess)
		{
			ArgumentAssert.IsNotNull(future, "future");
			ArgumentAssert.IsNotNull(onSuccess, "onSuccess");

			return future.OnComplete(() => { if (future.Status == FutureStatus.Success) onSuccess(); });
		}

		/// <summary>
		/// Instructs the future to react with the specified action when the future completes successfully.
		/// </summary>
		/// <param name="future">The future to subscribe to.</param>
		/// <param name="onSuccess">The on success reaction.</param>
		/// <returns>This future.</returns>
		public static IFuture<T> OnSuccess<T>(this IFuture<T> future, Action<T> onSuccess)
		{
			ArgumentAssert.IsNotNull(future, "future");
			ArgumentAssert.IsNotNull(onSuccess, "onSuccess");

			future.OnComplete(() => { if (future.Status == FutureStatus.Success) onSuccess(future.Result); });
			return future;
		}

		/// <summary>
		/// Instructs the future to react with the specified action when the future is canceled.
		/// </summary>
		/// <param name="future">The future to subscribe to.</param>
		/// <param name="onCancel">The on error reaction.</param>
		/// <returns>This future.</returns>
		public static IFuture OnCancel(this IFuture future, Action onCancel)
		{
			ArgumentAssert.IsNotNull(future, "future");
			ArgumentAssert.IsNotNull(onCancel, "onCancel");

			return future.OnComplete(() => { if (future.Status == FutureStatus.Canceled) onCancel(); });
		}

		/// <summary>
		/// Instructs the future to react with the specified action when the future is canceled.
		/// </summary>
		/// <param name="future">The future to subscribe to.</param>
		/// <param name="onCancel">The on error reaction.</param>
		/// <returns>This future.</returns>
		public static IFuture<T> OnCancel<T>(this IFuture<T> future, Action onCancel)
		{
			ArgumentAssert.IsNotNull(future, "future");
			ArgumentAssert.IsNotNull(onCancel, "onCancel");

			future.OnComplete(() => { if (future.Status == FutureStatus.Canceled) onCancel(); });
			return future;
		}

		/// <summary>
		/// Returns a future containing all results of futures that will complete with all specified futures are complete.
		/// If any future fails or is canceled the result future will immediately fail with the underlying error.
		/// </summary>
		/// <typeparam name="T">The type encapsulated by the future.</typeparam>
		/// <param name="futures">The futures.</param>
		/// <returns>A future containing all results of futures that will complete with all specified futures are complete.</returns>
		public static Future<IEnumerable<T>> Join<T>(this IEnumerable<Future<T>> futures)
		{
			var result = new Future<IEnumerable<T>>();
			var fs = futures.ToList();
			int count = fs.Count;
			foreach (var source in fs)
			{
				var s = source;
				source.OnComplete(() =>
				{
					if (s.HasError)
					{
						result.SetError(s.Error);
					}
					else if (s.IsCanceled)
					{
						result.SetError(new Exception("One or more underlying futures was canceled."));
					}
					else if (Interlocked.Decrement(ref count) == 0)
					{
						try
						{
							result.SetResult(fs.Select(f => f.Result));
						}
						catch (Exception ex)
						{
							result.SetError(ex);
						}
					}
				});
			}

			return result;
		}

		/// <summary>
		/// Iterates through the collection of futures asynchronously by reacting to each one before moving to the next.
		/// This operation does not bock unless the supplied iterator pattern blocks.
		/// </summary>
		/// <param name="futures">The futures.</param>
		/// <param name="stopOnFutureErrors"><see langword="true"/> to complete with an exception if a yielded future fails; <see langword="false"/> to continue execution.</param>
		/// <returns>A future that completes after sequentially waiting for completion on each one or when it encounters an exception.</returns>
		public static Future ExecuteSequentially(this IEnumerable<Future> futures, bool stopOnFutureErrors = true)
		{
			ArgumentAssert.IsNotNull(futures, "futures");

			var iterator = new SequentialFutureIterator(futures, stopOnFutureErrors);
			iterator.Begin();
			return iterator.Result;
		}

		private class SequentialFutureIterator
		{
			[ThreadStatic]
			private static int _recursionCount;
			[ThreadStatic]
			private static bool _trampoline;

			private readonly IEnumerator<Future> _enumerator;
			private readonly bool _stopOnFutureErrors;
			private readonly Future<int> _result;

			public SequentialFutureIterator(IEnumerable<Future> futures, bool stopOnFutureErrors)
			{
				_enumerator = futures.GetEnumerator();
				_stopOnFutureErrors = stopOnFutureErrors;
				_result = new Future<int>();
			}

			public void Begin()
			{
				NextStep();
			}

			private void NextStep()
			{
				if (_recursionCount > 0)
				{
					_trampoline = true;
					return;
				}

				while (true)
				{
					Future current;
					do
					{
						bool complete;
						try
						{
							complete = !_enumerator.MoveNext();
						}
						catch (Exception ex)
						{
							SetComplete(ex);
							return;
						}
						if (complete)
						{
							SetComplete(null);
							return;
						}

						current = _enumerator.Current;

					} while (current == null);

					if (current.IsComplete) continue;

					++_recursionCount;
					current.OnComplete(Continue);
					--_recursionCount;

					if (_recursionCount > 0 || !_trampoline) break;

					_trampoline = false;
					continue;
				}
			}

			private void SetComplete(Exception error)
			{
				try
				{
					_enumerator.Dispose();
				}
				catch (Exception disposeException)
				{
					_result.SetError(disposeException);
					return;
				}
				if (error == null)
				{
					_result.SetResult(0);
				}
				else
				{
					_result.SetError(error);
				}
			}

			public Future Result
			{
				get { return _result; }
			}

			private void Continue()
			{
				var current = _enumerator.Current;
				if (current.HasError && _stopOnFutureErrors)
				{
					SetComplete(current.Error);
				}
				else
				{
					NextStep();
				}
			}
		}

		internal static void TryDispose(this IDisposable disposable)
		{
			if (disposable != null)
			{
				try
				{
					disposable.Dispose();
				}
				catch (Exception ex)
				{
					Trace.TraceError("An error occurred while attempting to dispose an object - " + ex);
				}
			}
		}

		/// <summary>
		/// Prepares the exception for re-throw by preserving the stack trace.
		/// This method should be called just before re-throwing the exception.
		/// This method may not have any effect in future versions of the .NET framework because it depends on internal methods.
		/// </summary>
		/// <param name="exception">The exception that will be re-thrown.</param>
		/// <returns>The exception that was passed into this function after the stack trace has been preserved.</returns>
		public static Exception PrepareForRethrow(this Exception exception)
		{
			_prepareForRemoting(exception);
			return exception;
		}

		/// <summary>
		/// Returns a future that completes with a <see cref="TimeoutException"/> if the source future takes longer to complete than the specified timeout.
		/// </summary>
		/// <typeparam name="T">The type of value encapsulated by the future.</typeparam>
		/// <param name="future">The future to read from.</param>
		/// <param name="timeout">The timeout.</param>
		/// <returns>A future that completes with a <see cref="TimeoutException"/> if the source future takes longer to complete than the specified timeout.</returns>
		public static Future<T> TimeoutWith<T>(this Future<T> future, TimeSpan timeout)
		{
			var result = new Future<T>();
			bool timedOut = false;
			var stopwatch = Stopwatch.StartNew();
			var timer = new Timer(o =>
			{
				timedOut = true;
				future.Cancel();
			});
			timer.Change(timeout, TimeSpan.FromMilliseconds(Timeout.Infinite));

			future.OnComplete(() =>
			{
				stopwatch.Stop();
				timer.TryDispose();
				if (timedOut)
				{
					result.SetError(new TimeoutException(string.Format("Timed out after {0:0.00} seconds.", stopwatch.Elapsed.TotalSeconds)));
					return;
				}
				future.ForwardTo(result);
			});

			return result;
		}

		internal static Future<T> TimeoutWith<T>(this Future<T> future, T value, TimeSpan timeout)
		{
			bool timedOut = false;
			var result = new Future<T>();
			var timer = new Timer(o =>
			{
				timedOut = true;
				future.Cancel();
			});
			timer.Change(timeout, TimeSpan.FromMilliseconds(Timeout.Infinite));

			future.OnComplete(() =>
			{
				timer.TryDispose();
				if (timedOut)
				{
					result.SetResult(value);
					return;
				}
				future.ForwardTo(result);
			});

			return result;
		}

		public static void ForwardTo<T>(this Future<T> source, Future<T> destination)
		{
			ArgumentAssert.IsNotNull(source, "source");
			ArgumentAssert.IsNotNull(destination, "destination");

			if (source.IsComplete)
			{
				source.CopyTo(destination);
				return;
			}

			source.OnComplete(() => source.CopyTo(destination));
		}

		private static void CopyTo<T>(this IFuture<T> source, Future<T> dest)
		{
			switch (source.Status)
			{
				case FutureStatus.Success:
					dest.SetResult(source.Result);
					return;
				case FutureStatus.Canceled:
					dest.Cancel();
					return;
				case FutureStatus.Failure:
					dest.SetError(source.Error);
					return;
			}
			throw new InvalidOperationException("This method should never be called on an incomplete future.");
		}

		/// <summary>
		/// Catches any errors in the source future and handles them with the specified handler.
		/// If <paramref name="handler"/> throws an error the result will complete with that error.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="source">The source.</param>
		/// <param name="handler">The handler.</param>
		/// <returns>A result that will catch errors from <paramref name="source"/> with <paramref name="handler"/>.</returns>
		public static Future<T> Catch<T>(this IFuture<T> source, Func<Exception, T> handler)
		{
			var result = new Future<T>();

			source.OnComplete(() =>
			{
				if (source.Status == FutureStatus.Failure)
				{
					try
					{
						result.SetResult(handler(source.Error));
					}
					catch (Exception ex)
					{
						result.SetError(ex);
					}
					return;
				}
				source.CopyTo(result);
			});

			return result;
		}

		// todo - when we switch to 4.0 use covariant instead of generic
		/// <summary>
		/// Waits for all futures to complete.
		/// </summary>
		/// <param name="futures">The futures to wait for.</param>
		public static void WaitAll<T>(this IEnumerable<T> futures)
			where T : Future
		{
			var latch = new CountDownLatch(futures.Count(), false);
			foreach (var future in futures)
			{
				future.OnComplete(latch.Decrement);
			}
			latch.Wait();
		}

		/// <summary>
		/// Instructs the future to react with the specified complete or error actions (depending on success or failure).
		/// </summary>
		/// <typeparam name="T">The value produced by the future.</typeparam>
		/// <param name="future">The future.</param>
		/// <param name="onComplete">The completion reaction.</param>
		/// <param name="onError">The on error reaction.</param>
		[Obsolete]
		public static void ReactWith<T>(this Future<T> future, Action<T> onComplete, Action<Exception> onError)
		{
			future.ReactWith(new AnonymousFutureReaction<T>(onComplete, onError));
		}

		/// <summary>
		/// Instructs the future to react with the specified complete or error actions (depending on success or failure).
		/// </summary>
		/// <param name="future">The future.</param>
		/// <param name="onComplete">The completion reaction.</param>
		/// <param name="onError">The on error reaction.</param>
		[Obsolete]
		public static void ReactWith(this Future future, Action onComplete, Action<Exception> onError)
		{
			future.ReactWith(new AnonymousFutureReaction(onComplete, onError));
		}

		/// <summary>
		/// Validates the result from the specified future.
		/// </summary>
		/// <typeparam name="T">The type of result.</typeparam>
		/// <param name="source">The source.</param>
		/// <param name="validator">The validator function.</param>
		/// <returns>A future that validates the result of the source future.</returns>
		public static Future<T> Validate<T>(this Future<T> source, Action<T> validator)
		{
			var result = new Future<T>();

			source.OnComplete(() =>
			{
				if (source.HasResult)
				{
					try
					{
						validator(source.Result);
					}
					catch (Exception ex)
					{
						result.SetError(ex);
						return;
					}
					result.SetResult(source.Result);
				}
				else
				{
					source.ForwardTo(result);
				}
			});

			return result;
		}

		/// <summary>
		/// Converts the specified future into future of <typeparamref name="TResult"/>.
		/// When the source future completes <paramref name="converter"/> runs and then
		/// the produced value is feed to the result future.
		/// </summary>
		/// <typeparam name="TSource">The type of value encapsulated by the source future.</typeparam>
		/// <typeparam name="TResult">The type of value encapsulated by the result future.</typeparam>
		/// <param name="source">The future to convert from.</param>
		/// <param name="converter">The converter that will convert the source value into the result value.</param>
		/// <returns>A new <see cref="Future{TResult}"/>.</returns>
		public static Future<TResult> Convert<TSource, TResult>(this Future<TSource> source, Func<TSource, TResult> converter)
		{
			var dest = new Future<TResult>();

			if (source.IsComplete)
			{
				ExecuteConvert(source, dest, converter);
				return dest;
			}

			source.OnComplete(() => ExecuteConvert(source, dest, converter));

			dest.OnComplete(() =>
			{
				if (dest.IsCanceled)
				{
					source.Cancel();
				}
			});

			return dest;
		}

		private static void ExecuteConvert<TSource, TResult>(Future<TSource> source, Future<TResult> dest, Func<TSource, TResult> converter)
		{
			Debug.Assert(source.IsComplete, "Source should be complete before calling this method.");

			if (source.HasError)
			{
				dest.SetError(source.Error);
			}
			else if (source.IsCanceled)
			{
				if (!dest.IsComplete)
				{
					dest.SetError(new ApplicationException("The underlying source future was canceled."));
				}
			}
			else
			{
				TResult resultValue;
				try
				{
					resultValue = converter(source.Result);
				}
				catch (Exception ex)
				{
					dest.SetError(ex);
					return;
				}
				dest.SetResult(resultValue);
			}
		}

		/// <summary>
		/// Gets a <see cref="Future{T}"/> for the request stream of a <see cref="WebRequest"/>
		/// using <see cref="WebRequest.BeginGetRequestStream"/> and <see cref="WebRequest.EndGetRequestStream"/>.
		/// Note that this method does not timeout.
		/// </summary>
		/// <param name="request">The request to get a request stream for.</param>
		/// <returns>A <see cref="Future{T}"/> for the request stream of a <see cref="WebRequest"/>.</returns>
		public static Future<Stream> GetRequestStreamFuture(this WebRequest request)
		{
			return Future.FromAsyncPattern<Stream>(
				ac => request.BeginGetRequestStream(ac, null),
				request.EndGetRequestStream)
				.OnCancel(request.Abort);
		}

		/// <summary>
		/// Gets a <see cref="Future{T}"/> of <see cref="WebResponse"/> given a <see cref="WebRequest"/>
		/// using <see cref="WebRequest.BeginGetResponse"/> and <see cref="WebRequest.EndGetResponse"/>.
		/// Note that this method does not timeout.
		/// </summary>
		/// <param name="request">The request to get a response from.</param>
		/// <returns>A <see cref="Future{T}"/> of <see cref="WebResponse"/>.</returns>
		public static Future<WebResponse> GetResponseFuture(this WebRequest request)
		{
			return Future.FromAsyncPattern<WebResponse>(
				ac => request.BeginGetResponse(ac, null),
				request.EndGetResponse)
				.OnCancel(request.Abort);
		}

		public static Operation Create<T>(this Operation operation, Future<T> future, TimeSpan timeout)
		{
			ArgumentAssert.IsNotNull(operation, "operation");
			ArgumentAssert.IsNotNull(future, "future");

			return operation.Create((Action<Action>)(doneAction => future.ReactWith(value => doneAction(), e => doneAction())), timeout);
		}

		private class AnonymousFutureReaction<T> : IFutureReaction<T>
		{
			private readonly Action<T> _onComplete;
			private readonly Action<Exception> _onError;

			public AnonymousFutureReaction(Action<T> onComplete, Action<Exception> onError)
			{
				_onComplete = onComplete;
				_onError = onError;
			}

			#region IFutureReaction<T> Members

			public void OnError(Exception error)
			{
				if (_onError != null) _onError(error);
			}

			public void OnCompleted(T value)
			{
				if (_onComplete != null) _onComplete(value);
			}

			#endregion
		}

		private class AnonymousFutureReaction : IFutureReaction
		{
			private readonly Action _onComplete;
			private readonly Action<Exception> _onError;

			public AnonymousFutureReaction(Action onComplete, Action<Exception> onError)
			{
				_onComplete = onComplete;
				_onError = onError;
			}

			#region IFutureReaction<T> Members

			public void OnError(Exception error)
			{
				if (_onError != null) _onError(error);
			}

			public void OnCompleted()
			{
				if (_onComplete != null) _onComplete();
			}

			#endregion
		}
	}
}
