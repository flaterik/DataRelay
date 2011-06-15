using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySpace.Storage
{
	/// <summary>
	/// Provides helper functions.
	/// </summary>
	internal static class Utility
	{
		/// <summary>
		/// Gets an order dependent combination of 2 hash codes.
		/// </summary>
		/// <param name="hash1">The first hash code.</param>
		/// <param name="hash2">The second hash code.</param>
		/// <returns>The combined hash code.</returns>
		public static int CombineHashCodes(int hash1, int hash2)
		{
			if (hash1 < 0)
			{
				return ((hash1 << 1) + 1) ^ hash2;
			}
			return (hash1 << 1) ^ hash2;
		}

		/// <summary>
		/// Gets an order dependent combination of an array of hash codes.
		/// </summary>
		/// <param name="hashes">The array of hash codes.</param>
		/// <returns>The combined hash code.</returns>
		public static int CombineHashCodes(params int[] hashes)
		{
			var ret = 0;
			var idx = 0;
			while(true)
			{
				ret ^= hashes[idx];
				if (++idx == hashes.Length) break;
				if (ret < 0)
				{
					ret = (ret << 1) + 1;
				}
				else
				{
					ret <<= 1;
				}
			}
			return ret;
		}

		/// <summary>
		/// Performs a null cognizant equality test of 2 equatable class
		/// instances.
		/// </summary>
		/// <typeparam name="T">The type of the references compared.</typeparam>
		/// <param name="t1">The first <see cref="IEquatable{T}"/>.</param>
		/// <param name="t2">The second <see cref="IEquatable{T}"/>.</param>
		/// <returns>Whether or not the 2 instances are equal.</returns>
		public static bool AreEqual<T>(T t1, T t2) where T : class, IEquatable<T>
		{
			if (t1 == null) return t2 == null;
			if (t2 == null) return false;
			return t1.Equals(t2);
		}

		/// <summary>
		/// Performs a null cognizant equality test of 2 objects.
		/// </summary>
		/// <param name="o1">The first object.</param>
		/// <param name="o2">The second object.</param>
		/// <returns>Whether or not the 2 references are equal.</returns>
		public static bool AreEqualled(object o1, object o2)
		{
			if (o1 == null) return o2 == null;
			if (o2 == null) return false;
			return o1.Equals(o2);			
		}
	}
}
