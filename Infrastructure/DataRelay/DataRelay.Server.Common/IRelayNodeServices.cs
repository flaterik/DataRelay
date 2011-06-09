using System;

namespace MySpace.DataRelay.Server.Common
{
    /// <summary>
    /// This interface defines relay node services that a component may need.  
    /// For example, it facilitates Inter Relay Component Communication.
    /// </summary>
    public interface IRelayNodeServices
    {
		/// <summary>
		/// This method is used to dispatch an "in" (save, update, etc) <see cref="RelayMessage"/> to all local components except those in the <paramref name="exclusionList"/> 
		/// </summary>
		/// <param name="message">the <see cref="RelayMessage"/> to send</param>
		/// <param name="exclusionList">list of <see cref="IRelayComponent"/> Types to exclude</param>
		void HandleInMessageWithComponentExclusionList(RelayMessage message, params Type[] exclusionList);

		/// <summary>
		/// This method is used to dispatch an "out" (get, query, etc) <see cref="RelayMessage"/> to all local components except those in the <paramref name="exclusionList"/> 
		/// </summary>
		/// <param name="message">the <see cref="RelayMessage"/> to send</param>
		/// <param name="exclusionList">list of <see cref="IRelayComponent"/> Types to exclude</param>        
		void HandleOutMessageWithComponentExclusionList(RelayMessage message, params Type[] exclusionList);

		/// <summary>
		///		<para>Instructs the host to shutdown because one or more components are in a corrupted state.</para>
		/// </summary>
		/// <param name="message">The message to log. <see langword="null"/> if no message is available.</param>
		/// <param name="exception">The exception to log. <see langword="null"/> if no exception is available.</param>
		void FailFatally(string message, Exception exception);

		/// <summary>
		/// Gets the size of the In message Queue.
		/// </summary>
    	int InMessageQueueSize { get; }

		/// <summary>
		/// Gets the size of the Out message Queue.
		/// </summary>
    	int OutMessageQueueSize { get; }
    }
}
