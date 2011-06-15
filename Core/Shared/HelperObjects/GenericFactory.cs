using System.Xml;
using System.Xml.Serialization;
using System.Xml.Schema;

namespace MySpace.Common.HelperObjects
{
	/// <summary>
	/// Abstract base class for factories.
	/// </summary>
	/// <typeparam name="T">The type of the instance the factory provides.</typeparam>
	public abstract class GenericFactory<T> : IXmlSerializable where T : class
	{
		/// <summary>
		/// Obtains an instance from this factory.
		/// </summary>
		/// <returns>A <typeparamref name="T"/> instance.</returns>
		public abstract T ObtainInstance();

		/// <summary>
		/// Obtains the schema of the factory configuration.
		/// </summary>
		/// <returns>The <see cref="XmlSchema"/> that describes the
		/// factory configuration.</returns>
		public virtual XmlSchema GetSchema()
		{
			return null;
		}

		/// <summary>
		/// Reads the factory configuration.
		/// </summary>
		/// <param name="reader">The <see cref="XmlReader"/> to read from.</param>
		public abstract void ReadXml(XmlReader reader);

		/// <summary>
		/// Writes the factory configuration.
		/// </summary>
		/// <param name="writer">The <see cref="XmlWriter"/> to write to.</param>
		public abstract void WriteXml(XmlWriter writer);
	}
}
