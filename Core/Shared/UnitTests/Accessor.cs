using System;
using System.Linq;
using System.Reflection;

namespace MySpace.Common.UnitTests
{
	/// <summary>
	/// 	<para>Encapsulates helpers for accessing private members of classes.</para>
	/// </summary>
	public class Accessor
	{
		private readonly object _target;

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="Accessor"/> class.</para>
		/// </summary>
		/// <param name="target">The target object to access.</param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="target"/> is <see langword="null"/>.</para>
		/// </exception>
		public Accessor(object target)
		{
			if (target == null) throw new ArgumentNullException("target");

			_target = target;
		}

		/// <summary>
		/// 	<para>Gets or sets the value of the specified field or property.</para>
		/// </summary>
		/// <param name="memberName">The name of the property or field.</param>
		/// <value>The value of the specified field or property.</value>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="memberName"/> is <see langword="null"/> or empty.</para>
		/// </exception>
		/// <exception cref="ArgumentException">
		///	<para>The target does not have a field or property named <paramref name="memberName"/>.</para>
		/// </exception>
		public object this[string memberName]
		{
			get
			{
				if (string.IsNullOrEmpty(memberName))
				{
					throw new ArgumentNullException("memberName");
				}

				var member = _target.GetType().GetMember(
					memberName,
					MemberTypes.Property | MemberTypes.Field,
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault<MemberInfo>();
				if (member == null)
				{
					throw new ArgumentException(_target.GetType().Name + " does not define a property or field called '" + memberName + "'", "memberName");
				}
				if (member is FieldInfo)
				{
					var field = (FieldInfo)member;
					return field.GetValue(_target);
				}
				if (member is PropertyInfo)
				{
					var property = (PropertyInfo)member;
					return property.GetGetMethod(true).Invoke(_target, null);
				}
				throw new ArgumentException("Member '{0}' is not a property or field", "memberName");
			}
			set
			{
				if (string.IsNullOrEmpty(memberName))
				{
					throw new ArgumentNullException("memberName");
				}

				var member = _target.GetType().GetMember(
					memberName,
					MemberTypes.Property | MemberTypes.Field,
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault<MemberInfo>();
				if (member == null)
				{
					throw new ArgumentException(_target.GetType().Name + " does not define a property or field called '" + memberName + "' exists", "memberName");
				}
				if (member is FieldInfo)
				{
					var field = (FieldInfo)member;
					field.SetValue(_target, value);
					return;
				}
				if (member is PropertyInfo)
				{
					var property = (PropertyInfo)member;
					property.GetSetMethod(true).Invoke(_target, new [] { value });
					return;
				}
				throw new ArgumentException("Member '{0}' is not a property or field", "memberName");
			}
		}
	}
}
