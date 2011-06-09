using System;
using System.Linq;
using MySpace.Common.Dynamic;

namespace MySpace.Common.Barf
{
	internal class GenDeserializeContext : GenContext
	{
		private GenDeserializeContext(
			GenDeserializeContext parent,
			IExpression instanceShortcut,
			IExpression memberShortcut)
			: base(parent.Generator)
		{
			DeserializationArgs = parent.DeserializationArgs;
			Reader = parent.Reader;
			Instance = instanceShortcut;
			Member = memberShortcut;
			Header = parent.Header;
			IsBaseType = false;
		}

		public GenDeserializeContext(
			MethodGenerator generator,
			PartDefinition part,
			IExpression instance,
			IExpression args,
			IVariable header)
			: base(generator)
		{
			ArgumentAssert.IsNotNull(generator, "generator");
			ArgumentAssert.IsNotNull(part, "part");
			ArgumentAssert.IsNotNull(instance, "instance");
			ArgumentAssert.IsNotNull(args, "args");
			ArgumentAssert.IsNotNull(header, "header");

			DeserializationArgs = args;
			Reader = args.Copy().AddMember("Reader");
			Instance = instance;
			Member = part.IsBaseType
				? instance
					.MakeReadOnly()
				: instance
					.Copy()
					.AddMember(part.Member);
			IsBaseType = part.IsBaseType;
			Header = header;
		}

		public GenDeserializeContext CreateChild(IExpression memberShortcut)
		{
			return new GenDeserializeContext(this, Member, memberShortcut);
		}

		public void GenerateRaiseInvalidData(Type type)
		{
			var method = typeof(BarfDeserializationArgs)
				.ResolveMethod("RaiseInvalidData", typeof(BarfObjectHeader))
				.MakeGenericMethod(type);
			Generator.Load(DeserializationArgs);
			Generator.BeginCall(method);
			{
				Generator.Load(Header);
			}
			Generator.EndCall();
		}

		public IExpression DeserializationArgs { get; private set; }

		public IExpression Instance { get; private set; }

		public IExpression Member { get; private set; }

		public IVariable Header { get; private set; }

		public IExpression Reader { get; private set; }

		public bool IsBaseType { get; private set; }
	}
}
