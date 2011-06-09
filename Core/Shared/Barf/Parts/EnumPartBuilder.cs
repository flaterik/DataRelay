using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using MySpace.Common.Dynamic;

namespace MySpace.Common.Barf.Parts
{
    internal class EnumPartBuilder : IPartBuilder
    {
        private readonly Type _type;
        private readonly Type _underlyingType;
        private readonly IPartBuilder _innerBuilder;
		private MethodInfo _getRandomEnumValueMethod;

        public EnumPartBuilder(Type type, PartDefinition partDefinition, IPartResolver resolver)
        {
            if (type == null) throw new ArgumentNullException("type");
            if (!type.IsEnum) throw new ArgumentException("type must be an enumeration", "type");

            _type = type;
            _underlyingType = Enum.GetUnderlyingType(type);
            _innerBuilder = 
                _underlyingType == typeof(int)
                ? new PrimitivePartBuilder(typeof(int), "ReadVarInt32", "WriteVarInt32")
				: resolver.GetPartBuilder(_underlyingType, partDefinition, true);
        }

        public Type UnderlyingType
        {
            [DebuggerStepThrough]
            get { return _underlyingType; }
        }

        #region IPartBuilder Members

        public Type Type
        {
            [DebuggerStepThrough]
            get { return _type; }
        }

        public void GenerateSerializePart(GenSerializeContext context)
        {
            _innerBuilder.GenerateSerializePart(context.CreateChild(context.Member));
        }

        public void GenerateDeserializePart(GenDeserializeContext context)
        {
            _innerBuilder.GenerateDeserializePart(context.CreateChild(context.Member));
        }

		public void GenerateFillPart(GenFillContext context)
		{
			if (_getRandomEnumValueMethod == null)
			{
				// todo - resolve method
				var methodDef = typeof(FillArgs).GetMethod("GetRandomEnumValue");
				_getRandomEnumValueMethod = methodDef.MakeGenericMethod(_type);
			}

			var g = context.Generator;

			g.BeginAssign(context.Member);
			{
				g.Load(context.FillArgs);
				g.Call(_getRandomEnumValueMethod);
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
