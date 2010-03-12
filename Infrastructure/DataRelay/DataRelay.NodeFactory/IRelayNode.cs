using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.DataRelay
{
    /// <summary>
    /// Provides a lifetime and configuration interface for a Relay Transport Node server container.
    /// </summary>
	public interface IRelayNode
	{
        /// <summary>
        /// Initializes the server with the givne <see cref="ComponentRunState"/> array.
        /// </summary>
        /// <param name="runStates">The <see cref="ComponentRunState"/> to use.</param>
		void Initialize(ComponentRunState[] runStates);

		/// <summary>
		/// Gets the <see cref="ComponentRunState"/>s for the currently loaded components.
		/// </summary>
		/// <returns>An array of <see cref="ComponentRunState"/></returns>
		ComponentRunState[] GetComponentRunStates();

		/// <summary>
		/// Gets the <see cref="ComponentRuntimeInfo"/> for the currently running components.
		/// </summary>
		/// <returns></returns>
        ComponentRuntimeInfo[] GetComponentsRuntimeInfo();

		/// <summary>
		/// Gets the <see cref="ComponentRuntimeInfo"/> for the requested component.
		/// </summary>
		/// <param name="componentName">The name of the component to get the status for</param>
		/// <returns>A <see cref="ComponentRuntimeInfo"/></returns>
		ComponentRuntimeInfo GetComponentRuntimeInfo(string componentName);

        /// <summary>
        /// Starts the node server, all components and the transport to accept new requests.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the node server, components and transport.
        /// </summary>
        void Stop();
	}
}
