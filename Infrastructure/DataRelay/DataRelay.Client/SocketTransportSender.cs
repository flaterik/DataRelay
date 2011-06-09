using System;
using System.Diagnostics;
using MySpace.Common;
using MySpace.DataRelay.Common.Schemas;
using MySpace.DataRelay.Transports;
using MySpace.Metrics;

namespace MySpace.DataRelay.Client
{
    internal class SocketTransportSender : IRelayMessageSender
    {
        private readonly IAsyncRelayTransport _transport;
        private readonly SloppyMovingAverage _averager = new SloppyMovingAverage(1000);
        private readonly TimeSpan _sendTimeout;

        public SocketTransportSender(RelayNodeDefinition nodeDefinition, RelayNodeGroupDefinition groupDefinition)
        {
			if(groupDefinition.SocketSettings != null)
			{
				_sendTimeout = TimeSpan.FromMilliseconds(groupDefinition.SocketSettings.SendTimeout);
			}
			else if(groupDefinition.DefaultSocketSettings != null)
			{
				_sendTimeout = TimeSpan.FromMilliseconds(groupDefinition.DefaultSocketSettings.SendTimeout);
			}
			else
			{
				_sendTimeout = TimeSpan.Zero;
			}

            _transport = new SocketTransportAdapter(nodeDefinition, groupDefinition, 1);
        }

        public Future<RelayMessage> Send(RelayMessage relayMessage)
        {
            var publisher = new FuturePublisher<RelayMessage>();
            var stopwatch = Stopwatch.StartNew();
            try
            {
                _transport.BeginSendMessage(relayMessage, relayMessage.IsTwoWayMessage,
                                            asyncResult =>
                                                {
                                                    try
                                                    {
                                                        _transport.EndSendMessage(asyncResult);

                                                        publisher.SetRelayMessageResult(relayMessage);

                                                        UpdateStatistics(!publisher.Future.HasError, stopwatch.ElapsedTicks);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        UpdateStatistics(false, stopwatch.ElapsedTicks);
                                                        publisher.SetError(ex);
                                                    }
                                                }, null);
            }
            catch(Exception ex)
            {
                UpdateStatistics(false, stopwatch.ElapsedTicks);
                publisher.SetError(ex);
            }

            return publisher.Future;
        }

        private void UpdateStatistics(bool success, long elapsedTicks)
        {
            if (success) _averager.UpdateAverage(elapsedTicks);
            else _averager.UpdateAverage(_sendTimeout.Ticks==0 ? (elapsedTicks * 2) : (_sendTimeout.Ticks * 2));
        }

        public bool Connected
        {
            get
            {
                int openConnections;
                int activeConnections;
                _transport.GetConnectionStats(out openConnections, out activeConnections);
                return openConnections > 0;
            }
        }

        public TimeSpan ReplyLatency { get { return TimeSpan.FromTicks(_averager.CurrentAverage == 0 ? _averager.PrematureAverage : _averager.CurrentAverage); } }
    }
}