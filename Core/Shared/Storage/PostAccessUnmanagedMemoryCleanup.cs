using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;

namespace MySpace.Common.Storage
{
	/// <summary>
	/// Delegate to encapsulate the clean up of a block of unmanaged memory.
	/// </summary>
	/// <param name="pointer">Pointer to the unmanaged memory block.</param>
	[SuppressUnmanagedCodeSecurity]
	public unsafe delegate void PostAccessUnmanagedMemoryCleanup(void* pointer);
}
