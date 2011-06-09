using System;
using Mono.Cecil;

namespace MySpace.Common.Dynamic
{
	/// <summary>
	/// 	<para>Encapsulates helper methods for working with the Mono.Cecil library.</para>
	/// </summary>
	public static class CecilExtensions
	{
		private const char _cecilSubTypeSeparator = '/';
		private const char _reflectionSubTypeSeparator = '+';

		/// <summary>
		/// Gets the full type name that is compatible with the Mono.Cecil library.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns>The full type name that is compatible with the Mono.Cecil library.</returns>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="type"/> is <see langword="null"/>.</para>
		/// </exception>
		public static string GetCecilFullName(this Type type)
		{
			if (type == null) throw new ArgumentNullException("type");

			return type.FullName.Replace(
				_reflectionSubTypeSeparator,
				_cecilSubTypeSeparator);
		}

		/// <summary>
		/// Gets the full name of <paramref name="typeReference"/> that is compatible with the reflection libraries.
		/// </summary>
		/// <param name="typeReference">The type reference.</param>
		/// <returns>
		///	<para>The full name of <paramref name="typeReference"/> that is compatible with the reflection libraries.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="typeReference"/> is <see langword="null"/>.</para>
		/// </exception>
		public static string GetReflectionFullName(this TypeReference typeReference)
		{
			if (typeReference == null) throw new ArgumentNullException("typeReference");

			return typeReference.FullName.Replace(
				_cecilSubTypeSeparator,
				_reflectionSubTypeSeparator);
		}

		/// <summary>
		/// Converts the <see cref="TypeReference"/> into a <see cref="Type"/> by probing the current app domain.
		/// </summary>
		/// <param name="typeReference">The type reference.</param>
		/// <param name="throwOnError"><see langword="true"/> to throw an exception if the type is not found; otherwise <see langword="false"/>.</param>
		/// <returns>The type that matches the type reference; <see langword="null"/> if not found and <paramref name="throwOnError"/> is <see langword="false"/>.</returns>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="typeReference"/> is <see langword="null"/>.</para>
		/// </exception>
		public static Type ToReflectionType(this TypeReference typeReference, bool throwOnError)
		{
			if (typeReference == null) throw new ArgumentNullException("typeReference");
			return Type.GetType(typeReference.GetReflectionFullName() + "," + typeReference.Module.Assembly.Name.FullName, throwOnError);
		}

		public static bool IsAssignableFrom(this TypeReference toType, TypeReference fromType)
		{
			if (toType == null) throw new ArgumentNullException("toType");
			if (fromType == null) throw new ArgumentNullException("fromType");

			if (toType == fromType) return true;

			if (fromType.IsTypeEqual(toType))
			{
				return true;
			}

			if (fromType.IsSubclassOf(toType))
			{
				return true;
			}

			var toTypeDef = toType.Resolve();
			var fromTypeDef = fromType.Resolve();

			if (!toTypeDef.IsInterface) return false;

			foreach (TypeReference fromInterface in fromTypeDef.Interfaces)
			{
				if (fromInterface.IsTypeEqual(toTypeDef))
				{
					return true;
				}
				if (fromInterface.IsSubclassOf(toTypeDef))
				{
					return true;
				}
			}
			return false;
		}

		public static bool IsSubclassOf(this TypeReference target, TypeReference type)
		{
			if (target == null) throw new ArgumentNullException("target");
			if (type == null) throw new ArgumentNullException("type");

			var baseType = target.Resolve().BaseType;

			while (baseType != null)
			{
				if (baseType.IsTypeEqual(type)) return true;
				baseType = baseType.Resolve().BaseType;
			}

			return false;
		}

		public static bool IsTypeEqual(this TypeReference target, TypeReference type)
		{
			if (target == null) throw new ArgumentNullException("target");
			if (type == null) throw new ArgumentNullException("type");

			if (target == type) return true;
			return target.FullName == type.FullName && target.Module.Name == type.Module.Name;
		}
	}
}
