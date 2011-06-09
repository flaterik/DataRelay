using System;
using System.Linq;

namespace MySpace.Common.Dynamic
{
	internal static class StackAssert
	{
		private static string GetExceptionMessage(StackItem actual, StackItem expected, string typeComparePhrase)
		{
			return string.Format(
				"An unexpected item with ItemType=\"{0}\" and Type=\"{1}\" was found on the evaluation stack.  However an item with ItemType=\"{2}\" and a type {3} Type=\"{4}\" was expected.",
				actual.ItemType,
				actual.Type.Name,
				expected.ItemType,
				typeComparePhrase,
				expected.Type.Name);
		}

		public static void AreEqual(StackItem actual, Type expected)
		{
			AreEqual(actual, new StackItem(expected));
		}

		public static void AreEqual(StackItem actual, StackItem expected)
		{
			const string phrase = "equal to";

			if (expected.ItemType != actual.ItemType)
			{
				throw new StackValidationException(GetExceptionMessage(actual, expected, phrase));
			}

			if (expected.Type != actual.Type)
			{
				throw new StackValidationException(GetExceptionMessage(actual, expected, phrase));
			}
		}

		public static void IsPrimitive(StackItem actual)
		{
			var type = actual.Type.IsEnum ? Enum.GetUnderlyingType(actual.Type) : actual.Type;

			if (actual.ItemType != ItemType.Value && !type.IsPrimitive)
			{
				throw new StackValidationException("Expected a primitive value on the evaluation stack but got " + actual);
			}
		}

		public static void IsAssignable(StackItem actual, Type expected, bool permitNull)
		{
			IsAssignable(actual, new StackItem(expected), permitNull);
		}

		public static void IsAssignable(StackItem actual, StackItem expected, bool permitNull)
		{
			const string phrase = "assignable to";

            if (permitNull
                && expected.ItemType == ItemType.Reference
                && actual.Type.IsPrimitive
                && GetPrimitiveType(actual.Type) == PrimitiveType.I4)
            {
                // assigning null to a reference type
                return;
            }

			if (expected.ItemType != actual.ItemType)
			{
				throw new StackValidationException(GetExceptionMessage(actual, expected, phrase));
			}

			if (expected.Type.IsPrimitive && actual.Type.IsPrimitive)
			{
				if (!ArePrimitivesCompatible(actual.Type, expected.Type))
				{
					throw new StackValidationException(GetExceptionMessage(actual, expected, phrase));
				}
			}
			else if (expected.Type.IsEnum && actual.Type.IsPrimitive)
			{
				if (!ArePrimitivesCompatible(Enum.GetUnderlyingType(expected.Type), actual.Type))
				{
					throw new StackValidationException(GetExceptionMessage(actual, expected, phrase));
				}
			}
			else
			{
				if (!expected.Type.IsAssignableFrom(actual.Type))
				{
					throw new StackValidationException(GetExceptionMessage(actual, expected, phrase));
				}
			}
		}

		private enum PrimitiveType
		{
			I4,
			I8,
			R4,
			R8
		}

		private static PrimitiveType GetPrimitiveType(Type type)
		{
			if (type == typeof(bool)) return PrimitiveType.I4;
			if (type == typeof(char)) return PrimitiveType.I4;
			if (type == typeof(byte)) return PrimitiveType.I4;
			if (type == typeof(short)) return PrimitiveType.I4;
			if (type == typeof(int)) return PrimitiveType.I4;
			if (type == typeof(sbyte)) return PrimitiveType.I4;
			if (type == typeof(ushort)) return PrimitiveType.I4;
			if (type == typeof(uint)) return PrimitiveType.I4;
			if (type == typeof(decimal)) return PrimitiveType.I4;

			if (type == typeof(long)) return PrimitiveType.I8;
			if (type == typeof(ulong)) return PrimitiveType.I8;

			if (type == typeof(float)) return PrimitiveType.R4;

			if (type == typeof(double)) return PrimitiveType.R8;

			throw new ArgumentException("type is not a primitive", "type");
		}

		private static bool ArePrimitivesCompatible(Type left, Type right)
		{
			return GetPrimitiveType(left) == GetPrimitiveType(right);
		}
	}
}
