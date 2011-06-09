using System;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection;
using MySpace.Common.HelperObjects;
using MySpace.DataRelay.Common.Schemas;
using MySpace.DataRelay.Configuration;
using MySpace.Storage.Cache;
using MySpace.Common.Storage;
using System.Xml;

namespace MySpace.DataRelay.Client.Storage
{
	/// <summary>
	/// Provides instances that implement <see cref="ITypePolicy"/> using
	/// the relay type settings.
	/// </summary>
	public class RelayTypePolicyFactory : GenericFactory<ITypePolicy>
	{
		/// <summary>
		/// 	<para>Overriden. Obtains an instance from this factory.</para>
		/// </summary>
		/// <returns>
		/// 	<para>An <see cref="ITypePolicy"/> instance.</para></returns>
		public override ITypePolicy ObtainInstance()
		{
			ITypePolicy ret = new RelayTypePolicy(Assembly.GetCallingAssembly());
			ret.Initialize(null);
			return ret;
		}

		/// <summary>
		/// 	<para>Overriden. Reads the factory configuration.</para>
		/// </summary>
		/// <param name="reader">
		/// 	<para>The <see cref="XmlReader"/> to read from.</para>
		/// </param>
		public override void ReadXml(XmlReader reader)
		{
		}

		/// <summary>
		/// 	<para>Overriden. Writes the factory configuration.</para>
		/// </summary>
		/// <param name="writer">
		/// 	<para>The <see cref="XmlWriter"/> to write to.</para>
		/// </param>
		public override void WriteXml(XmlWriter writer)
		{
		}

		class RelayTypePolicy : ITypePolicy
		{
			private Assembly _baseAssembly;

			private static readonly Type _objectDependencyType =
				typeof(ObjectDependency);

			private const int _objectDependencyTtl = 3600;

			public RelayTypePolicy(Assembly baseAssembly)
			{
				_baseAssembly = baseAssembly;
			}

			private static Type GetTypeFromAssembly(List<string> names,
				Assembly assembly, string name)
			{
				var asmName = assembly.GetName().FullName;
				if (names.Contains(asmName)) return null;
				names.Add(asmName);
				var type = assembly.GetType(name);
				if (type != null) return type;
				foreach (var assemblyName in assembly.GetReferencedAssemblies())
				{
                    try
                    {
                        var assemblyChild = Assembly.Load(assemblyName);
                        type = GetTypeFromAssembly(names, assemblyChild, name);
                        if (type != null) return type;
                    }
                    catch(System.IO.FileNotFoundException)
                    {
                        // ignore FileNotFound exceptions because a referenced assembly 
                        // doesn't necessarily need to be present if no code from it is
                        // ever called.
                    }
                    catch(BadImageFormatException)
                    {
                        // ignore BadImageFormat exceptions because some of
                        // our MySpace assemblies may reference win32, but never actually
                        // use them (in the case of Managed Lib, for example).
                    }
				}
				return null;
			}

			private TypeDescription CreateDescription(TypeSetting setting, Type type)
			{
				if (type == null)
				{
					if (!string.IsNullOrEmpty(setting.AssemblyQualifiedTypeName))
					{
						type = Type.GetType(setting.AssemblyQualifiedTypeName);
					}
					else
					{
						type = GetTypeFromAssembly(new List<string>(),
							_baseAssembly, setting.TypeName);
					}
				}
				return new TypeDescription(type, setting.TypeId,
					setting.LocalCacheTTLSeconds ?? -1);
			}

			TypeDescription GetDescriptionCore(DataBuffer keySpace, Type type)
			{
				if (_baseAssembly == null) throw new ObjectDisposedException("policy");
				TypeSetting setting;
				switch (keySpace.Type)
				{
					case DataBufferType.String:
						setting = RelayClient.Instance.GetTypeSetting(keySpace.StringValue);
						break;
					case DataBufferType.Int32:
						setting = RelayClient.Instance.GetTypeSetting(keySpace.Int32Value);
						break;
					default:
						throw new ArgumentException(string.Format("Type {0} not supported",
							keySpace.Type), "keySpace");
				}
				if (setting == null) return TypeDescription.NotFound;
				return CreateDescription(setting, type);
			}

			TypeDescription ITypePolicy.GetDescription(DataBuffer keySpace)
			{
				return GetDescriptionCore(keySpace, null);				
			}

			TypeDescription ITypePolicy.GetDescription(Type type)
			{
				if (type == null) throw new ArgumentNullException("type");
				if (type.Equals(_objectDependencyType))
				{
					return new TypeDescription(_objectDependencyType,
						RelayClient.Instance.MaximumTypeId + 1, _objectDependencyTtl);
				}
				return GetDescriptionCore(type.FullName, type);
			}

			void ITypePolicy.Initialize(object config)
			{
			}

			void IDisposable.Dispose()
			{
				_baseAssembly = null;
			}
		}
	}
}