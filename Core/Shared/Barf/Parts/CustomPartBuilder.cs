using System;
using System.Linq;
using System.Reflection;
using MySpace.Common.IO;

namespace MySpace.Common.Barf.Parts
{
	internal class CustomPartBuilder : IPartBuilder
	{
		public static CustomPartBuilder Create(Type type, PartDefinition definition)
		{
			if (string.IsNullOrEmpty(definition.ReadMethod) || string.IsNullOrEmpty(definition.WriteMethod))
			{
				throw new InvalidOperationException("Custom serialized fields must defined both 'ReadMethod' and 'WriteMethod'");
			}

			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

			var readMethod = definition.Member.DeclaringType.GetMethod(definition.ReadMethod, flags, null, new[] { typeof(IPrimitiveReader) }, null);

			if (readMethod == null)
			{
				throw new InvalidOperationException(string.Format("The read method void {0}({1}) couldn't be found", definition.ReadMethod, typeof(IPrimitiveReader).Name));
			}

			var writeMethod = definition.Member.DeclaringType.GetMethod(definition.WriteMethod, flags, null, new[] { typeof(IPrimitiveWriter) }, null);

			if (writeMethod == null)
			{
				throw new InvalidOperationException(string.Format("The write method void {0}({1}) couldn't be found", definition.ReadMethod, typeof(IPrimitiveReader).Name));
			}

			return new CustomPartBuilder
			{
				_readMethod = readMethod,
				_writeMethod = writeMethod
			};
		}

		#region IPartBuilder Members

		private MethodInfo _readMethod;
		private MethodInfo _writeMethod;

		private CustomPartBuilder()
		{
		}

		public Type Type { get; private set; }

		public void GenerateSerializePart(GenSerializeContext context)
		{
			var g = context.Generator;
			g.Load(context.Instance);
			g.BeginCall(_writeMethod);
			{
				g.Load(context.Writer);
			}
			g.EndCall();
		}

		public void GenerateDeserializePart(GenDeserializeContext context)
		{
			var g = context.Generator;
			g.Load(context.Instance);
			g.BeginCall(_readMethod);
			{
				g.Load(context.Reader);
			}
			g.EndCall();
		}

		public void GenerateFillPart(GenFillContext context)
		{
			context.GenerateSkippedWarning(false, "custom fields can't be filled");
		}

		public void GenerateAssertAreEqualPart(GenAssertAreEqualContext context)
		{
			context.GenerateRaiseNotSupported();
		}

		#endregion
	}
}
