using System;

namespace BerkeleyDbWrapper
{
	[Flags]
	public enum GetOpFlags {
		Default = 0,
	}

	[Flags]
	public enum PutOpFlags {
		Default = 0,
	}

	[Flags]
	public enum DeleteOpFlags {
		Default = 0,
	}

	[Flags]
	public enum ExistsOpFlags {
		Default = 0,
	}
}