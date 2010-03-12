using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.DataRelay.Server.Common
{
    /// <summary>
    /// Class Represents a container for a <see cref="RelayMessage"/> and <see cref="RelayMessageProcessingContext"/>
    /// </summary>
    public class RelayMessageWithContext
    {
        #region DataMembers
        /// <summary>
        /// The <see cref="RelayMessage"/> that needs to be processed
        /// </summary>
        private readonly RelayMessage relayMessage;
        /// <summary>
        /// The <see cref="RelayMessageProcessingContext"/> associated to the <see cref="RelayMessage"/>
        /// </summary>
        private readonly RelayMessageProcessingContext processingContext;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor to create a <see cref="RelayMessageWithContext"/>
        /// </summary>
        /// <param name="relayMessage">The <see cref="RelayMessage"/> that needs to be processed</param>
        /// <param name="processingContext">The <see cref="RelayMessageProcessingContext"/> associated to the <see cref="RelayMessage"/></param>
        /// <exception cref="ArgumentNullException">All arguments for this constructor must be non-null</exception>
        public RelayMessageWithContext(RelayMessage relayMessage, RelayMessageProcessingContext processingContext)
        {
            if (relayMessage == null || processingContext == null)
            {
                throw new ArgumentNullException("RelayMessageWithContext consturctor requires non-null arguments");
            }
            this.relayMessage = relayMessage;
            this.processingContext = processingContext;
        }
        #endregion

        #region Getters/Setters
        /// <summary>
        /// Used to access the <see cref="RelayMessage"/> that needs to be processed
        /// </summary>
        public RelayMessage RelayMessage
        {
            get
            {
                return this.relayMessage;
            }            
        }
        /// <summary>
        /// Used to access the <see cref="RelayMessageProcessingContext"/> associated to the <see cref="RelayMessage"/> 
        /// </summary>
        public RelayMessageProcessingContext ProcessingContext
        {
            get
            {
                return this.processingContext;
            }          
        }
        #endregion
    }
}
