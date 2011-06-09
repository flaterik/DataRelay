using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using MySpace.Common.Dynamic;
using MySpace.Common.IO;

namespace MySpace.Common.Barf
{
	internal class BarfTypeDefinition
	{
		private static readonly Factory<Type, BarfTypeDefinition> _typeDefinitions = Algorithm.LazyIndexer<Type, BarfTypeDefinition>(Create);

		private static class Definition<T>
		{
			static Definition()
			{
				Instance = _typeDefinitions(typeof(T));
			}

			public static BarfTypeDefinition Instance { get; private set; }
		}

		public static BarfTypeDefinition Get<T>(bool throwIfInvalid)
		{
			if (throwIfInvalid && !Definition<T>.Instance.IsValid)
			{
				var message = new StringBuilder("Auto-serialization validation failure on ");
				message.Append(typeof(T));
				message.AppendLine(".");
				message.AppendLine("Errors - ");
				foreach (var error in Definition<T>.Instance.Errors)
				{
					message.AppendLine();
					message.AppendLine("Error - ");
					message.AppendLine(error);
				}
				throw new ApplicationException(message.ToString());
			}
			return Definition<T>.Instance;
		}

		public static BarfTypeDefinition Get(Type type)
		{
			return _typeDefinitions(type);
		}

		private static BarfTypeDefinition Create(Type type)
		{
			var parts = new List<PartDefinition>();
			var errors = new List<string>();
			var result = new BarfTypeDefinition
			{
				Type = type,
				Errors = new ReadOnlyCollection<string>(errors),
				Parts = new ReadOnlyCollection<PartDefinition>(parts)
			};

			try
			{
				var classAttribute = (SerializableClassAttribute)Attribute.GetCustomAttribute(type, typeof(SerializableClassAttribute), false);

				if (classAttribute == null)
				{
					errors.Add(type.Name + " does not define a " + typeof(SerializableAttribute).Name);
					return result;
				}

				var typePartDef = PartDefinition.Create(type);

				if (typePartDef != null)
				{
					parts.Add(typePartDef);
				}

				const BindingFlags flags
					= BindingFlags.Instance
					| BindingFlags.Public
					| BindingFlags.NonPublic
					| BindingFlags.DeclaredOnly;

				parts.AddRange(PartDefinition.CreateInheritedParts(type));

				foreach (var member in type.GetMembers(flags))
				{
					if (member.DeclaringType != type)
					{
						continue;
					}
					if (member.MemberType != MemberTypes.Property && member.MemberType != MemberTypes.Field)
					{
						continue;
					}

					var partDefinition = PartDefinition.Create(member);

					if (partDefinition == null) continue;

					if (partDefinition.IsDynamic)
					{
						if (partDefinition.Type.IsValueType)
						{
							// value types may not be dynamic
							return result;
						}
					}

					parts.Add(partDefinition);
				}

				parts.Sort(BarfPartInfoComparer.Default);

				result.CurrentVersion = result.Parts
					.Select(part => part.Version)
					.Concat(new[] { classAttribute.MinVersion })
					.Max<int>();


				// before we would guess the min version if it was zero
				// by taking the lowest property version. However
				// if an empty class defining no parts will be version
				// zero. If a property is added at version 1 min version
				// will then become 1 making it impossible to deserialize
				// the older empty class streams.
				result.MinVersion = classAttribute.MinVersion;

				result.MinDeserializeVersion = Math.Max(classAttribute.MinDeserializeVersion, 0);

				result.LegacyVersion = classAttribute.LegacyVersion;

				if (result.LegacyVersion >= 0)
				{
					var method = type.ResolveMethod("Deserialize", typeof(IPrimitiveReader), typeof(int));
					if (method != null && method.IsStatic) method = null;
					if (method == null && result.LegacyVersion > 0)
					{
						errors.Add("LegacyVersion=" + result.LegacyVersion + " but no method void Deserialize(IPrimitiveReader, int) was defined.");
					}
					else
					{
						result.LegacyDeserializeMethod = method;
					}
				}

				result.IsForwardCompatible = typeof(ISerializationInfo).IsAssignableFrom(type);
			}
			catch (Exception ex)
			{
				errors.Add(ex.ToString());
				return result;
			}
			return result;
		}

		private BarfTypeDefinition()
		{
		}

		public ReadOnlyCollection<string> Errors { get; private set; }

		public bool IsValid
		{
			get { return Errors.Count == 0; }
		}

		public bool IsForwardCompatible { get; private set; }

		public Type Type { get; private set; }

		public int CurrentVersion { get; private set; }

		public int MinVersion { get; private set; }

		public int MinDeserializeVersion { get; private set; }

		public bool HasLegacyVersion
		{
			get { return LegacyDeserializeMethod != null; }
		}

		public int LegacyVersion { get; private set; }

		public MethodInfo LegacyDeserializeMethod { get; private set; }

		public ReadOnlyCollection<PartDefinition> Parts { get; private set; }

		private class BarfPartInfoComparer : IComparer<PartDefinition>
		{
			private static readonly BarfPartInfoComparer _default = new BarfPartInfoComparer();

			public static BarfPartInfoComparer Default
			{
				get { return _default; }
			}

			private BarfPartInfoComparer() { }

			#region IComparer<Part> Members

			public int Compare(PartDefinition x, PartDefinition y)
			{
				var result = x.Version.CompareTo(y.Version);
				if (result != 0) return result;
				return x.MemberName.CompareTo(y.MemberName);
			}

			#endregion
		}
	}
}
