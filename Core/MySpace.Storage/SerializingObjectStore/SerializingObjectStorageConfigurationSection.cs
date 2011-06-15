using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySpace.Common.Configuration;

namespace MySpace.Storage.Configuration
{
	/// <summary>
	/// Configuration class for <see cref="SerializingObjectStorage"/>. Defined for convenience,
	/// to avoid lengthy generic class specifications in configuration files.
	/// </summary>
	public class SerializingObjectStorageConfigurationSection :
		GenericFactoryConfigurationSection<IObjectStorage> { }
}
