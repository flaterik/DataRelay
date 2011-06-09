using System;
using System.Linq;

namespace MySpace.Common.Barf
{
	internal interface IPartResolver
	{
		int FrameworkVersion { get; }
		IPartBuilder GetPartBuilder(Type type, PartDefinition partDefinition);
	}
}
