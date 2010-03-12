using System.Net;
using System.Collections.Generic;
namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context
{
    internal class MessageContext
    {
        #region Data Members

        /// <summary>
        /// Gets or sets the type id.
        /// </summary>
        /// <value>The type id.</value>
        public short TypeId
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the primary id.
        /// </summary>
        /// <value>The primary id.</value>
        public int PrimaryId
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the extended id.
        /// </summary>
        /// <value>The extended id.</value>
        public byte[] ExtendedId
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the source zone.
        /// </summary>
        /// <value>The source zone.</value>
        public ushort SourceZone
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the relay TTL.
        /// </summary>
        /// <value>The relay TTL.</value>
        public short RelayTTL
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the address history.
        /// </summary>
        /// <value>The address history.</value>
        public List<IPAddress> AddressHistory
        {
            get; set;
        }

        #endregion
    }
}