using System;
using System.Linq;

namespace MySpace.Common.Barf
{
	public static class BarfErrors
	{
		public static void RaiseAbstractConstructionError(Type type)
		{
			throw new InvalidOperationException("Invalid attempt to construct an abstract type occurred while filling or deserializing. Type=" + type.FullName);
		}
	}
}
