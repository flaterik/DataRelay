using System;
using System.Linq;

namespace MySpace.Common.IO
{
	internal static class SerializerHeaders
	{
		internal const byte NullVersion = 0xff;
		internal const byte AutoSerializable = 0xfe;
		internal const byte Barf = 0xfd;
		internal const byte MaxInlineSize = 0xf0;
		internal const int InlineHeaderSize = sizeof(byte);
	}
}
