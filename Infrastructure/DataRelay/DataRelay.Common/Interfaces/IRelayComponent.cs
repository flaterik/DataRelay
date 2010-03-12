using System;
using System.Collections.Generic;
using System.Text;
using MySpace.DataRelay.Configuration;


namespace MySpace.DataRelay
{
    /// <summary>
    /// Represents a loadable Relay Component and is implemented to provide new functionality 
    /// into the relay transport.
    /// </summary>
	public interface IRelayComponent : IDataHandler
	{
        /// <summary>
        /// Returns a unique human readable component name.  This name MUST match the name used
		/// in the component config file.
        /// </summary>
        /// <returns>The name of the component.</returns>
		string GetComponentName();
		
        /// <summary>
        /// Reloads the configuration from the given <see cref="RelayNodeCofig"/> and applies the new settings.
        /// </summary>
        /// <param name="config">The given <see cref="RelayNodeConfig"/>.</param>
		void ReloadConfig(RelayNodeConfig config);
		

        ComponentRunState GetRunState();

		ComponentRuntimeInfo GetRuntimeInfo();
		
        /// <summary>
        /// Initializes and starts the component.
        /// </summary>
        /// <param name="config">The configuration to use.</param>
        /// <param name="runState"></param>
        void Initialize(RelayNodeConfig config, ComponentRunState runState);
		
        /// <summary>
        /// Stops the component and releases it's resources.
        /// </summary>
        void Shutdown();
		
	}
}
