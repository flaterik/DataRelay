using System;
using System.Linq;

namespace MySpace.DataRelay.RelayComponent.FlexForwarding
{
	internal enum MessageActionType
	{
		Put,
		Get,
		Delete,
		Unsupported
	}

	internal static class RelayMessageExtensions
	{
		public static string GetKeySpace(this RelayMessage message)
		{
			return "RelayType_" + message.TypeId;
		}

		public static string GetKey(this RelayMessage message)
		{
			if (message.ExtendedId != null) return Convert.ToBase64String(message.ExtendedId);

			return message.Id.ToString();
		}

		public static MessageActionType GetMessageActionType(this RelayMessage message)
		{
			switch (message.MessageType)
			{
				case MessageType.Undefined:
				case MessageType.DeleteInAllTypes:
				case MessageType.DeleteAllInType:
				case MessageType.DeleteAll:
				case MessageType.DeleteAllInTypeWithConfirm:
				case MessageType.DeleteAllWithConfirm:
				case MessageType.DeleteInAllTypesWithConfirm:
				case MessageType.Increment:
				case MessageType.IncrementWithConfirm:
				case MessageType.NotificationWithConfirm:
				case MessageType.Notification:
				case MessageType.Query:
				case MessageType.Invoke:
				case MessageType.NumTypes:
					return MessageActionType.Unsupported;

				case MessageType.Get:
					return MessageActionType.Get;

				case MessageType.Save:
				case MessageType.Update:
				case MessageType.SaveWithConfirm:
				case MessageType.UpdateWithConfirm:
					return MessageActionType.Put;

				case MessageType.Delete:
				case MessageType.DeleteWithConfirm:
					return MessageActionType.Delete;
			}
			return MessageActionType.Unsupported;
		}
	}
}
