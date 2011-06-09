using System;
using System.Linq;

namespace MySpace.Common.Barf
{
	internal static class PartResolver
	{
		private static readonly IPartResolver _default = new DefaultPartResolver(1);

		public static IPartResolver Default
		{
			get { return _default; }
		}

		public static IPartResolver Current
		{
			get { return _default; }
		}
	}
}
