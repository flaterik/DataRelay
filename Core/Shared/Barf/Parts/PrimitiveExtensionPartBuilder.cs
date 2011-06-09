using System;
using System.Linq;
using System.Reflection;
using MySpace.Common.Dynamic;
using MySpace.Common.IO;

namespace MySpace.Common.Barf.Parts
{
	internal class PrimitiveExtensionPartBuilder : IPartBuilder
	{
		private static string GetFriendlyTypeName(Type type)
		{
			if(type.IsArray)
			{
				return GetFriendlyTypeName(type.GetElementType()) + "Array";
			}
			return type.Name;
		}

		private static MethodInfo GetMethod(
			Type type,
			string prefix,
			string suffix,
			bool typeNameRequired,
			params Type[] parameterTypes)
		{
			string typeName = GetFriendlyTypeName(type);
			var result = typeof(PrimitiveExtensions).ResolveMethod(prefix + typeName + suffix, parameterTypes);
			if (result == null && !typeNameRequired)
			{
				result = typeof(PrimitiveExtensions).ResolveMethod(prefix + suffix, parameterTypes);
			}
			if (result == null)
			{
				string parameters = "(" + string.Join(", ", parameterTypes.Select<Type, string>(t => t.Name).ToArray<string>()) + ")";
				throw new MissingMethodException(typeof(PrimitiveExtensions).FullName, prefix + typeName + suffix + parameters);
			}
			return result;
		}

		private readonly MethodInfo _readMethod;
		private readonly MethodInfo _writeMethod;
		private readonly MethodInfo _fillMethod;
		private readonly MethodInfo _assertMethod;

		public PrimitiveExtensionPartBuilder(Type type)
		{
            if (type == null) throw new ArgumentNullException("type");

			_readMethod = GetMethod(type, "Read", string.Empty, true, typeof(IPrimitiveReader));
			_writeMethod = GetMethod(type, "Write", string.Empty, false, typeof(IPrimitiveWriter), type);
			_fillMethod = GetMethod(type, "Fill", string.Empty, true, typeof(FillArgs));
			_assertMethod = GetMethod(type, "Assert", "AreEqual", false, typeof(AssertArgs), type, type);
		}

		#region IPartBuilder Members

		public void GenerateSerializePart(GenSerializeContext context)
		{
			var g = context.Generator;
			g.BeginCall(_writeMethod);
			{
				g.Load(context.Writer);
				g.Load(context.Member);
			}
			g.EndCall();
		}

		public void GenerateDeserializePart(GenDeserializeContext context)
		{
			var g = context.Generator;
			g.BeginAssign(context.Member);
			{
				g.BeginCall(_readMethod);
				{
					g.Load(context.Reader);
				}
				g.EndCall();
			}
			g.EndAssign();
		}

		public void GenerateFillPart(GenFillContext context)
		{
			var g = context.Generator;
			g.BeginAssign(context.Member);
			{
				g.BeginCall(_fillMethod);
				{
					g.Load(context.FillArgs);
				}
				g.EndCall();
			}
			g.EndAssign();
		}

		public void GenerateAssertAreEqualPart(GenAssertAreEqualContext context)
		{
			var g = context.Generator;
			g.BeginCall(_assertMethod);
			{
				g.Load(context.AssertArgs);
				g.Load(context.Expected);
				g.Load(context.Actual);
			}
			g.EndCall();
		}

		#endregion
	}
}
