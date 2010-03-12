using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.Common.Framework
{
    /// <summary>
    /// An enumeration that describes from whence an Entity came.
    /// </summary>
	public enum DataSource
	{
		Unknown,
		Database,
		Cache,
        CacheList,
		MessageQueue,
		CacheError,
		IdentityMap
	}
}
