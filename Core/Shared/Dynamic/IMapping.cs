using System;
using System.Linq;
using System.Reflection.Emit;

namespace MySpace.Common.Dynamic
{
	internal interface IMapping
	{
		void GenerateMap(ILGenerator gen, int sourceArgIndex, int destArgIndex);
	}
}
