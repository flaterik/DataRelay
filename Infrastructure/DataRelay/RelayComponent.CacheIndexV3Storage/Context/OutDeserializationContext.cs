using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context
{
    internal class OutDeserializationContext
    {
        #region Data members

        /// <summary>
        /// Gets or sets the total count.
        /// </summary>
        /// <value>The total count.</value>
        internal int TotalCount
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the unserialized cache index internal.
        /// </summary>
        /// <value>The unserialized cache index internal.</value>
        internal byte[] UnserializedCacheIndexInternal
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the filtered internal item list.
        /// </summary>
        /// <value>The filtered internal item list.</value>
        internal InternalItemList FilteredInternalItemList
        {
            get; set;
        }

        private int readItemCount = -1;
        /// <summary>
        /// Gets or sets the read item count.
        /// </summary>
        /// <value>The read item count.</value>
        internal int ReadItemCount
        {
            get
            {
                return readItemCount;
            }
            set
            {
                readItemCount = value;
            }
        }
        
        #endregion

        #region Ctors

        /// <summary>
        /// Initializes a new instance of the <see cref="OutDeserializationContext"/> class.
        /// </summary>
        internal OutDeserializationContext()
        {
            FilteredInternalItemList = new InternalItemList();
        }

        #endregion
    }
}