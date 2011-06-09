using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MySpace.Common.Barf
{
	/// <summary>
	/// Encapsulates arguments passed to <see cref="IBarfTester.AssertAreEqual"/>.
	/// </summary>
	public class AssertArgs
	{
		private static readonly Factory<string, bool> _traceOnce = Algorithm.LazyIndexer<string, bool>(partName =>
		{
			Trace.TraceWarning("Equality can't be asserted for Member=\"{0}\"", partName);
			return true;
		});

		private readonly NotSupportedBehavior _behavior;

		/// <summary>
		/// Initializes a new instance of the <see cref="AssertArgs"/> class.
		/// </summary>
		/// <param name="behavior">The behavior of the assertion method when fields that cannot be asserted are encountered.</param>
		public AssertArgs(NotSupportedBehavior behavior)
		{
			_behavior = behavior;
		}

		/// <summary>
		/// For use by generated code only. This is called internally to raise that a field can't be asserted.
		/// </summary>
		/// <param name="partName">The name of the serializable field that can't be asserted.</param>
		public void RaiseNotSupported(string partName)
		{
			switch (_behavior)
			{
				case NotSupportedBehavior.RaiseInconclusive:
					Assert.Inconclusive("Equality can't be asserted for Member=\"{0}\"", partName);
					break;
				case NotSupportedBehavior.TraceField:
					Trace.TraceWarning("Equality can't be asserted for Member=\"{0}\"", partName);
					break;
				case NotSupportedBehavior.TraceFieldOnce:
					_traceOnce(partName);
					break;
			}
		}
	}
}
