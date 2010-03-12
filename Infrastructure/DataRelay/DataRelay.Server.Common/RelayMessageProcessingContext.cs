using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.DataRelay.Server.Common
{
    /// <summary>
    /// This class is contained with in <see cref="RelayMessageWithContext"/>. This class serves as a container for processing
    /// instructions that will be used to process a <see cref="RelayMessage"/>
    /// </summary>
    public class RelayMessageProcessingContext
    {
        #region DataMemebers
        /// <summary>
        /// Array stores the components that should not receive the associated <see cref="RelayMessage"/>
        /// </summary>
        readonly private Type[] exclusionComponentList;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor allows users to set an exclusionComponentList which will prevent the associated <see cref="RelayMessage"/> 
        /// from being sent to any component on the list.
        /// </summary>
        /// <param name="exclusionComponentList">Type array contains the components that should not receive the associated <see cref="RelayMessage"/>
        /// Passing null for this parameter is allowed.
        /// </param>
        public RelayMessageProcessingContext(Type[] exclusionComponentList)
        {
            this.exclusionComponentList = exclusionComponentList;
        }
        #endregion

        #region Getters/Setters
        /// <summary>
        /// Used to access the array of components that should not receive the associated <see cref="RelayMessage"/>
        /// </summary>
        public Type[] ExclusionComponentList
        {
            get
            {
                return this.exclusionComponentList;
            }            
        }
        #endregion

    }
}
