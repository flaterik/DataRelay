using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySpace.Common.Barf
{
	public class FillSettings
	{
		public FillSettings()
		{
			NullPeriod = 2;
			MinCollectionSize = 1;
			MaxCollectionSize = 20;
			MaxRecursionDepth = 1;
		}

		public NotSupportedBehavior NotSupportedBehavior { get; set; }
		public bool AllowNulls { get; set; }
		public uint NullPeriod { get; set; }
		public int MaxRecursionDepth { get; set; }

		public uint MinCollectionSize { get; set; }
		public uint MaxCollectionSize { get; set; }
	}
}
