using System;
using System.Linq;

namespace MySpace.Common
{
	/// <summary>
	/// Encapsulates something that will complete some time in the future.
	/// </summary>
	public interface IFuture
	{
		/// <summary>
		/// Waits for the future to complete.
		/// </summary>
		void Wait();

		/// <summary>
		/// Gets the current state. Does not block.
		/// </summary>
		/// <value>The current state.</value>
		FutureStatus Status { get; }

		/// <summary>
		/// Gets the error. Will block if this instance is not yet complete.
		/// </summary>
		/// <value>The error or <see langword="null"/> if there is no error.</value>
		Exception Error { get; }

		/// <summary>
		/// Instructs the future to execute the specified action when the future completes successfully or otherwise.
		/// </summary>
		/// <param name="onComplete">The on complete action.</param>
		/// <returns>This future.</returns>
		IFuture OnComplete(Action onComplete);
	}

	/// <summary>
	/// Encapsulates a value that will be computed at some point in the future.
	/// </summary>
	/// <typeparam name="T">The type of value.</typeparam>
	public interface IFuture<out T> : IFuture
	{
		/// <summary>
		/// Gets the result. Will block if this instance is not yet complete.
		/// </summary>
		/// <value>The result.</value>
		/// <exception cref="Exception">
		///		<para>The future completed with an error. <see cref="IFuture.Error"/> will be thrown.</para>
		/// </exception>
		/// <exception cref="InvalidOperationException">
		///		<para>The future was canceled.</para>
		/// </exception>
		T Result { get; }
	}
}
