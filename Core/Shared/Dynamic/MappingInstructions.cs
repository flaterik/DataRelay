using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MySpace.Common.Dynamic
{
	/// <summary>
	/// Contains instructions for mapping objects of type <see cref="SourceType"/> to <see cref="DestinationType"/>.
	/// </summary>
	public class MappingInstructions
	{
		private List<IMapping> _mappings = new List<IMapping>();

		internal MappingInstructions(Type source, Type dest)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (dest == null) throw new ArgumentNullException("dest");

			SourceType = source;
			DestinationType = dest;
		}

		/// <summary>
		/// Gets the type of object to map from.
		/// </summary>
		/// <value>The type of object to map from.</value>
		public Type SourceType { get; private set; }
		/// <summary>
		/// Gets the type of object to map to.
		/// </summary>
		/// <value>The type of object to map to.</value>
		public Type DestinationType { get; private set; }

		/// <summary>
		/// Adds a mapping between two properties.
		/// </summary>
		/// <param name="sourceProperty">The source property to read from.</param>
		/// <param name="destinationProperty">The destination property to write to.</param>
		public void AddMapping(PropertyInfo sourceProperty, PropertyInfo destinationProperty)
		{
			if (sourceProperty == null) throw new ArgumentNullException("sourceProperty");
			if (destinationProperty == null) throw new ArgumentNullException("destinationProperty");

			_mappings.Add(new PropertyMapping(sourceProperty, destinationProperty));
		}

		/// <summary>
		/// Adds a mapping between a getter method and a setter method. <paramref name="destinationSetter"/>
		/// must have exactly one argument that is the same type as the return value of <paramref name="sourceGetter"/>
		/// </summary>
		/// <param name="sourceGetter">The source getter.</param>
		/// <param name="destinationSetter">The destination setter.</param>
		public void AddMapping(MethodInfo sourceGetter, MethodInfo destinationSetter)
		{
			if (sourceGetter == null) throw new ArgumentNullException("sourceGetter");
			if (destinationSetter == null) throw new ArgumentNullException("destinationSetter");

			_mappings.Add(new MethodMapping(sourceGetter, destinationSetter));
		}

		internal IEnumerable<IMapping> GetMappings()
		{
			return _mappings.ToArray<IMapping>();
		}
	}
}
