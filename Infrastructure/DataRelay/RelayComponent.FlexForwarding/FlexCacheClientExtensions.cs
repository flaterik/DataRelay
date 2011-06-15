using System;
using System.IO;
using System.Net.Mime;
using MySpace.Common;
using MySpace.FlexCache;

namespace MySpace.DataRelay.RelayComponent.FlexForwarding
{
	internal static class FlexCacheClientExtensions
	{
		public static Future GetFuture(this FlexCacheClient client, RelayMessage message)
		{
			switch (message.GetMessageActionType())
			{
				case MessageActionType.Put:
                    return client.Put(message.GetKeySpace(), message.GetKey(), new MemoryStream(message.Payload.ByteArray), new ContentType(FlexCache.MediaTypeNames.Auto));

				case MessageActionType.Delete:
					return client.Delete(message.GetKeySpace(), message.GetKey());

				case MessageActionType.Get:
					return client.Get(message.GetKeySpace(), message.GetKey());
			}

			throw new NotSupportedException("Message type is not supported by Flex Cache.");
		}
	}
}
