using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;

namespace MySpace.Common
{
	/// <summary>
	/// Encapsulates commonly used extension methods.
	/// </summary>
	public static class CommonExtensions
	{
		public static AssemblyName TryGetName(this Assembly assembly)
		{
			try
			{
				return assembly.GetName();
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// Reads the specified xml attribute as if it were a Boolean string.
		/// Returns <paramref name="defaultValue"/> the value was not present or could not be parsed.
		/// </summary>
		/// <param name="reader">The reader to read from.</param>
		/// <param name="attributeName">The name of the attribute.</param>
		/// <param name="defaultValue">The value to return in the event that the attribute is missing or invalid.</param>
		/// <returns></returns>
		public static bool ReadBooleanAttributeOrDefault(this XmlReader reader, string attributeName, bool defaultValue = false)
		{
			ArgumentAssert.IsNotNull(reader, "reader");
			ArgumentAssert.IsNotNullOrEmpty(attributeName, "attributeName");

			var val = reader.GetAttribute(attributeName);
			bool result;
			if (!string.IsNullOrEmpty(val) && bool.TryParse(val, out result)) return result;
			return defaultValue;
		}

		/// <summary>
		/// Gets the value or default(T).
		/// </summary>
		/// <typeparam name="TKey">The type of the key.</typeparam>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <param name="dictionary">The dictionary.</param>
		/// <param name="key">The key.</param>
		/// <returns>The value or default(T) if not found.</returns>
		public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
		{
			TValue value;
			dictionary.TryGetValue(key, out value);
			return value;
		}

		private static string BuildReducedFullName(Type type, bool withAssemblyNames)
		{
			if (!type.IsGenericType)
			{
				Debug.Fail("Non generic types shouldn't get here.");
				return type.FullName;
			}

			var typeParams = type.GetGenericArguments();

			var output = new StringBuilder(type.Namespace);
			output.Append('.');
			output.Append(type.Name);
			output.Append('[');
			for (int i = 0; i < typeParams.Length; ++i)
			{
				if (i > 0)
				{
					output.Append(',');
				}
				output.Append('[');
				output.Append(typeParams[i].GetReducedFullName(withAssemblyNames));
				if (withAssemblyNames)
				{
					output.Append(',');
					output.Append(typeParams[i].Assembly.GetName().Name);
				}
				output.Append("]");
			}
			output.Append("]");
			return output.ToString();
		}

		private static readonly Factory<Type, string> _reducedFullNames = Algorithm.LazyIndexer<Type, string>(type => BuildReducedFullName(type, false));
		private static readonly Factory<Type, string> _reducedFullNamesWithAssemblyNames = Algorithm.LazyIndexer<Type, string>(type => BuildReducedFullName(type, true));

		/// <summary>
		/// Gets the full type name of the specified type with assembly strong name and version numbers stripped out or no assembly names all together if <paramref name="includeAssemblyNames"/> is false.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="includeAssemblyNames">if set to <see langword="true"/> short assembly names will be included for generic type parameters but not the actual type.</param>
		/// <returns>The full type name of the specified type with assembly strong name and version numbers stripped out or no assembly names all together.</returns>
		public static string GetReducedFullName(this Type type, bool includeAssemblyNames)
		{
			if (!type.IsGenericType) return type.FullName;

			return includeAssemblyNames
				? _reducedFullNamesWithAssemblyNames(type)
				: _reducedFullNames(type);
		}

		/// <summary>
		/// Converts a byte[] into a base64 encoded string.
		/// </summary>
		/// <param name="bytes">The bytes to encode.</param>
		/// <returns>A base64 encoded string.</returns>
		public static string ToBase64String(this byte[] bytes)
		{
			return Convert.ToBase64String(bytes);
		}

		/// <summary>
		/// Converts the <see cref="IEnumerable{T}"/> into a comma-separated string value.
		/// Warning - this method does not escape special characters.
		/// </summary>
		/// <typeparam name="T">The element type.</typeparam>
		/// <param name="values">The values.</param>
		/// <returns>A comma-separated string value.</returns>
		/// <exception cref="ArgumentNullException">
		///   <para><paramref name="values"/> is <see langword="null"/>.</para>
		/// </exception>
		public static string ToCsvString<T>(this IEnumerable<T> values)
		{
			return ToCsvString<T>(values, v => v.ToString());
		}

		/// <summary>
		/// Converts the <see cref="IEnumerable{T}"/> into a comma-separated string value.
		/// Warning - this method does not escape special characters.
		/// </summary>
		/// <typeparam name="T">The element type.</typeparam>
		/// <param name="values">The values.</param>
		/// <param name="toString">The function that converts elements into strings.</param>
		/// <returns>A comma-separated string value.</returns>
		/// <exception cref="ArgumentNullException">
		///   <para><paramref name="values"/> is <see langword="null"/>.</para>
		///   <para>- or -</para>
		///   <para><paramref name="toString"/> is <see langword="null"/>.</para>
		/// </exception>
		public static string ToCsvString<T>(this IEnumerable<T> values, Func<T, string> toString)
		{
			ArgumentAssert.IsNotNull(values, "values");
			ArgumentAssert.IsNotNull(toString, "toString");

			using (var e = values.GetEnumerator())
			{
				if (!e.MoveNext()) return string.Empty;

				var value = toString(e.Current);

				if (!e.MoveNext()) return value;

				var builder = new StringBuilder(value);

				do
				{
					builder.Append(',');
					builder.Append(toString(e.Current));
				}
				while (e.MoveNext());

				return builder.ToString();
			}
		}

		/// <summary>
		/// Converts the specified bytes into a hexadecimal string value.
		/// </summary>
		/// <param name="bytes">The bytes to convert.</param>
		/// <returns>The hexadecimal representation of <paramref name="bytes"/>.</returns>
		public static string ToHex(this IEnumerable<byte> bytes)
		{
			ArgumentAssert.IsNotNull(bytes, "bytes");
			StringBuilder sb = new StringBuilder();
			foreach (var b in bytes)
			{
				sb.Append(GetHexNibble(b >> 4));
				sb.Append(GetHexNibble(b & 0xF));
			}
			return sb.ToString();
		}

		private static char GetHexNibble(int value)
		{
			if (value > 15 || value < 0)
			{
				throw new ArgumentOutOfRangeException("value", "value must be between 0 and 15 inclusive");
			}
			if (value >= 10)
			{
				return (char)(value - 10 + 'A');
			}
			return (char)(value + '0');
		}
	}
}
