using System;
using System.Reflection;
using MySpace.Common.Dynamic;
using MySpace.Common.IO;

namespace MySpace.Common.Barf
{
	internal abstract class GenContext
	{
		protected GenContext(
			MethodGenerator generator)
		{
			Generator = generator;
		}

		public MethodGenerator Generator { get; private set; }
	}
}
