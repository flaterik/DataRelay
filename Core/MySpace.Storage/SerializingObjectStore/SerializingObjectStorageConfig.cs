using MySpace.ResourcePool;

namespace MySpace.Storage
{
	/// <summary>
	/// Initializing configuration for <see cref="SerializingObjectStorage"/>.
	/// To be passed to <see cref="SerializingObjectStorage.Initialize"/> and
	/// <see cref="SerializingObjectStorage.Reinitialize"/>.
	/// </summary>
	internal struct SerializingObjectStorageConfig
	{
		/// <summary>
		/// Gets or sets the memory stream pool to use for serialization.
		/// </summary>
		/// <value>The <see cref="MemoryStreamPool"/> to use.</value>
		public MemoryStreamPool StreamPool { get; set; }

		/// <summary>
		/// Gets or sets the underlying binary store to use.
		/// </summary>
		/// <value>The underlying <see cref="IBinaryStorage"/> to use.</value>
		public IBinaryStorage Storage { get; set; }
	}
}
