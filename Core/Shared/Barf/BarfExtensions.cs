using System;
using System.Linq;

namespace MySpace.Common.Barf
{
	/// <summary>
	/// Contains extension methods for the barf framework.
	/// </summary>
	internal static class BarfExtensions
	{
		public static IPartBuilder GetCurrentBuilder(this PartDefinition definition)
		{
			return PartResolver.Current.GetPartBuilder(definition);
		}

		public static object Fill(this IBarfTester tester, FillArgs args)
		{
			object result = null;
			tester.Fill(ref result, args);
			return result;
		}

		public static T Fill<T>(this IBarfTester<T> tester, FillArgs args)
		{
			T result = default(T);
			tester.Fill(ref result, args);
			return result;
		}

		public static bool IsSet(this PartFlags flags, PartFlags value)
		{
			return (flags & value) == value;
		}

		public static bool IsSet(this HeaderFlags flags, HeaderFlags value)
		{
			return (flags & value) == value;
		}

		public static IPartBuilder GetPartBuilder(this IPartResolver resolver, PartDefinition partDefinition)
		{
			return resolver.GetPartBuilder(partDefinition.Type, partDefinition);
		}

		public static IPartBuilder GetPartBuilder(this IPartResolver resolver, PartDefinition partDefinition, bool throwIfUnresolvable)
		{
			return resolver.GetPartBuilder(partDefinition.Type, partDefinition, throwIfUnresolvable);
		}

		public static IPartBuilder GetPartBuilder(this IPartResolver resolver, Type type, PartDefinition partDefinition, bool throwIfUnresolvable)
		{
			var result = resolver.GetPartBuilder(type, partDefinition);

			if (result == null && throwIfUnresolvable)
			{
				throw new NotSupportedException(string.Format(
					"Property - {0} cannot be serialized by the auto-serialization framework. PartResolver - {1}",
					partDefinition,
					resolver));
			}

			return result;
		}
	}
}
