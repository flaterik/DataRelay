using System;
using System.Linq;
using System.Reflection;

namespace MySpace.Common.Barf
{
	/// <summary>
	/// Encapsulates errors that happen within the Barf framework.
	/// </summary>
	public class BarfException : Exception
	{
		private static string GetMessage(string message, MemberInfo targetMember)
		{
			return string.Format(
				"Serialization validation failure on {0}.{1} - {2}",
				targetMember.DeclaringType.Name,
				targetMember.Name,
				message);
		}

		private static string GetMessage(string message, Type targetType)
		{
			return string.Format(
				"Serialization validation failure on {0} - {1}",
				targetType.Name,
				message);
		}

		internal BarfException(string message, MemberInfo targetMember, Exception innerException)
			: base(GetMessage(message, targetMember), innerException)
		{
			TargetMember = targetMember;
			TargetType = targetMember.DeclaringType;
		}

		internal BarfException(string message, Type targetType, Exception innerException)
			: base(GetMessage(message, targetType), innerException)
		{
			TargetMember = null;
			TargetType = targetType;
		}

		/// <summary>
		/// Gets the target member that was being serialized or deserialized when the error occurred.
		/// </summary>
		/// <value>The target member that was being serialized or deserialized when the error occurred.</value>
		public MemberInfo TargetMember { get; private set; }

		/// <summary>
		/// Gets the target type that was being serialized or deserialized when the error occurred.
		/// </summary>
		/// <value>The target type that was being serialized or deserialized when the error occurred.</value>
		public Type TargetType { get; private set; }
	}
}
