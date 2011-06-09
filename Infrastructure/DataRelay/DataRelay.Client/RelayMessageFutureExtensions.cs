using System;
using System.IO;
using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Client
{
    internal static class RelayMessageFutureExtensions
    {
        /// <summary>
        /// Completes a FuturePublisher{RelayMessage} with a RelayMessage.
        /// </summary>
        /// <param name="publisher">The publisher.</param>
        /// <param name="relayMessage">The relay message.</param>
        /// <returns></returns>
        public static FuturePublisher<RelayMessage> SetRelayMessageResult(this FuturePublisher<RelayMessage> publisher, RelayMessage relayMessage)
        {
            if (relayMessage.MessageType == MessageType.Get)
            {
                publisher.SetResult(relayMessage);
            }
            else
            {
                switch (relayMessage.ResultOutcome)
                {
                    case RelayOutcome.Success:
                        publisher.SetResult(relayMessage);
                        break;
                    case RelayOutcome.Timeout:
                        publisher.SetError(new TimeoutException(String.Format("{0} timed out.", relayMessage.MessageType)));
                        break;
                    case null:
                        if (!relayMessage.IsTwoWayMessage)
                        {
                            publisher.SetResult(relayMessage);
                        }
                        else
                        {
                            publisher.SetError(new Exception(String.Format("{0} failed with {1}.", relayMessage.MessageType, relayMessage.ResultOutcome)));
                        }
                        break;
                    default:
                        publisher.SetError(new Exception(String.Format("{0} failed with {1}.", relayMessage.MessageType, relayMessage.ResultOutcome)));
                        break;
                }
            }
            return publisher;
        }

        /// <summary>
        /// Completes a FuturePublisher{RelayMessage} with the results of a Future{Stream}.
        /// </summary>
        /// <param name="publisher">The publisher.</param>
        /// <param name="futureStream">The future stream.</param>
        /// <returns></returns>
        public static FuturePublisher<RelayMessage> CompleteWith(this FuturePublisher<RelayMessage> publisher, Future<Stream> futureStream)
        {
            futureStream.OnComplete(() =>
                                        {
                                            if (futureStream.HasError) publisher.SetError(futureStream.Error);
                                            else if (futureStream.IsCanceled) publisher.SetError(new ApplicationException("Transport operation was cancelled."));
                                            else
                                            {
                                                try
                                                {
                                                    using (var reply = futureStream.Result)
                                                    {
                                                        if (reply == null || reply.Length == 0) publisher.SetResult(default(RelayMessage));
                                                        else
                                                        {
                                                            var replyMessage = Serializer.Deserialize<RelayMessage>(reply, SerializerFlags.Default);
                                                            SetRelayMessageResult(publisher, replyMessage);
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    publisher.SetError(ex);
                                                }
                                            }
                                        });
            return publisher;
        }
    }
}