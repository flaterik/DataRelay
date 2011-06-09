using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MySpace.Common.Barf
{
	internal class PartDefinition
	{
		public static PartDefinition Create(Type type)
		{
			if (type == null) throw new ArgumentNullException("type");

			var classAttributes = Attribute.GetCustomAttributes(type, typeof(SerializableClassAttribute), false);

			if (classAttributes == null || classAttributes.Length == 0)
			{
				throw new BarfException(string.Format("{0} does not define a {1}", type.Name, typeof(SerializableClassAttribute).Name), type, null);
			}

			var classAttribute = classAttributes
				.OfType<SerializableClassAttribute>()
				.First<SerializableClassAttribute>();

			SerializablePropertyAttribute attribute;

			if (classAttribute.SerializeBaseClass)
			{
				attribute = new SerializablePropertyAttribute(0);
			}
			else
			{
				var attributes = Attribute
					.GetCustomAttributes(type, typeof(SerializablePropertyAttribute))
					.OfType<SerializablePropertyAttribute>()
					.Where<SerializablePropertyAttribute>(a => !(a is SerializableInheritedPropertyAttribute))
					.ToArray <SerializablePropertyAttribute>();

				if (attributes.Length > 1)
				{
					throw new BarfException(string.Format("Can't have more than one {0} defined on a type.", typeof(SerializablePropertyAttribute).Name), type, null);
				}

				if (attributes.Length == 0)
				{
					return null;
				}

				attribute = attributes[0];
			}

			if (attribute.Dynamic)
			{
				// todo validate better (top level type can't be but generic parameters may be)
				//throw new BarfException(string.Format("{0}'s that are applied to types may not be Dynamic. Type=\"{1}\"", typeof(SerializablePropertyAttribute).Name, type.Name), type, null);
			}

			var partType = type.BaseType;

			if (partType == typeof(object))
			{
				partType = type
					.GetInterfaces()
					.Where(t =>
					{
						if (t == typeof(ICollection<>)) return true;
						return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ICollection<>);
					})
					.FirstOrDefault<Type>()
					?? typeof(object);
			}

			return new PartDefinition
			{
				Member = null,
				MemberName = "!base",
				Version = attribute.Version,
				ObsoleteVersion = attribute.ObsoleteVersion,
				Flags = attribute.Dynamic ? PartFlags.Dynamic : PartFlags.None,
				ReadMethod = attribute.ReadMethod,
				WriteMethod = attribute.WriteMethod,
				Type = partType
			};
		}

		public static IEnumerable<PartDefinition> CreateInheritedParts(Type type)
		{
			ArgumentAssert.IsNotNull(type, "type");
			foreach (SerializableInheritedPropertyAttribute attribute in Attribute
				.GetCustomAttributes(type, typeof(SerializableInheritedPropertyAttribute), false))
			{
				const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;
				const MemberTypes memberTypes = MemberTypes.Property | MemberTypes.Field;
				var member = attribute.BaseClass.GetMember(attribute.BasePropertyName, memberTypes, flags).FirstOrDefault<MemberInfo>();
				if (member == null)
				{
					throw new MissingMemberException(attribute.BaseClass.FullName, attribute.BasePropertyName);
				}
				var prop = member as PropertyInfo;
				var memberType = prop != null
					? prop.PropertyType
					: ((FieldInfo)member).FieldType;

				yield return new PartDefinition
				{
					Member = member,
					MemberName = member.Name,
					Version = attribute.Version,
					ObsoleteVersion = attribute.ObsoleteVersion,
					Flags = attribute.Dynamic ? PartFlags.Dynamic : PartFlags.None,
					ReadMethod = attribute.ReadMethod,
					WriteMethod = attribute.WriteMethod,
					Type = memberType
				};
			}
		}

		public static PartDefinition Create(MemberInfo member)
		{
			if (member == null) throw new ArgumentNullException("member");
			if (!(member is PropertyInfo || member is FieldInfo))
			{
				throw new ArgumentException("member is not a property or field", "member");
			}

			var attribute = member.GetCustomAttributes(typeof(SerializablePropertyAttribute), false)
				.OfType<SerializablePropertyAttribute>()
				.FirstOrDefault<SerializablePropertyAttribute>();

			if (attribute == null) return null;

			var type = member is PropertyInfo
				? ((PropertyInfo)member).PropertyType
				: ((FieldInfo)member).FieldType;

			return new PartDefinition
			{
				Member = member,
				MemberName = member.Name,
				Version = attribute.Version,
				ObsoleteVersion = attribute.ObsoleteVersion,
				Flags = attribute.Dynamic ? PartFlags.Dynamic : PartFlags.None,
				ReadMethod = attribute.ReadMethod,
				WriteMethod = attribute.WriteMethod,
				Type = type
			};
		}

		private PartDefinition()
		{
		}

		public bool IsBaseType
		{
			get { return Member == null; }
		}

		public Type Type { get; private set; }

		public Type ConcreteType { get; private set; }

		public string FullName
		{
			get
			{
				return Member == null
					? Type.ToString()
					: Member.DeclaringType.FullName + "." + Member.Name;
			}
		}

		public PartFlags Flags { get; private set; }

		public MemberInfo Member { get; private set; }

		public string MemberName { get; private set; }

		public int Version { get; private set; }

		public int ObsoleteVersion { get; private set; }

		public bool IsDynamic
		{
			get { return Flags.IsSet(PartFlags.Dynamic); }
		}

		public string ReadMethod { get; private set; }

		public string WriteMethod { get; private set; }

		public override string ToString()
		{
			var result = new StringBuilder("[SerializableProperty(");
			result.Append(Version);
			if (ObsoleteVersion != 0)
			{
				result.Append(", ObsoleteVersion = ");
				result.Append(ObsoleteVersion);
			}
			if (Flags.IsSet(PartFlags.Dynamic))
			{
				result.Append(", Dynamic = true");
			}
			if (!string.IsNullOrEmpty(ReadMethod))
			{
				result.Append(", ReadMethod = ");
				result.Append('"');
				result.Append(ReadMethod);
				result.Append('"');
			}
			if (!string.IsNullOrEmpty(WriteMethod))
			{
				result.Append(", ReadMethod = ");
				result.Append('"');
				result.Append(ReadMethod);
				result.Append('"');
			}
			result.Append(")] ");
			result.Append(Type.FullName);
			if (Member != null)
			{
				result.Append('.');
				result.Append(Member.Name);
			}

			return result.ToString();
		}
	}
}
