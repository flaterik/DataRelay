using System;
using System.Linq;

namespace MySpace.Common.Barf
{
	/// <summary>
	/// A domain object that implements this interface can deserialize future
	/// versions of the object and still serialize to the same future version.
	/// </summary>
	public interface IForwardSerializable
	{
		/// <summary>
		/// Gets or sets the future data.
		/// </summary>
		/// <value>The future data.</value>
		object FutureData { get; set; }
	}
}
