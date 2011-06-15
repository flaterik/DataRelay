using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySpace.Common;

namespace MySpace.Storage.Cache
{
	/// <summary>
	/// Contains options for local cache operations.
	/// </summary>
	public struct LocalCacheOptions
	{
		/// <summary>
		/// Gets or sets the <see cref="DateTime"/> when the entry was
		/// last updated. Can be null.
		/// </summary>
		public DateTime? Updated { get; set; }

		/// <summary>
		/// Gets or sets the <see cref="ICollection{ObjectReference}"/> content dependencies.
		/// </summary>
		public IEnumerable<ObjectReference> ContentDependencies { get; set; }

		/// <summary>
		/// Gets or sets the <see cref="ICollection{ObjectReference}"/> existence dependencies.
		/// </summary>
		public IEnumerable<ObjectReference> ExistenceDependencies { get; set; }

		/// <summary>
		/// Gets or sets an object to supply a virtual cache type when there is no
		/// operation instance available.
		/// </summary>
		public IVirtualCacheType VirtualCacheObject { get; set; }

		/// <summary>
		/// Gets the instance for no options selected.
		/// </summary>
		/// <value>The no options <see cref="LocalCacheOptions"/>.</value>
		public static LocalCacheOptions None { get; private set; }

		static LocalCacheOptions()
		{
			None = new LocalCacheOptions();
		}
	}
}
