using System;
using MySpace.Common;

namespace MySpace.DataRelay.Client
{
	/// <summary>
	/// This class expands on why a given <see cref="ICacheParameter"/> object from the transport
	/// could not be found and loaded from the <see cref="RelayClient"/>.
	/// </summary>
	/// <typeparam name="T">A type that implements <see cref="ICacheParameter"/>.</typeparam>
	public class RelayResult<T> where T : ICacheParameter
	{
		#region Ctor

		/// <summary>
		/// Initializes a new instance of the <see cref="RelayResult{T}" /> class.
		/// </summary>
		/// <param name="relayResultType">Enumeration <see cref="RelayResultType"/> 
		/// depicting the state of finding and loading the object.</param>
		/// <param name="exception">Encountered exception.</param>
		/// <param name="item">Object of type <see cref="ICacheParameter"/> 
		/// that was to be found and loaded.</param>
		public RelayResult(RelayResultType relayResultType, System.Exception exception, T item)
		{
			this.RelayResultType = relayResultType;
			this.Exception = exception;
			this.Item = item;
		}

		#endregion

		/// <summary>
		///  Gets the type of relay operation result this instance represents.
		/// </summary>
		/// <value>A <see cref="RelayResultType"/> value indicating the type of relay operation result.</value>
		public RelayResultType RelayResultType
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the <see cref="Exception"/> representing the encountered exception.
		/// </summary>
		/// <value>
		/// An <see cref="Exception"/> representing the encountered exception.
		/// <list type="number">
		/// <item><see langword="null"/> if the object is successfully found and/or no exceptions are encountered.</item>
		/// <item><see langword="null"/> if no exceptions are encountered even if the object is not found.</item>
		/// <item><see cref="ApplicationException"/> if the emptyObject Type Information can not be found.</item>
		/// <item><see cref="ArgumentNullException"/>if the input emptyObject parameter is null.</item>
		/// <item><see cref="Exception"/> if any other <see cref="Exception"/> is encountered.</item>
		/// <item><see langword="null"/> an exception is not encountered.</item>
		/// </list>
		/// </value>
		public Exception Exception
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the modified input object that should be stored in this field after trying to find and load it.
		/// </summary>
		/// <value>
		/// <see langword="null"/> is a valid value.  If <see langword="null"/> is added to the 
		/// <see cref="ICacheParameter"/> input list of <see cref="RelayClient.GetObjectsWithResult"/> 
		/// or as the <see cref="ICacheParameter"/> input parameter to <see cref="RelayClient.GetObjectWithResult{T}"/>.
		/// </value>
		public T Item
		{
			get;
			private set;
		}
	}
}
