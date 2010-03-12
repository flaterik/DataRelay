#pragma once
#include "stdafx.h"

using namespace System;

namespace BerkeleyDbWrapper
{
	[FlagsAttribute]
	public enum class GetOpFlags {
		Default = 0,
	};

	[FlagsAttribute]
	public enum class PutOpFlags {
		Default = 0,
	};

	[FlagsAttribute]
	public enum class DeleteOpFlags {
		Default = 0,
	};

	[FlagsAttribute()]
	public enum class ExistsOpFlags {
		Default = 0,
	};
}