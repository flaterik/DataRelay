using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace MySpace.Common.Barf.Parts
{
    internal class KeyValuePairPartBuilder : IPartBuilder
    {
        private readonly Type _type;
        private readonly Type _keyType;
        private readonly Type _valueType;
        private readonly IPartBuilder _keyBuilder;
        private readonly IPartBuilder _valueBuilder;
        private readonly ConstructorInfo _ctor;

        public KeyValuePairPartBuilder(Type type, PartDefinition partDefinition, IPartResolver resolver)
        {
            if (type == null) throw new ArgumentNullException("type");

            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(KeyValuePair<,>))
            {
                throw new ArgumentException("type must be a KeyValuePair<,>", "type");
            }

            if (type.IsGenericTypeDefinition)
            {
                throw new ArgumentException("The type parameters of type are not defined", "type");
            }

            _type = type;
            var typeArgs = _type.GetGenericArguments();
            _keyType = typeArgs[0];
            _valueType = typeArgs[1];
			_keyBuilder = resolver.GetPartBuilder(_keyType, partDefinition, true);
			_valueBuilder = resolver.GetPartBuilder(_valueType, partDefinition, true);
            _ctor = _type.GetConstructor(new[] { _keyType, _valueType });
            Debug.Assert(_ctor != null, "Couldn't find KeyValuePair constructor");
        }

        #region IPartBuilder Members

		public void GenerateSerializePart(GenSerializeContext context)
        {
			_keyBuilder.GenerateSerializePart(context.CreateChild(
                context.Member
                    .Copy()
                    .AddMember("Key")));

			_valueBuilder.GenerateSerializePart(context.CreateChild(
                context.Member
                    .Copy()
                    .AddMember("Value")));
        }

        public void GenerateDeserializePart(GenDeserializeContext context)
		{
			var g = context.Generator;

			var key = g.DeclareLocal(_keyType);
			var value = g.DeclareLocal(_valueType);

			g.BeginScope();
			{
				_keyBuilder.GenerateDeserializePart(context.CreateChild(g.CreateExpression(key)));
			}
			g.EndScope();

			g.BeginScope();
			{
				_valueBuilder.GenerateDeserializePart(context.CreateChild(g.CreateExpression(value)));
			}
			g.EndScope();

			g.BeginAssign(context.Member);
			{
				g.BeginNewObject(_ctor);
				{
					g.Load(key);
					g.Load(value);
				}
				g.EndNewObject();
			}
			g.EndAssign();
		}

		public void GenerateFillPart(GenFillContext context)
		{
			var g = context.Generator;

			var key = g.CreateExpression(g.DeclareLocal(_keyType));
			var value = g.CreateExpression(g.DeclareLocal(_valueType));

			context.InnerFill(_keyBuilder, key);
			context.InnerFill(_valueBuilder, value);

			g.BeginAssign(context.Member);
			{
				g.BeginNewObject(_ctor);
				{
					g.Load(key);
					g.Load(value);
				}
				g.EndNewObject();
			}
			g.EndAssign();
		}

		public void GenerateAssertAreEqualPart(GenAssertAreEqualContext context)
		{
			context.GenerateInnerAssert(_keyBuilder, context.Expected.Copy().AddMember("Key"), context.Actual.Copy().AddMember("Key"));
			context.GenerateInnerAssert(_valueBuilder, context.Expected.Copy().AddMember("Value"), context.Actual.Copy().AddMember("Value"));
		}

		#endregion
	}
}
