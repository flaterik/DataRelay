using System;
using MySpace.Common;

namespace MySpace.DataRelay.Client
{
	/// <summary>
	/// Optional arguments specified when finding and loading a given list of 
	/// <see cref="ICacheParameter"/> objects from the transport. 
	/// </summary>
	public class CacheGetArgs
	{
		/// <summary>
		/// Gets or sets the cache correction.  The default value is set to true.
		/// </summary>
		/// <value>A <see cref="Boolean"/> of true indicates that cache correction will be performed; 
		/// false indicates that cache correction will not be done.  Cache correction means the <see cref="ICacheParameter"/>
		/// objects retrieved that cause an <see cref="UnhandledVersionException"/> will be deleted.
		/// </value>
		public Boolean RectifyCorruptObjects { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="CacheGetArgs"/> class.
		/// </summary>
		public CacheGetArgs()
		{
			RectifyCorruptObjects = true;
		}
	}
}
