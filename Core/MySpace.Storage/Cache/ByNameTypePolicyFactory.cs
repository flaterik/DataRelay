using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using MySpace.Common.HelperObjects;
using MySpace.Common.Storage;
using MySpace.Storage.Cache;
using System.Xml;

namespace MySpace.Storage.Cache
{
	/// <summary>
	/// Provides implementation of <see cref="ITypePolicy"/> using
	/// full class names as the key space id.
	/// </summary>
	public class ByNameTypePolicyFactory : GenericFactory<ITypePolicy>
	{
		/// <summary>
		/// 	<para>Overriden. Obtains an instance from this factory.</para>
		/// </summary>
		/// <returns>
		/// 	<para>An instance.</para></returns>
		public override ITypePolicy ObtainInstance()
		{
			var ret = new MockTestPolicy();
			ret.Initialize(_ttlDuration);
			return ret;
		}

		private const int defaultTtlDuration = 100;
		private int _ttlDuration = defaultTtlDuration;

		/// <summary>
		/// Gets the TTL duration in seconds.
		/// </summary>
		public int TtlDuration { get { return _ttlDuration; } }

		/// <summary>
		/// 	<para>Overriden. Reads the factory configuration.</para>
		/// </summary>
		/// <param name="reader">
		/// 	<para>The <see cref="XmlReader"/> to read from.</para>
		/// </param>
		public override void ReadXml(XmlReader reader)
		{
			if (!int.TryParse(reader.ReadString(), out _ttlDuration))
			{
				_ttlDuration = defaultTtlDuration;
			}
		}

		/// <summary>
		/// Gets or sets additional assemblies to check for types from
		/// type name.
		/// </summary>
		public static Assembly[] AdditionalAssemblies { get; set; }

		/// <summary>
		/// 	<para>Overriden. Writes the factory configuration.</para>
		/// </summary>
		/// <param name="writer">
		/// 	<para>The <see cref="XmlWriter"/> to write to.</para>
		/// </param>
		public override void WriteXml(XmlWriter writer)
		{
			writer.WriteString(_ttlDuration.ToString());
		}

		class MockTestPolicy : ITypePolicy
		{
			private int _ttlDuration = defaultTtlDuration;

			#region ITypePolicy Members

			public void Initialize(object config)
			{
				_ttlDuration = (int)config;
			}

			public TypeDescription GetDescription(Type type)
			{
				return new TypeDescription(type, type.FullName, _ttlDuration);
			}

			public TypeDescription GetDescription(DataBuffer keySpace)
			{
				var name = keySpace.StringValue;
				var type = Type.GetType(name);
				if (type == null && AdditionalAssemblies != null)
				{
					foreach(var asm in AdditionalAssemblies)
					{
						type = asm.GetType(name);
						if (type != null) break;
					}
				}
				if (type != null)
				{
					return GetDescription(type);
				}
				return new TypeDescription(null, keySpace, _ttlDuration);
			}

			#endregion

			#region IDisposable Members

			public void Dispose()
			{
			}

			#endregion
		}
	}
}
