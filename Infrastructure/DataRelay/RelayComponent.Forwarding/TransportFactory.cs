using MySpace.DataRelay.Common.Schemas;
using MySpace.DataRelay.Transports;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// Responsible for creating instances of implementation of <see cref="IRelayTransport"/>
	/// and <see cref="IRelayTransportExtended"/>.
	/// </summary>
	public static class TransportFactory
	{
		/// <summary>
		/// Creates a new instance of <see cref="IRelayTransport"/> for the given <see cref="RelayNodeDefinition"/>
		/// and <see cref="RelayNodeGroupDefinition"/>.
		/// </summary>
		/// <param name="nodeDefinition">The node definition.</param>
		/// <param name="group">The group.</param>
		/// <param name="chunkLength">The chunk length</param>
		/// <returns></returns>
		internal static IRelayTransport CreateTransportForNode(RelayNodeDefinition nodeDefinition,
			RelayNodeGroupDefinition group, int chunkLength)
		{
			CreateTransportDelegate creator = CreateTransportMethod;
			if (creator == null)
			{
				return new SocketTransportAdapter(nodeDefinition, group, chunkLength);
			}
			IRelayTransport transport = creator(nodeDefinition, group) ?? new NullTransport();
			return transport;
		}

		/// <summary>
		/// Gets or set the <see cref="CreateTransportDelegate"/> used to create new instances.
		/// </summary>
		public static CreateTransportDelegate CreateTransportMethod { get; set; }
	}

	/// <summary>
	/// A delegate to create <see cref="IRelayTransport"/> instances.
	/// </summary>
	/// <param name="nodeDefinition">The node definition.</param>
	/// <param name="groupDefinition">The group definition.</param>
	/// <returns>A new instance of <see cref="IRelayTransport"/>.</returns>
	public delegate IRelayTransport CreateTransportDelegate(RelayNodeDefinition nodeDefinition,
		RelayNodeGroupDefinition groupDefinition);
}
