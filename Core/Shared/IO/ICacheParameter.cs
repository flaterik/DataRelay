using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common.Framework;

namespace MySpace.Common
{
	/// <summary>
	/// Defines parameters that objects can supply to become self caching.
	/// </summary>
	public interface ICacheParameter
	{
		int PrimaryId { get; set; }

		/// <summary>
		/// Source of the object (Cache vs. Database).
		/// </summary>
		DataSource DataSource { get; set; }

		/// <summary>
		/// If shared is empty.
		/// </summary>
		bool IsEmpty { get; set; }
	}

	/// <summary>
	/// Defines an additional cache parameter for using a string as an extended cache key. Use EITHER this or IExtendedCacheParameter, not both.
	/// </summary>
	public interface IExtendedCacheParameter : ICacheParameter
	{
		/// <summary>
		/// A string used to identiy the object when an integer is insufficient. 
		/// </summary>
        string ExtendedId { get; set; }
		
        /// <summary>
        /// If this is not null, on input it will be used in place of DateTime.Now. On output, it will be populated by the server's recorded LastUpdatedDate.
        /// </summary>
		DateTime? LastUpdatedDate { get; set; }
	}

    /// <summary>
    /// Defines an additional cache parameter for using a btte[] as an extended cache key. Use EITHER this or IExtendedCacheParameter, not both.
    /// </summary>
    public interface IExtendedRawCacheParameter : ICacheParameter
    {
        /// <summary>
        /// A byte array used to identiy the object when an integer is insufficient. 
        /// </summary>
        byte[] ExtendedId { get; set; }

        /// <summary>
        /// If this is not null, on input it will be used in place of DateTime.Now. On output, it will be populated by the server's recorded LastUpdatedDate.
        /// </summary>
        DateTime? LastUpdatedDate { get; set; }
    }

	/// <summary>
	/// Defines additional cache parameters for specifying the cache type manually
	/// instead of deriving from object type.  The cache type MUST exist RelayTypeSettings.config
	/// and should only be used when caching the same type of objects in many places.
	/// </summary>
    public interface IVirtualTypeCacheParameter : ICacheParameter, IVirtualCacheType
	{
		
	}

    /// <summary>
    /// Optional overrides default cache type with customer tye name.
    /// As opposed to deriving from the object type.  The cache type MUST exist RelayTypeSettings.config
    /// and should only be used when caching the same type of objects in different places.
    /// </summary>
    /// <remarks>ME: I do not agree on the set abilitiy</remarks>
    public interface IVirtualCacheType {
        string CacheTypeName
        {
            get;
            //[Obsolete("There is no need to set the CacheTypeName, the value should be set by the requester on the query or cache data")]
            set;
        }
    }

    
}
