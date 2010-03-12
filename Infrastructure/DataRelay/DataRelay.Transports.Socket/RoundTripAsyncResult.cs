using System;

namespace MySpace.DataRelay.Transports
{
	/// <summary>
	/// 	<para>A simple <see cref="IAsyncResult"/> implementation
	/// 	for round trip message handling.</para>
	/// </summary>
	/// <typeparam name="T">
	///	<para>The type of message.</para>
	/// </typeparam>
	internal class RoundTripAsyncResult<T> : SimpleAsyncResult
	{
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="RoundTripAsyncResult&lt;T&gt;"/> class.</para>
		/// </summary>
		/// <param name="callback">The callback. <see langword="null"/> if not available.</param>
		/// <param name="asyncState">A user-defined state object that will be returned by <see cref="IAsyncResult.AsyncState"/>.</param>
		public RoundTripAsyncResult(AsyncCallback callback, object asyncState)
			: base(callback, asyncState)
		{
		}

		/// <summary>
		/// 	<para>Gets or sets the sent message.</para>
		/// </summary>
		/// <value>
		/// 	<para>The sent message.</para>
		/// </value>
		public T SentMessage { get; set; }

		/// <summary>
		/// 	<para>Gets or sets the response message.</para>
		/// </summary>
		/// <value>
		/// 	<para>The response message.</para>
		/// </value>
		public T ResponseMessage { get; set; }

		/// <summary>
		/// 	<para>Sets the result to complete and invokes the result callback if there was one.</para>
		/// </summary>
		/// <param name="wasSynchronous">
		/// <see langword="true"/> if the operation is being completed on the same thread it began on.
		/// <see langword="false"/> otherwise.
		/// </param>
		public void Complete(bool wasSynchronous)
		{
			CompleteOperation(wasSynchronous);
		}
	}
}
