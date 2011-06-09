using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace MySpace.Common.Dynamic
{
	internal class PropertyMapping : IMapping
	{
		private readonly MethodMapping _innerMapping;

		internal PropertyMapping(PropertyInfo source, PropertyInfo dest)
		{
			var sourceGetter = source.GetGetMethod(true);
			if (sourceGetter == null)
			{
				throw new ArgumentException(string.Format("source property {0}.{1} is read-only", source.DeclaringType.Name, source.Name), "source");
			}

			var destSetter = dest.GetSetMethod(true);
			if (destSetter == null)
			{
				throw new ArgumentException(string.Format("dest property {0}.{1} does not have a setter", source.DeclaringType.Name, dest.Name), "dest");
			}

			_innerMapping = new MethodMapping(sourceGetter, destSetter);
		}

		public void GenerateMap(ILGenerator gen, int sourceArgIndex, int destArgIndex)
		{
			_innerMapping.GenerateMap(gen, sourceArgIndex, destArgIndex);
		}

		public PropertyInfo Source { get; private set; }
		public PropertyInfo Dest { get; private set; }
	}
}
