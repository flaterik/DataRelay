using System;
using System.Collections.Generic;
using MySpace.DataRelay;
using MySpace.DataRelay.Common.Schemas;
using MySpace.DataRelay.Configuration;
using MySpace.DataRelay.RelayComponent.Forwarding;
using MySpace.DataRelay.Transports;

namespace MySpace.RelayComponent.Forwarding.Test
{
	/// <summary>
	/// Responsible for directing forwarder requests to <see cref="IRelayComponent"/>s.
	/// </summary>
	/// <remarks>
	///		<para>Any message sent using the forwarder that would have gotten sent across the network
	///		will get passed to the <see cref="IRelayComponent"/>s registered with this class.
	///		</para>
	/// </remarks>
	public class ComponentTestManager
	{
		private Forwarder _forwarder;
		private readonly List<IRelayComponent> _components = new List<IRelayComponent>();
		private RelayNodeConfig _config;
		private CreateTransportDelegate[] _createTransports;

		/// <summary>
		/// Initializes the <see cref="ComponentTestManager"/>, gets the <see cref="RelayNodeConfig"/>
		/// and Initializes the forwarder.
		/// </summary>
		/// <param name="createTransports">Optional, creates other
		/// <see cref="IRelayTransport"/>s to be added to the transports.</param>
		public void Initialize(params CreateTransportDelegate[] createTransports)
		{
			_createTransports = createTransports;
			TransportFactory.CreateTransportMethod = _createTransportDelegate;
			_forwarder = new Forwarder();  //we initialize because there's a static singleton we're wanting to get set.
			RelayNodeConfig config = RelayNodeConfig.GetRelayNodeConfig();
			if (config == null)
			{
				throw new InvalidOperationException("RelayNodeConfig not found.");
			}

			if (config.TransportSettings != null && config.TransportSettings.ListenPort != 0)
			{
				//do we fix for them?
				throw new InvalidOperationException("TransportSettings.ListenPort must be zero to make forwarder act like a client.");
			}
			_config = config;
			_forwarder.Initialize(config, null);
		}

		/// <summary>
		/// Shuts down the manager and the componenets.
		/// </summary>
		public void Shutdown()
		{
			if (_forwarder != null)
			{
				_forwarder.Shutdown();
				_forwarder = null;
			}
			foreach (var component in _components)
			{
				component.Shutdown();
			}
		}

		private IRelayTransport _createTransportDelegate(RelayNodeDefinition nodeDefinition,
			RelayNodeGroupDefinition groupDefinition)
		{
			var transport = new MockTransport(nodeDefinition, groupDefinition);
			transport.DoDispatchMessages = true;
			transport.MessageRecievedMethod = _receiveMessage;
			transport.MessageListRecievedMethod = _receiveMessageList;
			IRelayTransport[] otherTransports = null;
			if (_createTransports != null)
			{
				var count = _createTransports.Length;
				if (count > 0)
				{
					otherTransports = new IRelayTransport[count];
					for(var idx = 0; idx < count; ++idx)
					{
						otherTransports[idx] = _createTransports[idx](
							nodeDefinition, groupDefinition);
					}
				}
			}
			if (otherTransports == null)
			{
				return transport;
			}
			var transports = new List<IRelayTransport>();
			transports.AddRange(otherTransports);
			transports.Add(transport);
			return new RelayTransportStack(transports.ToArray());
		}

		private void _receiveMessage(RelayMessage message, RelayNodeDefinition nodeDefinition, RelayNodeGroupDefinition groupDefinition)
		{
			foreach (var component in _components)
			{
				component.HandleMessage(message);
			}
		}

		private void _receiveMessageList(IList<RelayMessage> messages, RelayNodeDefinition nodeDefinition, RelayNodeGroupDefinition groupDefinition)
		{
			foreach (var component in _components)
			{
				component.HandleMessages(messages);
			}
		}

		/// <summary>
		/// Registers and initializes the given <paramref name="component"/> to recieve messages.
		/// Must call <see cref="Initialize"/> first.
		/// </summary>
		/// <param name="component">The <see cref="IRelayComponent"/> to register.</param>
		private void _registerComponent(IRelayComponent component)
		{
			if (_config == null)
			{
				throw new InvalidOperationException("Must call initialize first");
			}
			component.Initialize(_config, null);
			_components.Add(component);
		}

		/// <summary>
		/// Registers the given <see cref="IRelayComponent"/>, and removes it upon disposing
		/// the returned <see cref="IDisposable"/> interface.
		/// </summary>
		/// <param name="component">The component to register.</param>
		/// <remarks>The removal is not thread safe, so all call must have completed before calling dispose.</remarks>
		/// <returns>The <see cref="IDisposable"/> interface that will remove the component on dispose.</returns>
		public IDisposable RegisterComponentScoped(IRelayComponent component)
		{
			_registerComponent(component);
			return new ComponentCleanup(this, component);
		}

		private class ComponentCleanup : IDisposable
		{
			private IRelayComponent _component;
			private ComponentTestManager _manager;

			public ComponentCleanup(ComponentTestManager manager, IRelayComponent component)
			{
				_component = component;
				_manager = manager;
			}

			#region IDisposable Members

			public void Dispose()
			{
				_manager._components.Remove(_component);	
			}

			#endregion
		}
	}
}
