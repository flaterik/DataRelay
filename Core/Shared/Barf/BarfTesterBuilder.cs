using System;
using System.Linq;
using MySpace.Common.Dynamic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MySpace.Common.Barf
{
	internal class BarfTesterBuilder
	{
		private readonly BarfTypeDefinition _def;

		public BarfTesterBuilder(BarfTypeDefinition typeDefinition)
		{
			if (typeDefinition == null) throw new ArgumentNullException("typeDefinition");

			_def = typeDefinition;
		}

		public void GenerateFill(ICilWriter msilWriter)
		{
			var g = new MethodGenerator(msilWriter);

			var instance = g.GetParameter(0);

			g.If(() =>
			{
				g.Load(instance);
				return BinaryOperator.IsNull;
			});
			{
				if (_def.Type.IsAbstract)
				{
					g.BeginCall(typeof(BarfErrors).ResolveMethod("RaiseAbstractConstructionError", typeof(Type)));
					g.Load(_def.Type);
					g.EndCall();
				}
				else
				{
					g.BeginAssign(instance);
					{
						g.NewObject(_def.Type);
					}
					g.EndAssign();
				}
			}
			g.EndIf();

			foreach (var part in _def.Parts)
			{
				g.BeginScope();
				{
					var member = g.CreateExpression(instance);
					if (part.Member == null)
					{
						member.MakeReadOnly();
					}
					else
					{
						member.AddMember(part.Member);
					}
					var context = new GenFillContext(g, member, part);

					var builder = PartResolver.Current.GetPartBuilder(part.Type, part, true);

					builder.GenerateFillPart(context);
				}
				g.EndScope();
			}

			g.Return();
		}

		public void GenerateAssertAreEqual(ICilWriter msilWriter)
		{
			var g = new MethodGenerator(msilWriter);

			var expected = g.GetParameter(0);
			var actual = g.GetParameter(1);

			var assertAreEqual = typeof(Assert).ResolveMethod("AreEqual", typeof(object), typeof(object));
			var assertAreNotEqual = typeof(Assert).ResolveMethod("AreNotEqual", typeof(object), typeof(object));

			if (_def.Type.IsValueType)
			{
				GenerateAssertPartsAreEqual(g);
			}
			else
			{
				g.If(() =>
				{
					g.Load(expected);
					return BinaryOperator.IsNull;
				});
				{
					g.BeginCall(assertAreEqual);
					{
						g.LoadNull();
						g.Load(actual);
					}
					g.EndCall();
				}
				g.Else();
				{
					g.BeginCall(assertAreNotEqual);
					{
						g.LoadNull();
						g.Load(actual);
					}
					g.EndCall();

					GenerateAssertPartsAreEqual(g);
				}
				g.EndIf();
			}

			g.Return();
		}

		private void GenerateAssertPartsAreEqual(MethodGenerator g)
		{
			var expected = g.GetParameter(0);
			var actual = g.GetParameter(1);
			var assertAgs = g.GetParameter(2);

			foreach (var part in _def.Parts)
			{
				g.BeginScope();
				{
					var e = g.CreateExpression(expected);
					var a = g.CreateExpression(actual);
					if (part.Member == null)
					{
						e.MakeReadOnly();
						a.MakeReadOnly();
					}
					else
					{
						e.AddMember(part.Member);
						a.AddMember(part.Member);
					}
					var context = new GenAssertAreEqualContext(g, part, e, a, g.CreateExpression(assertAgs));

					part.GetCurrentBuilder()
						.GenerateAssertAreEqualPart(context);
				}
				g.EndScope();
			}
		}
	}
}
