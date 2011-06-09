using System;
using System.Linq;

namespace MySpace.Common.Barf
{
	public interface IBarfTester
	{
		void Fill(ref object instance, FillArgs args);
		void AssertAreEqual(object expected, object actual, AssertArgs args);
	}

	public interface IBarfTester<T> : IBarfTester
	{
		void Fill(ref T instance, FillArgs args);
		void AssertAreEqual(T expected, T actual, AssertArgs args);
	}
}
