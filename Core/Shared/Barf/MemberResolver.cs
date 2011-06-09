using System;
using System.Linq;
using System.Reflection;
using MySpace.Common.IO;

namespace MySpace.Common.Barf
{
	internal static class MemberResolver
	{
		public static MethodInfo GetReadMethod(Type type)
		{
			return GetReadMethod("Read" + type.Name, type);
		}

		public static MethodInfo GetReadMethod(string name, Type type)
		{
			// todo - cache
			return typeof(IPrimitiveReader).GetMethod(name);
		}

		public static MethodInfo GetWriteMethod(Type type)
		{
			return GetWriteMethod("Write", type);
		}

		public static MethodInfo GetWriteMethod(string name, Type type)
		{
			// todo - cache
			return typeof(IPrimitiveWriter).GetMethod(name, new[] { type });
		}
	}
}
