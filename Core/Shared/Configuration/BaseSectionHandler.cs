using System;
using System.Configuration;
using System.Xml;
using System.Xml.Serialization;

namespace MySpace.Common.Configuration
{
	/// <summary>
	/// 	<para>Base class for <see cref="IConfigurationSectionHandler"/> implementations.</para>
	/// </summary>
	/// <typeparam name="TImplementation">
	///		<para>The non-abstract descendent of this class.</para>
	///		<para>This class must have the attribute <see cref="SectionHandlerAttribute"/>.</para>
	/// </typeparam>
	/// <typeparam name="TConfigType">The type of the config type. Must be xml serializable.</typeparam>
	public abstract class BaseSectionHandler<TImplementation, TConfigType> : IConfigurationSectionHandler
		where TImplementation : BaseSectionHandler<TImplementation, TConfigType>
		where TConfigType : class
	{
		private static readonly XmlSerializer _serializer = new XmlSerializer(typeof(TConfigType));
		private static string _sectionNameValue;
		private static readonly object _sectionNameLock = new object();

		private static string _sectionName
		{
			get
			{
				if (_sectionNameValue != null) return _sectionNameValue;
				lock (_sectionNameLock)
				{
					if (_sectionNameValue != null) return _sectionNameValue;
					object[] attributes =
						typeof(TImplementation).GetCustomAttributes(typeof(SectionHandlerAttribute), false);
					if (attributes != null && attributes.Length > 0)
					{
						SectionHandlerAttribute attribute = (SectionHandlerAttribute)attributes[0];
						_sectionNameValue = attribute.Name;
					}
					else
					{
						throw new ApplicationException(string.Format(
						                               	"{0} must the {1} attribute applied.",
						                               	typeof(TImplementation),
						                               	typeof(SectionHandlerAttribute)));
					}
				}
				return _sectionNameValue;
			}
		}

		/// <summary>
		/// Gets the <typeparamref name="TConfigType"/> config object.
		/// </summary>
		/// <param name="throwOnError">if set to <see langword="true"/> [throw on error].</param>
		/// <param name="forceRefresh"><see langword="true"/> to refresh the cached copy, if there is one.</param>
		/// <returns>
		///	<para>The config object. <see langword="null"/> if <paramref name="throwOnError"/>
		///	is <see langword="false"/> and the config could not be loaded.</para>
		/// </returns>
		/// <exception cref="ApplicationException">
		///	<para>Thrown when <paramref name="throwOnError"/> is <see langword="true"/>
		///	and the configuration object could not be loaded properly.</para>
		/// </exception>
		public static TConfigType GetConfig(bool throwOnError, bool forceRefresh)
		{
			TConfigType result = default(TConfigType);
			try
			{
				if (forceRefresh) ConfigurationManager.RefreshSection(_sectionName);
				result = (TConfigType)ConfigurationManager.GetSection(_sectionName);
			}
			catch (Exception x)
			{
				if (throwOnError)
				{
					throw new ApplicationException(
						"Exception encountered when opening the desired configuration section", x);
				}
			}

			if (typeof(TConfigType).IsClass && ReferenceEquals(result, null))
			{
				if (throwOnError)
				{
					throw new ApplicationException(string.Format(
					                               	"Config {0} was not found",
					                               	_sectionName));
				}
			}

			return result;
		}

		#region IConfigurationSectionHandler Members

		/// <summary>
		/// Creates a configuration section handler.
		/// </summary>
		/// <param name="parent">Parent object.</param>
		/// <param name="configContext">Configuration context object.</param>
		/// <param name="section">Section XML node.</param>
		/// <returns>The created section handler object.</returns>
		public object Create(object parent, object configContext, XmlNode section)
		{
			return _serializer.Deserialize(new XmlNodeReader(section));
		}

		#endregion
	}
}