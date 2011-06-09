using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MySpace.Common.Dynamic
{
	[DebuggerDisplay("Count = {Count}")]
	internal class EvaluationStack : Stack<StackItem>
	{
		public void Push(Type type)
		{
			Push(type, LoadOptions.Default);
		}

		public void Push(Type type, LoadOptions loadOptions)
		{
			Push(new StackItem(type, loadOptions));
		}
	}
}
