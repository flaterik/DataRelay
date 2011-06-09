using System;
using System.Linq;

namespace MySpace.Common
{
	/// <summary>
	/// Encapsulates a reaction event. Reaction events may subscribe to <see cref="IFuture.ReactWith"/>
	/// and will be executed exactly once when the future completes or immediately if the future is already complete.
	/// </summary>
	[Obsolete]
	public interface IFutureReaction
	{
		/// <summary>
		/// Called when the target <see cref="Future"/> instance fails.
		/// </summary>
		/// <param name="error">The error.</param>
		void OnError(Exception error);

		/// <summary>
		/// Called when the target <see cref="Future"/> instance completes successfully.
		/// </summary>
		void OnCompleted();
	}

	/// <summary>
	/// Encapsulates a reaction event. Reaction events may subscribe to <see cref="IFuture{T}.ReactWith"/>
	/// and will be executed exactly once when the future completes or immediately if the future is already complete.
	/// </summary>
	/// <typeparam name="T">The type of value.</typeparam>
	[Obsolete]
	public interface IFutureReaction<in T>
	{
		/// <summary>
		/// Called when the target <see cref="Future{T}"/> instance fails.
		/// </summary>
		/// <param name="error">The error.</param>
		void OnError(Exception error);

		/// <summary>
		/// Called when the target <see cref="Future{T}"/> instance completes successfully with a value.
		/// </summary>
		/// <param name="value">The value.</param>
		void OnCompleted(T value);
	}
}
