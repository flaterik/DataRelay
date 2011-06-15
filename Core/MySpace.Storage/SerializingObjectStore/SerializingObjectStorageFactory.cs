using System;
using System.Xml;
using MySpace.Common.Configuration;
using MySpace.Common.HelperObjects;
using MySpace.Common.Storage;
using MySpace.ResourcePool;
using Serializer = MySpace.Common.IO.Serializer;

namespace MySpace.Storage
{
	/// <summary>
	/// Class that provides instances that implement <see cref="IObjectStorage"/> via
	/// serialization to an <see cref="IBinaryStorage"/>.
	/// </summary>
	/// <remarks>The generic parameter classes T and THeader used in
	/// <see cref="IObjectStorage.Get{T}(DataBuffer, StorageKey, T)"/> and
	/// <see cref="IObjectStorage.GetList{T, THeader}"/> et al must be serializable by
	/// <see cref="Serializer"/>.</remarks>
	public class SerializingObjectStorageFactory : GenericFactory<IObjectStorage>
	{
		private int? _memoryPoolInitialBufferSize;

		private GenericFactoryConfigurationSection<IBinaryStorage> _storageSection;

		/// <summary>
		/// Overriden. Obtains an instance from this factory.
		/// </summary>
		/// <returns>An <see cref="IObjectStorage"/> instance.</returns>
		public override IObjectStorage ObtainInstance()
		{
			var memoryPool = new MemoryStreamPool();
			if (_memoryPoolInitialBufferSize.HasValue)
			{
				memoryPool.InitialBufferSize = _memoryPoolInitialBufferSize.Value;
			}
			var storage = _storageSection.ObtainInstance();
			var ret = new SerializingObjectStorage();
			ret.Initialize(new SerializingObjectStorageConfig
           	{
				StreamPool = memoryPool,
           		Storage = storage
           	});
			return ret;
		}

		private const string _binaryStorageElementName = "binaryStorage";
		private const string _memoryPoolElementName = "memoryPool";

		/// <summary>
		/// 	<para>Overriden. Reads the factory configuration.</para>
		/// </summary>
		/// <param name="reader">
		/// 	<para>The <see cref="XmlReader"/> to read from.</para>
		/// </param>
		public override void ReadXml(XmlReader reader)
		{
			_memoryPoolInitialBufferSize = null;
			_storageSection = null;
			using (var subReader = reader.ReadSubtree())
			{
				subReader.Read();
				while (subReader.Read())
				{
					if (subReader.NodeType == XmlNodeType.Element)
					{
						switch (subReader.Name)
						{
							case _binaryStorageElementName:
								_storageSection = GenericFactoryConfigurationSection<IBinaryStorage>.FromXml(subReader);
								break;
							case _memoryPoolElementName:
								_memoryPoolInitialBufferSize =
									subReader.ReadElementContentAsInt();
								break;
						}
					}
				}
			}
			if (_storageSection == null)
			{
				throw new ApplicationException(
					"No binary storage configuration found");
			}
		}

		/// <summary>
		/// 	<para>Overriden. Writes the factory configuration.</para>
		/// </summary>
		/// <param name="writer">
		/// 	<para>The <see cref="XmlWriter"/> to write to.</para>
		/// </param>
		public override void WriteXml(XmlWriter writer)
		{
			if (_storageSection != null)
			{
				_storageSection.WriteXml(writer);
			}
			if (_memoryPoolInitialBufferSize.HasValue)
			{
				writer.WriteStartElement(_memoryPoolElementName);
				writer.WriteValue(_memoryPoolInitialBufferSize.Value);
				writer.WriteEndElement();
			}
		}
	}
}
