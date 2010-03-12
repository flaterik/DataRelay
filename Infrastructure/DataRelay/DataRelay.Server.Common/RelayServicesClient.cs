using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.DataRelay.Server.Common
{
    /// <summary>
    /// Serverside modules (ie, an <see cref="IRelayComponent"/>) can use this 
    /// Singleton class to consume a variety of services.  
    /// Currently, it provides services exposed by <see cref="IRelayNodeServices"/>. 
    /// This class
    /// will serve as a container for future server side services.
    /// </summary>
    public class RelayServicesClient
    {
        #region Fields
        /// <summary>
        /// The Singleton instance
        /// </summary>
        private static RelayServicesClient instance = new RelayServicesClient();
      
        /// <summary>
        /// Used to consume services exposed by <see cref="IRelayNodeServices"/>
        /// </summary>
        private IRelayNodeServices relayNodeServices;
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes the <see cref="RelayServicesClient"/>.
        /// </summary>
        private RelayServicesClient()
        {
        }
        #endregion

        #region Instance

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static RelayServicesClient Instance
        {
            get
            {
                return instance;
            }
        }

        #endregion

        #region Setter/Getter
        /// <summary>
        /// Used to access <see cref="IRelayNodeServices"/>
        /// </summary>
        public IRelayNodeServices RelayNodeServices
        {
            get
            {
                return this.relayNodeServices;
            }
            set
            {
                this.relayNodeServices = value;
            }
        }
        #endregion
    }
}
