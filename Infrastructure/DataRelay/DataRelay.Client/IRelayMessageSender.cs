using System;
using System.IO;
using MySpace.Common;

namespace MySpace.DataRelay.Client
{
    /// <summary>
    /// Required interface for <see cref="CacheClient"/> to send a <see cref="RelayMessage"/> asynchrounously using
    /// the <see cref="Future"/> pattern.
    /// </summary>
    internal interface IRelayMessageSender
    {
        /// <summary>
        /// Gets a value indicating whether this <see cref="IRelayMessageSender"/> is currently connected to the remote endpoint.  This property does not block.
        /// </summary>
        /// <value><c>true</c> if connected; otherwise, <c>false</c>.</value>
        bool Connected { get; }
        /// <summary>
        /// Gets the reply latency.  It will be 0 if the sender has not been used.
        /// </summary>
        /// <value>The reply latency.</value>
        TimeSpan ReplyLatency { get; }
        /// <summary>
        /// Sends the specified relay message.
        /// </summary>
        /// <param name="relayMessage">The relay message.</param>
        /// <returns>A future of the reply RelayMessage.</returns>
        Future<RelayMessage> Send(RelayMessage relayMessage);
    }
}