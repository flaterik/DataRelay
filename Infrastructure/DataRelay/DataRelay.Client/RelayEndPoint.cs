using System;
using System.Diagnostics;
using System.Net;
using MySpace.DataRelay.Common.Schemas;
using MySpace.PipelineTransport;

namespace MySpace.DataRelay.Client
{
    /// <summary>
    /// Represents a Data Relay endpoint, which is an IP Address and Port that accepts messages for a specific transport.  Transports include
    /// <see cref="PipelineTransport"/> and <see cref="SocketTransport"/>.
    /// </summary>
    /// <remarks>RelayEndPoint examines the RelayNode definition, from the configs, and find the ServiceType, using that to decide
    /// which kind of client (such as "Pipeline" or "Sockets") to instantiate for the given RelayNode endpoint.</remarks>
    [DebuggerDisplay("RelayEndPoint {_ipEndPoint}")]
    internal class RelayEndPoint : IComparable<RelayEndPoint>
    {
        private readonly IPEndPoint _ipEndPoint;

        public IRelayMessageSender RelayMessageSender { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RelayEndPoint"/> class, and presents a <see cref="IRelayMessageSender"/>.
        /// </summary>
        /// <param name="nodeDefinition">The node definition.</param>
        /// <param name="groupDefinition">The group definition.</param>
        /// <exception cref="InvalidOperationException">Thrown if the RelayNode configuration member "ServiceType" is set to an invalid value.</exception>
        /// <remarks>A RelayEndPoint references either a PipelineTransport or or SocketTransport type endpoint, which is configured in the RelayNodeMapping.  
        /// A pipeline node will have the PipelinePort set to a nonzero value.  A SocketTransport node will have the ServiceType="Sockets" and Port set 
        /// to a nonzero value.</remarks>
        public RelayEndPoint(RelayNodeDefinition nodeDefinition, RelayNodeGroupDefinition groupDefinition)
        {
            var address = Dns.GetHostAddresses(nodeDefinition.Host)[0];
            if (nodeDefinition.PipelinePort != 0)
            {
                _ipEndPoint = new IPEndPoint(address, nodeDefinition.PipelinePort);
                InstantiatePipelineClient(groupDefinition);
            }
            else
            {
                if (nodeDefinition.ServiceType == "Sockets")
                {
                    _ipEndPoint = new IPEndPoint(address, nodeDefinition.Port);
                    InstantiateSocketTransportClient(nodeDefinition, groupDefinition);
                }
                else
                {
                    throw new InvalidOperationException(String.Format("An invalid ServiceType, {0}, was defined for Relay Node {2}/{1}.", nodeDefinition.ServiceType, nodeDefinition.Host, groupDefinition.Name));
                }
            }
        }

        private void InstantiatePipelineClient(RelayNodeGroupDefinition groupDefinition)
        {
            var client = PipelineClient.GetClient(_ipEndPoint);
            client.AckTimeout = TimeSpan.FromMilliseconds(groupDefinition.SocketSettings.ReceiveTimeout>>1);
            client.ResponseTimeout = TimeSpan.FromMilliseconds(groupDefinition.SocketSettings.ReceiveTimeout);
            RelayMessageSender = new PipelineSender(client);
        }

        private void InstantiateSocketTransportClient(RelayNodeDefinition nodeDefinition, RelayNodeGroupDefinition groupDefinition)
        {
            RelayMessageSender = new SocketTransportSender(nodeDefinition, groupDefinition);
        }

        #region IComparable<RelayEndPoint>
        public int CompareTo(RelayEndPoint other)
        {
            var thisLatency = RelayMessageSender.ReplyLatency;
            var otherLatency = other.RelayMessageSender.ReplyLatency;
            return (int)(thisLatency.Ticks - otherLatency.Ticks);
        }
        #endregion
    }
}
