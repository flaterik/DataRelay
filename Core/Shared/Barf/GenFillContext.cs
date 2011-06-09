using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MySpace.Common.Dynamic;

namespace MySpace.Common.Barf
{
	internal class GenFillContext : GenContext
	{
		private readonly IExpression _fillArgs;
		private readonly Stack<IExpression> _members = new Stack<IExpression>();

		public GenFillContext(MethodGenerator generator, IExpression memberShortcut, PartDefinition part)
			: base(generator)
		{
			_fillArgs = generator.CreateExpression(generator.GetParameter(1));

			_members.Push(memberShortcut);
			Part = part;

			NextIsNull = _fillArgs
				.Copy()
				.AddMember("NextIsNull");

			NextCollectionSize = _fillArgs
				.Copy()
				.AddMember("NextCollectionSize");
		}

		public IExpression FillArgs
		{
			[DebuggerStepThrough]
			get { return _fillArgs; }
		}

		public IExpression Member
		{
			get { return _members.Peek(); }
		}

		public bool IsBaseType
		{
			get { return _members.Count == 1 && Part.IsBaseType; }
		}

		public PartDefinition Part { get; private set; }

		public IExpression NextIsNull { get; private set; }

		public IExpression NextCollectionSize { get; private set; }

		public void GenerateSkippedWarning(bool assignDefault, string message)
		{
			var g = Generator;

			var method = typeof(FillArgs).ResolveMethod("RaiseSkippedWarning", typeof(string), typeof(string));

			g.Load(FillArgs);
			g.BeginCall(method);
			{
				g.Load(Part.FullName);
				g.Load(message);
			}
			g.EndCall();

			if (assignDefault)
			{
				if (!Member.IsReadOnly)
				{
					g.BeginAssign(Member);
					{
						g.LoadDefaultOf(Member.Type);
					}
					g.EndAssign();
				}
			}
		}

		public void InnerFill(IPartBuilder innerBuilder, IExpression innerMember)
		{
			Generator.BeginScope();
			_members.Push(innerMember);
			innerBuilder.GenerateFillPart(this);
			_members.Pop();
			Generator.EndScope();
		}
	}
}
