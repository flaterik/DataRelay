using System;
using System.Linq;

namespace MySpace.Common
{
	/// <summary>
	/// Encapsulates the publisher of a <see cref="Future{T}"/>.
	/// </summary>
	/// <typeparam name="T">The type of result encapsulated by the future.</typeparam>
	public class FuturePublisher<T>
	{
		private readonly Future<T> _future;

		/// <summary>
		/// Initializes a new instance of the <see cref="FuturePublisher{T}"/> class.
		/// </summary>
		public FuturePublisher()
		{
			_future = new Future<T>();
		}

		/// <summary>
		/// Completes the future successfully with the specified result and runs any callbacks subscribed to the future.
		/// </summary>
		/// <param name="value">The value to complete the future with.</param>
		/// <returns><see langword="true"/> if the result was set successfully. <see langword="false"/> if the future was already set.</returns>
		public bool SetResult(T value)
		{
			return _future.SetResult(value);
		}

		/// <summary>
		/// Completes the future with the specified error and runs any callbacks subscribed to the future.
		/// </summary>
		/// <param name="error">The error to complete the future with.</param>
		public bool SetError(Exception error)
		{
			return _future.SetError(error);
		}

		/// <summary>
		/// Instructs the future to execute the specified action when a consumer successfully cancels the operation.
		/// </summary>
		/// <param name="onCancel">The <see cref="Action"/> to run when the future is successfully canceled.</param>
		public void OnCancel(Action onCancel)
		{
			_future.OnCancel(onCancel);
		}

		/// <summary>
		/// Gets the future that this publisher writes to.
		/// </summary>
		/// <value>The future this publisher writes to.</value>
		public Future<T> Future
		{
			get { return _future; }
		}
	}
}
