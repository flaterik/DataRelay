using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySpace.Common.Dynamic;

namespace MySpace.Common.Barf.Parts
{
	internal class NullablePartBuilder : IPartBuilder
	{
		private readonly Type _type;
		private readonly Type _innerType;
		private readonly ConstructorInfo _ctor;
		private readonly IPartBuilder _innerBuilder;

		public NullablePartBuilder(Type nullableType, PartDefinition partDefinition, IPartResolver resolver)
		{
			if (nullableType.GetGenericTypeDefinition() != typeof(Nullable<>))
			{
				throw new ArgumentException(string.Format("{0} is not a Nullable<T>", nullableType), "nullableType");
			}
			_type = nullableType;
			_innerType = _type.GetGenericArguments()[0];
			_ctor = _type.GetConstructor(new[] { _innerType });
			_innerBuilder = resolver.GetPartBuilder(_innerType, partDefinition, true);
		}

		#region IPartBuilder Members

		void IPartBuilder.GenerateSerializePart(GenSerializeContext context)
		{
			var g = context.Generator;
			g.If(() =>
			{
				g.Load(context.Member, LoadOptions.ValueAsAddress);
				g.LoadMember("HasValue");
				return BinaryOperator.IsTrue;
			});
			{
				g.Load(context.Writer);
				g.BeginCall(MemberResolver.GetWriteMethod(typeof(bool)));
				{
					g.Load(true);
				}
				g.EndCall();
				g.BeginScope();
				{
					_innerBuilder.GenerateSerializePart(context.CreateChild(
						context.Member
							.Copy()
							.AddMember("Value")));
				}
				g.EndScope();
			}
			g.Else();
			{
				g.Load(context.Writer);
				g.BeginCall(MemberResolver.GetWriteMethod(typeof(bool)));
				{
					g.Load(false);
				}
				g.EndCall();
			}
			g.EndIf();
		}

		void IPartBuilder.GenerateDeserializePart(GenDeserializeContext context)
		{
			var g = context.Generator;

			g.If(() =>
			{
				g.Load(context.Reader);
				g.Call(MemberResolver.GetReadMethod(typeof(bool)));
				return BinaryOperator.IsTrue;
			});
			{
				var value = g.DeclareLocal(_innerType);
				g.BeginScope();
				{
					var innerContext = context.CreateChild(g.CreateExpression(value));
					_innerBuilder.GenerateDeserializePart(innerContext);
				}
				g.EndScope();

				g.BeginAssign(context.Member);
				{
					g.BeginNewObject(_ctor);
					{
						g.Load(value);
					}
					g.EndNewObject();
				}
				g.EndAssign();
			}
			g.Else();
			{
				g.Load(context.Member, LoadOptions.ValueAsAddress);
				g.InitializeValue();
			}
			g.EndIf();
		}

		void IPartBuilder.GenerateFillPart(GenFillContext context)
		{
			var g = context.Generator;

			g.If(() =>
			{
				g.Load(context.NextIsNull);
				return BinaryOperator.IsTrue;
			});
			{
				g.Load(context.Member, LoadOptions.ValueAsAddress);
				g.InitializeValue();
			}
			g.Else();
			{
				g.BeginAssign(context.Member);
				{
					var value = g.DeclareLocal(_innerType);
					context.InnerFill(_innerBuilder, g.CreateExpression(value));
					g.BeginNewObject(_ctor);
					{
						g.Load(value);
					}
					g.EndNewObject();
				}
				g.EndAssign();
			}
			g.EndIf();
		}

		void IPartBuilder.GenerateAssertAreEqualPart(GenAssertAreEqualContext context)
		{
			var g = context.Generator;

			var method = typeof(Assert).ResolveMethod("AreEqual", typeof(object), typeof(object), typeof(string));

			g.BeginCall(method);
			{
				g.Load(context.Expected, LoadOptions.ValueAsAddress);
				g.LoadMember("HasValue");
				g.Box();
				g.Load(context.Actual, LoadOptions.ValueAsAddress);
				g.LoadMember("HasValue");
				g.Box();
				g.Load(context.Part.FullName);
			}
			g.EndCall();

			g.If(() =>
			{
				g.Load(context.Expected, LoadOptions.ValueAsAddress);
				g.LoadMember("HasValue");
				return BinaryOperator.IsTrue;
			});
			{
				var expected = context.Expected.Copy().AddMember("Value");
				var actual = context.Actual.Copy().AddMember("Value");

				context.GenerateInnerAssert(_innerBuilder, expected, actual);
			}
			g.EndIf();
		}

		#endregion
	}
}
