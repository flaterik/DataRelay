using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BerkeleyDbWrapper
{
	/// <summary>
	/// Specifies the size and multiplicity of a cache.
	/// </summary>
	public class CacheSize
	{
		// Fields
		private readonly uint _bytes;
		private readonly uint _gbytes;
		private readonly int _ncache;

		// Methods
		/// <summary>
		/// Initializes a new instance of the <see cref="CacheSize"/> class.
		/// </summary>
		/// <param name="gbytes">The number of gigabytes.</param>
		/// <param name="bytes">The number of bytes.</param>
		/// <param name="ncache">The multiplicity of the cache.</param>
		public CacheSize(uint gbytes, uint bytes, int ncache)
		{
			_gbytes = gbytes;
			_bytes = bytes;
			_ncache = ncache;
		}

		/// <summary>
		/// Gets the number of bytes.
		/// </summary>
		/// <returns>The <see cref="UInt32"/> number of bytes.</returns>
		public uint GetBytes()
		{
			return _bytes;
		}

		/// <summary>
		/// Gets the number of gigabytes.
		/// </summary>
		/// <returns>The <see cref="UInt32"/> number of gigabytes.</returns>
		public uint GetGigaBytes()
		{
			return _gbytes;
		}

		/// <summary>
		/// Gets the cache multiplicity.
		/// </summary>
		/// <returns>The <see cref="Int32"/> number of caches.</returns>
		public int GetNumCaches()
		{
			return _ncache;
		}
	}
}
