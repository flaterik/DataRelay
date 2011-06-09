using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySpace.Common.Dynamic;

namespace MySpace.Common.Barf.Parts
{
	internal class SerializableStructPartBuilder : IPartBuilder
	{
		public static SerializableStructPartBuilder TryCreate(Type type, PartDefinition partDefinition, IPartResolver resolver)
		{
			var attribute = Attribute
				.GetCustomAttributes(type, typeof(SerializableStructAttribute), false)
				.Cast<SerializableStructAttribute>()
				.FirstOrDefault<SerializableStructAttribute>();

			if (attribute == null) return null;

			var targetType = attribute.TargetType;

			if (!type.IsValueType)
			{
				throw new BarfException(string.Format(
					"{0}(typeof({1})) is invalid on type {2}. {2} must be a value type.",
					typeof(SerializableStructAttribute).Name,
					targetType,
					type), type, null);
			}

			if (!targetType.IsPrimitive)
			{
				throw new BarfException(string.Format(
					"{0}(typeof({1})) is invalid. {1} must be primitive type.",
					typeof(SerializableStructAttribute).Name,
					targetType.Name),
					type,
					null);
			}

			var possibleMethods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
				.Where<MethodInfo>(m => m.IsSpecialName)
				.Where<MethodInfo>(m => m.Name == "op_Explicit" || m.Name == "op_Implicit")
				.Where<MethodInfo>(m => !m.IsGenericMethodDefinition);

			var result = new SerializableStructPartBuilder
			{
				_targetType = targetType,
				_toPrimitiveMethod = possibleMethods
					.Where<MethodInfo>(m => m.ReturnType == targetType)
					.Where<MethodInfo>(m =>
					{
						var p = m.GetParameters();
						return p.Length == 1 && p[0].ParameterType == type;
					})
					.FirstOrDefault<MethodInfo>(),
				_fromPrimitiveMethod = possibleMethods
					.Where<MethodInfo>(m => m.ReturnType == type)
					.Where<MethodInfo>(m =>
					{
						var p = m.GetParameters();
						return p.Length == 1 && p[0].ParameterType == targetType;
					})
					.FirstOrDefault<MethodInfo>(),
				_innerBuilder = resolver.GetPartBuilder(targetType, partDefinition, true)
			};

			if (result._toPrimitiveMethod == null || result._fromPrimitiveMethod == null)
			{
				throw new BarfException(string.Format(
					"{0}(typeof({1})) is invalid on type {2}. {2} must define an implicit or explicit conversion operator to type {1}.",
					typeof(SerializableStructAttribute).Name,
					targetType,
					type), type, null);
			}

			return result;
		}

		private Type _targetType;
		private MethodInfo _toPrimitiveMethod;
		private MethodInfo _fromPrimitiveMethod;
		private IPartBuilder _innerBuilder;

		private SerializableStructPartBuilder()
		{
		}

		#region IPartBuilder Members

		private static void GenerateConvert(
			MethodGenerator g,
			MethodInfo converter,
			IExpression source,
			IExpression dest)
		{
			g.BeginAssign(dest);
			{
				g.BeginCall(converter);
				{
					g.Load(source);
				}
				g.EndCall();
			}
			g.EndAssign();
		}

		public void GenerateSerializePart(GenSerializeContext context)
		{
			var g = context.Generator;

			var primitive = g.CreateExpression(g.DeclareLocal(_targetType));

			GenerateConvert(g, _toPrimitiveMethod, context.Member, primitive);

			_innerBuilder.GenerateSerializePart(context.CreateChild(primitive));
		}

		public void GenerateDeserializePart(GenDeserializeContext context)
		{
			var g = context.Generator;
			var primitive = g.CreateExpression(g.DeclareLocal(_targetType));
			_innerBuilder.GenerateDeserializePart(context.CreateChild(primitive));

			GenerateConvert(g, _fromPrimitiveMethod, primitive, context.Member);
		}

		public void GenerateFillPart(GenFillContext context)
		{
			var g = context.Generator;
			var primitive = g.CreateExpression(g.DeclareLocal(_targetType));
			context.InnerFill(_innerBuilder, primitive);

			GenerateConvert(g, _fromPrimitiveMethod, primitive, context.Member);
		}

		public void GenerateAssertAreEqualPart(GenAssertAreEqualContext context)
		{
			var g = context.Generator;
			var expected = g.CreateExpression(g.DeclareLocal(_targetType));
			var actual = g.CreateExpression(g.DeclareLocal(_targetType));

			GenerateConvert(g, _toPrimitiveMethod, context.Expected, expected);
			GenerateConvert(g, _toPrimitiveMethod, context.Actual, actual);

			var method = typeof(Assert).ResolveMethod("AreEqual", typeof(object), typeof(object), typeof(string));
			g.BeginCall(method);
			{
				g.Load(expected);
				if (expected.Type.IsValueType)
				{
					g.Box();
				}
				g.Load(actual);
				if (actual.Type.IsValueType)
				{
					g.Box();
				}
				g.Load(context.Part.FullName);
			}
			g.EndCall();
		}

		#endregion
	}
}
