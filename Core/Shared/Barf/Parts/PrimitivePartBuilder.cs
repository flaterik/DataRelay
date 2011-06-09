using System;
using System.Diagnostics;
using MySpace.Common.Dynamic;

namespace MySpace.Common.Barf.Parts
{
    internal class PrimitivePartBuilder : IPartBuilder
    {
        private readonly Type _type;
        private readonly string _readMethodName;
        private readonly string _writeMethodName;

        public PrimitivePartBuilder(Type type)
            : this(type, "Read" + type.Name, "Write")
        {
        }

        public PrimitivePartBuilder(Type type, string readMethodName, string writeMethodName)
        {
            if (type == null) throw new ArgumentNullException("type");
            if (string.IsNullOrEmpty(readMethodName)) throw new ArgumentNullException("readMethodName");
            if (string.IsNullOrEmpty(writeMethodName)) throw new ArgumentNullException("writeMethodName");

            _type = type;
            _readMethodName = readMethodName;
            _writeMethodName = writeMethodName;
        }

        #region IBarfPartBuilder Members

        public Type Type
        {
            [DebuggerStepThrough]
            get { return _type; }
        }

        public void GenerateSerializePart(GenSerializeContext context)
        {
            var g = context.Generator;
            g.Load(context.Writer);
			g.BeginCall(MemberResolver.GetWriteMethod(_writeMethodName, Type));
            {
                g.Load(context.Member);
            }
            g.EndCall();
        }

        public void GenerateDeserializePart(GenDeserializeContext context)
		{
			var g = context.Generator;
			g.BeginAssign(context.Member);
			{
				g.Load(context.Reader);
				g.Call(MemberResolver.GetReadMethod(_readMethodName, Type));
			}
			g.EndAssign();
		}

		public void GenerateFillPart(GenFillContext context)
		{
			var g = context.Generator;
			var property = typeof(FillArgs).ResolveProperty("Next" + _type.Name);

			if (property == null)
			{
				throw new MissingMemberException(typeof(FillArgs).Name, "Next" + _type.Name);
			}

			g.BeginAssign(context.Member);
			{
				g.Load(context.FillArgs);
				g.LoadProperty(property);
			}
			g.EndAssign();
		}

		public void GenerateAssertAreEqualPart(GenAssertAreEqualContext context)
		{
			context.GenerateSimpleAssert();
		}

		#endregion
	}
}
