using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySpace.Common.Dynamic
{
	public class StackValidationException : ApplicationException
	{
		internal StackValidationException(string message)
			: base(message)
		{
		}
	}
}
