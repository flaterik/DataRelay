using System;
using System.Linq;
using System.Reflection;
using MySpace.Common.IO;

namespace MySpace.Common.Barf.Parts
{
	internal class CustomSerializablePartBuilder : IPartBuilder
	{
		private static readonly MethodInfo _serializeMethodDef;
		private static readonly MethodInfo _deserializeMethodDef;

		static CustomSerializablePartBuilder()
		{
#pragma warning disable 618,612
			var methods = typeof(PartFormatter).GetMethods(BindingFlags.Public | BindingFlags.Static);
#pragma warning restore 618,612

			_serializeMethodDef = methods
				.Where<MethodInfo>(m => m.Name == "WriteCustomSerializable")
				.Where<MethodInfo>(m => m.IsGenericMethodDefinition)
				.Where<MethodInfo>(m => m.ReturnType == null || m.ReturnType == typeof(void))
				.Where<MethodInfo>(m => m.GetParameters().Length == 2)
				.Where<MethodInfo>(m => m.GetParameters()[0].ParameterType.IsGenericParameter)
				.Where<MethodInfo>(m => m.GetParameters()[1].ParameterType == typeof(IPrimitiveWriter))
				.FirstOrDefault<MethodInfo>();

			if (_serializeMethodDef == null)
			{
				throw new MissingMethodException(typeof(ICustomSerializable).FullName, "WriteCustomSerializable<T>(T,IPrimitiveWriter):void");
			}

			_deserializeMethodDef = methods
				.Where<MethodInfo>(m => m.Name == "ReadCustomSerializable")
				.Where<MethodInfo>(m => m.IsGenericMethodDefinition)
				.Where<MethodInfo>(m => m.ReturnType.IsGenericParameter)
				.Where<MethodInfo>(m => m.GetParameters().Length == 1)
				.Where<MethodInfo>(m => m.GetParameters()[0].ParameterType == typeof(IPrimitiveReader))
				.FirstOrDefault<MethodInfo>();

			if (_deserializeMethodDef == null)
			{
				throw new MissingMethodException(typeof(ICustomSerializable).FullName, "ReadCustomSerializable(IPrimitiveReader):T");
			}
		}

		private readonly MethodInfo _serializeMethod;
		private readonly MethodInfo _deserializeMethod;

		public CustomSerializablePartBuilder(Type type)
		{
			if (!typeof(ICustomSerializable).IsAssignableFrom(type))
			{
				throw new ArgumentException("type does not implement ICustomSerializable", "type");
			}

			_serializeMethod = _serializeMethodDef.MakeGenericMethod(type);
			_deserializeMethod = _deserializeMethodDef.MakeGenericMethod(type);
		}

		#region IPartBuilder Members

		public void GenerateSerializePart(GenSerializeContext context)
		{
			var g = context.Generator;

			g.BeginCall(_serializeMethod);
			{
				g.Load(context.Member);
				g.Load(context.Writer);
			}
			g.EndCall();
		}

		public void GenerateDeserializePart(GenDeserializeContext context)
		{
			var g = context.Generator;

			g.BeginAssign(context.Member);
			{
				g.BeginCall(_deserializeMethod);
				{
					g.Load(context.Reader);
				}
				g.EndCall();
			}
			g.EndAssign();
		}

		public void GenerateFillPart(GenFillContext context)
		{
			context.GenerateSkippedWarning(true, "ICustomSerializable fields can't be filled");
		}

		public void GenerateAssertAreEqualPart(GenAssertAreEqualContext context)
		{
			context.GenerateRaiseNotSupported();
		}

		#endregion
	}
}
