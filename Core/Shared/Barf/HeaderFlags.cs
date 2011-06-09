using System;
using System.Linq;

namespace MySpace.Common.Barf
{
	[Flags]
	public enum HeaderFlags : byte
	{
		None = 0,
		HasNameTable = 1
	}
}
