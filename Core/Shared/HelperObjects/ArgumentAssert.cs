using System;
using System.Linq;

namespace MySpace.Common
{
	/// <summary>
	/// Encapsulates methods for validating method parameters.
	/// </summary>
	public static class ArgumentAssert
	{
		/// <summary>
		///		<para>Throws an <see cref="ArgumentNullException"/> is <paramref name="value"/> is <see langword="null"/>.</para>
		///		<para>- or -</para>
		///		<para>Throws an <see cref="ArgumentException"/> is <paramref name="value"/> is empty.</para>
		/// </summary>
		/// <param name="value">The value of the parameter.</param>
		/// <param name="parameterName">The name of the parameter.</param>
		public static void IsNotNullOrEmpty(string value, string parameterName)
		{
			if (value == null) throw new ArgumentNullException(parameterName);
			if (value.Length == 0) throw new ArgumentException(parameterName + " may not be empty.", parameterName);
		}

		/// <summary>
		/// Throws an <see cref="ArgumentNullException"/> is <paramref name="value"/> is <see langword="null"/>.
		/// </summary>
		/// <param name="value">The value of the parameter.</param>
		/// <param name="parameterName">The name of the parameter.</param>
		public static void IsNotNull(object value, string parameterName)
		{
			if (value == null) throw new ArgumentNullException(parameterName);
		}

		/// <summary>
		/// Throws an <see cref="ArgumentNullException"/> is <paramref name="value"/> is <see langword="null"/>.
		/// </summary>
		/// <typeparam name="T">The type of parameter.</typeparam>
		/// <param name="value">The value of the parameter.</param>
		/// <param name="parameterName">The name of the parameter.</param>
		public static void IsNotNull<T>(T? value, string parameterName) where T : struct
		{
			if (!value.HasValue) throw new ArgumentNullException(parameterName);
		}
	}
}
