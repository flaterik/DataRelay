using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.DataRelay
{
    public enum MessageType
    {
        Undefined,
        Get,
        Save,
        Delete,
        DeleteInAllTypes,
        DeleteAllInType,
        DeleteAll,
        Update,
        Query,
        Invoke,
        Notification,
		Increment,
		SaveWithConfirm,
		UpdateWithConfirm,
		DeleteWithConfirm,
		DeleteAllInTypeWithConfirm,
		DeleteAllWithConfirm,
		DeleteInAllTypesWithConfirm,
		NotificationWithConfirm,
		IncrementWithConfirm,
        NumTypes        // This must always be the last item
    }
}
