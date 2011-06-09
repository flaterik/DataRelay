using System;
using System.IO;
using MySpace.Common;
using MySpace.Common.IO;
using MySpace.PipelineTransport;

namespace MySpace.DataRelay.Client
{
    internal class PipelineSender : IRelayMessageSender
    {
        private readonly PipelineClient _client;

        public PipelineSender(PipelineClient client)
        {
            _client = client;
        }

        public Future<RelayMessage> Send(RelayMessage relayMessage)
        {
            using (var requestStream = new MemoryStream(Serializer.Serialize(relayMessage, SerializerFlags.Default)))
            {
                return new FuturePublisher<RelayMessage>()
                    .CompleteWith(_client.SendRequest(requestStream))
                    .Future;
            }
        }

        public bool Connected 
        { 
            get
            {
                return (_client.Connected.IsComplete && _client.Connected.HasResult && _client.Connected.Result);
            }
        }

        public TimeSpan ReplyLatency
        {
            get { return _client.ReplyLatency; }
        }
    }
}