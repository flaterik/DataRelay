using System;
using MySpace.Common.Dynamic;

namespace MySpace.Common.Barf
{
	internal class GenSerializeContext : GenContext
	{
		private const int _serializationArgsIndex = 1;

		private GenSerializeContext(
			GenSerializeContext parent,
			IExpression instance,
			IExpression member)
			: base(parent.Generator)
		{
			Writer = parent.Writer;
			Instance = instance;
			Member = member;
		}

		public GenSerializeContext(
			MethodGenerator generator,
			PartDefinition part,
			IExpression instance)
			: base(generator)
		{
			ArgumentAssert.IsNotNull(generator, "generator");
			ArgumentAssert.IsNotNull(part, "part");
			ArgumentAssert.IsNotNull(instance, "instance");

			Writer = generator.CreateExpression(generator.GetParameter(_serializationArgsIndex)).AddMember("Writer");
			Instance = instance;
			Member = part.IsBaseType
				? instance
					.MakeReadOnly()
				: instance
					.Copy()
					.AddMember(part.Member);
		}

		public GenSerializeContext CreateChild(IExpression memberShortcut)
		{
			return new GenSerializeContext(this, Member, memberShortcut);
		}

		public IVariable SerializationArgs
		{
			get { return Generator.GetParameter(_serializationArgsIndex); }
		}

		public IExpression Instance { get; private set; }

		public IExpression Member { get; private set; }

		public IExpression Writer { get; private set; }
	}
}
