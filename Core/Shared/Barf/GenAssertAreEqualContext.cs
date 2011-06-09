using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySpace.Common.Dynamic;

namespace MySpace.Common.Barf
{
	internal class GenAssertAreEqualContext : GenContext
	{
		private readonly Stack<KeyValuePair<IExpression, IExpression>> _shortcutStack = new Stack<KeyValuePair<IExpression, IExpression>>();

		public GenAssertAreEqualContext(
			MethodGenerator generator,
			PartDefinition definition,
			IExpression expected,
			IExpression actual,
			IExpression assertArgs)
			: base(generator)
		{
			Part = definition;
			_shortcutStack.Push(new KeyValuePair<IExpression, IExpression>(
				expected,
				actual));
			AssertArgs = assertArgs;
		}

		public IExpression Expected
		{
			get { return _shortcutStack.Peek().Key; }
		}

		public IExpression Actual
		{
			get { return _shortcutStack.Peek().Value; }
		}

		public IExpression AssertArgs { get; private set; }

		public PartDefinition Part { get; private set; }

		public void GenerateSimpleAssert()
		{
			var g = Generator;
			var method = typeof(Assert).ResolveMethod("AreEqual", typeof(object), typeof(object), typeof(string));

			g.BeginCall(method);
			{
				g.Load(Expected);
				if (Expected.Type.IsValueType)
				{
					g.Box();
				}
				g.Load(Actual);
				if (Actual.Type.IsValueType)
				{
					g.Box();
				}
				g.Load(Part.FullName);
			}
			g.EndCall();
		}

		public void GenerateRaiseNotSupported()
		{
			var g = Generator;
			var method = typeof(AssertArgs).ResolveMethod("RaiseNotSupported");

			g.Load(AssertArgs);
			g.BeginCall(method);
			{
				g.Load(Part.FullName);
			}
			g.EndCall();
		}

		public void GenerateInnerAssert(IPartBuilder innerBuilder, IExpression expected, IExpression actual)
		{
			var g = Generator;
			_shortcutStack.Push(new KeyValuePair<IExpression, IExpression>(expected, actual));
			g.BeginScope();
			{
				innerBuilder.GenerateAssertAreEqualPart(this);
			}
			g.EndScope();
			_shortcutStack.Pop();
		}
	}
}
