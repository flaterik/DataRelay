using System;
using System.Linq;

namespace MySpace.Common.Barf
{
	internal interface IPartBuilder
	{
		void GenerateSerializePart(GenSerializeContext context);
		void GenerateDeserializePart(GenDeserializeContext context);
		void GenerateFillPart(GenFillContext context);
		void GenerateAssertAreEqualPart(GenAssertAreEqualContext context);
	}
}
