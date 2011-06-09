using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySpace.Common.Barf
{
	interface ITypeMetadata
	{
		Type Type { get; }
		int CurrentVersion { get; }
	}
}
