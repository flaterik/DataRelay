using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySpace.Common.Dynamic;

namespace MySpace.Common.Barf.Parts
{
    internal class ArrayPartBuilder : IPartBuilder
    {
        private readonly Type _elementType;
        private readonly IPartBuilder _elementBuilder;

        public ArrayPartBuilder(Type arrayType, PartDefinition partDefinition, IPartResolver resolver)
        {
            if (!arrayType.IsArray)
            {
                throw new ArgumentException("arrayType is not an array", "arrayType");
            }

            if (arrayType.GetArrayRank() != 1)
            {
                throw new NotSupportedException("Multi-dimensional arrays are not supported by this class.");
            }

            _elementType = arrayType.GetElementType();
			_elementBuilder = resolver.GetPartBuilder(_elementType, partDefinition, true);
        }

        #region IPartBuilder Members

        void IPartBuilder.GenerateSerializePart(GenSerializeContext context)
		{
			var g = context.Generator;

			g.If(() =>
			{
				g.Load(context.Member);
				return BinaryOperator.IsNull;
			});
			{
				g.Load(context.Writer);
				g.BeginCall(MemberResolver.GetWriteMethod("WriteVarInt32", typeof(int)));
				{
					g.Load(-1);
				}
				g.EndCall();
			}
			g.Else();
			{
				g.Load(context.Writer);
				g.BeginCall(MemberResolver.GetWriteMethod("WriteVarInt32", typeof(int)));
				{
					g.Load(context.Member);
					g.LoadMember("Length");
				}
				g.EndCall();

				var i = g.DeclareLocal(typeof(int));
				i.Initialize();

				g.While(() =>
				{
					g.Load(i);
					g.Load(context.Member);
					g.LoadMember("Length");
					return BinaryOperator.LessThan;
				},
				() =>
				{
					var element = g.DeclareLocal(_elementType);

					g.BeginAssign(element);
					{
						g.Load(context.Member);
						g.BeginLoadElement();
						{
							g.Load(i);
						}
						g.EndLoadElement();
					}
					g.EndAssign();

					var elementContext = context.CreateChild(g.CreateExpression(element));
					_elementBuilder.GenerateSerializePart(elementContext);

					g.BeginAssign(i);
					{
						g.Load(i);
						g.Increment();
					}
					g.EndAssign();
				});
			}
			g.EndIf();
		}

        void IPartBuilder.GenerateDeserializePart(GenDeserializeContext context)
		{
			var g = context.Generator;
			var count = g.DeclareLocal(typeof(int));

			g.BeginAssign(count);
			{
				g.Load(context.Reader);
				g.Call(MemberResolver.GetReadMethod("ReadVarInt32", typeof(int)));
			}
			g.EndAssign();

			g.If(() =>
			{
				g.Load(count);
				g.Load(-1);
				return BinaryOperator.AreEqual;
			});
			{
				g.BeginAssign(context.Member);
				{
					g.LoadNull();
				}
				g.EndAssign();
			}
			g.Else();
			{
				g.BeginAssign(context.Member);
				{
					g.BeginNewArray(_elementType);
					{
						g.Load(count);
					}
					g.EndNewArray();
				}
				g.EndAssign();

				var i = g.DeclareLocal(typeof(int));
				i.Initialize();

				g.While(() =>
				{
					g.Load(i);
					g.Load(count);
					return BinaryOperator.LessThan;
				},
				() =>
				{
					var element = g.DeclareLocal(_elementType);
					g.BeginScope();
					{
						_elementBuilder.GenerateDeserializePart(context.CreateChild(g.CreateExpression(element)));
					}
					g.EndScope();

					g.Load(context.Member);
					g.BeginStoreElement();
					{
						g.Load(i);
						g.Load(element);
					}
					g.EndStoreElement();

					g.BeginAssign(i);
					{
						g.Load(i);
						g.Increment();
					}
					g.EndAssign();
				});
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
				g.BeginAssign(context.Member);
				{
					g.LoadNull();
				}
				g.EndAssign();
			}
			g.Else();
			{
				var array = g.DeclareLocal(context.Member.Type);
				var element = g.CreateExpression(g.DeclareLocal(_elementType));

				g.BeginAssign(array);
				g.BeginNewArray(_elementType);
				{
					g.Load(context.NextCollectionSize);
				}
				g.EndNewArray();
				g.EndAssign();

				var i = g.DeclareLocal(typeof(int));
				g.BeginAssign(i);
				g.Load(0);
				g.EndAssign();

				g.While(() =>
				{
					g.Load(i);
					g.Load(array);
					g.LoadMember("Length");
					return BinaryOperator.LessThan;
				}, () =>
				{
					context.InnerFill(_elementBuilder, element);

					g.Load(array);
					g.BeginStoreElement();
					{
						g.Load(i);
						g.Load(element);
					}
					g.EndStoreElement();

					g.BeginAssign(i);
					{
						g.Load(i);
						g.Increment();
					}
					g.EndAssign();
				});

				g.BeginAssign(context.Member);
				{
					g.Load(array);
				}
				g.EndAssign();
			}
			g.EndIf();
		}

		void IPartBuilder.GenerateAssertAreEqualPart(GenAssertAreEqualContext context)
		{
			var g = context.Generator;

			var assertEqual = typeof(Assert).ResolveMethod("AreEqual", typeof(object), typeof(object), typeof(string));
			var assertNotEqual = typeof(Assert).ResolveMethod("AreNotEqual", typeof(object), typeof(object), typeof(string));

			g.If(() =>
			{
				g.Load(context.Expected);
				return BinaryOperator.IsNull;
			});
			{
				g.BeginCall(assertEqual);
				{
					g.LoadNull();
					g.Load(context.Actual);
					g.Load(context.Part.FullName);
				}
				g.EndCall();
			}
			g.Else();
			{
				g.BeginCall(assertNotEqual);
				{
					g.LoadNull();
					g.Load(context.Actual);
					g.Load(context.Part.FullName);
				}
				g.EndCall();

				g.BeginCall(assertEqual);
				{
					g.Load(context.Expected);
					g.LoadMember("Length");
					g.Box();
					g.Load(context.Actual);
					g.LoadMember("Length");
					g.Box();
					g.Load(context.Part.FullName);
				}
				g.EndCall();

				var i = g.DeclareLocal(typeof(int));
				i.Initialize();

				g.While(() =>
				{
					g.Load(i);
					g.Load(context.Expected);
					g.LoadMember("Length");
					return BinaryOperator.LessThan;
				}, () =>
				{
					var expected = g.DeclareLocal(_elementType);
					var actual = g.DeclareLocal(_elementType);

					g.BeginAssign(expected);
					{
						g.Load(context.Expected);
						g.BeginLoadElement();
						{
							g.Load(i);
						}
						g.EndLoadElement();
					}
					g.EndAssign();

					g.BeginAssign(actual);
					{
						g.Load(context.Actual);
						g.BeginLoadElement();
						{
							g.Load(i);
						}
						g.EndLoadElement();
					}
					g.EndAssign();

					context.GenerateInnerAssert(_elementBuilder, g.CreateExpression(expected), g.CreateExpression(actual));

					g.BeginAssign(i);
					{
						g.Load(i);
						g.Increment();
					}
					g.EndAssign();
				});
			}
			g.EndIf();
		}

		#endregion
	}
}
