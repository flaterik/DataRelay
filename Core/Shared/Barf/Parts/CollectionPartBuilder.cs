using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySpace.Common.Dynamic;

namespace MySpace.Common.Barf.Parts
{
	internal class CollectionPartBuilder : IPartBuilder
	{
		private enum CtorType
		{
			Capacity,
			Default
		}

		private readonly Type _collectionType;
		private readonly Type _elementType;
		private readonly IPartBuilder _innerBuilder;
		private readonly MethodInfo _getCount;
		private readonly MethodInfo _add;
		private readonly MethodInfo _contains;
		private readonly ConstructorInfo _ctor;
		private readonly CtorType _ctorType;

		public CollectionPartBuilder(Type collectionType, Type elementType, PartDefinition partDefinition, IPartResolver resolver)
			: this(collectionType, elementType, partDefinition, resolver, collectionType)
		{
		}

		public CollectionPartBuilder(Type collectionType, Type elementType, PartDefinition partDefinition, IPartResolver resolver, Type underlyingType)
		{
			if (collectionType == null) throw new ArgumentNullException("collectionType");
			if (elementType == null) throw new ArgumentNullException("elementType");
			if (partDefinition == null) throw new ArgumentNullException("partDefinition");
			if (resolver == null) throw new ArgumentNullException("resolver");

			_collectionType = collectionType;
			_elementType = elementType;
			_innerBuilder = resolver.GetPartBuilder(_elementType, partDefinition, true);

			if (_innerBuilder == null)
			{
				throw new NotSupportedException(string.Format("Type '{0}' cannot be serialized", elementType));
			}

			var interfaceType = typeof(ICollection<>).MakeGenericType(elementType);

			if (!interfaceType.IsAssignableFrom(collectionType))
			{
				throw new ArgumentException(string.Format(
					"collectionType '{0}' is not assignable to '{1}'",
					collectionType.Name,
					interfaceType.Name),
					"collectionType");
			}

			_getCount = _collectionType.GetBestCallableOverride(interfaceType.ResolveProperty("Count").GetGetMethod());
			_add = _collectionType.GetBestCallableOverride(interfaceType.ResolveMethod("Add", new[] { elementType }));
			_contains = _collectionType.GetBestCallableOverride(interfaceType.ResolveMethod("Contains", elementType));

			const BindingFlags bindingFlags =
				BindingFlags.CreateInstance
				| BindingFlags.Public
				| BindingFlags.NonPublic
				| BindingFlags.Instance;

			_ctor = underlyingType.GetConstructor(bindingFlags, null, new[] { typeof(int) }, null);
			_ctorType = CtorType.Capacity;
			if (_ctor == null || _ctor.GetParameters()[0].Name != "capacity")
			{
				_ctor = underlyingType.GetConstructor(bindingFlags, null, Type.EmptyTypes, null);
				_ctorType = CtorType.Default;
			}

			if (_ctor == null)
			{
				throw new ArgumentException("collectionType='" + collectionType.Name + "' does not define .ctor(int capacity) or .ctor()", "collectionType");
			}
		}

		#region IPartBuilder Members

		public void GenerateSerializePart(GenSerializeContext context)
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
					g.BeginCall(_getCount);
					g.EndCall();
				}
				g.EndCall();

				g.Load(context.Member);
				var item = g.BeginForEach(_elementType);
				{
					var innerContext = context.CreateChild(g.CreateExpression(item));
					_innerBuilder.GenerateSerializePart(innerContext);
				}
				g.EndForEach();
			}
			g.EndIf();
		}

		public void GenerateDeserializePart(GenDeserializeContext context)
		{
			var g = context.Generator;
			var count = g.DeclareLocal(typeof(int));
			var collection = context.Member.IsReadOnly
				? context.Member
				: g.DeclareLocal(_collectionType);

			g.BeginAssign(count);
			{
				g.Load(context.Reader);
				g.Call(MemberResolver.GetReadMethod("ReadVarInt32", typeof(int)));
			}
			g.EndAssign();

			g.If(() =>
			{
				g.Load(count);
				g.Load(0);
				return BinaryOperator.LessThan;
			});
			{
				if (context.Member.IsReadOnly)
				{
					context.GenerateRaiseInvalidData(_collectionType);
				}
				else
				{
					g.BeginAssign(collection);
					{
						g.LoadDefaultOf(_collectionType);
					}
					g.EndAssign();
				}
			}
			g.Else();
			{
				if (!context.Member.IsReadOnly)
				{
					g.BeginAssign(collection);
					g.BeginNewObject(_ctor);
					{
						if (_ctorType == CtorType.Capacity)
						{
							g.Load(count);
						}
					}
					g.EndNewObject();
					g.EndAssign();
				}

				g.While(() =>
				{
					g.Load(count);
					g.Load(0);
					return BinaryOperator.GreaterThan;
				},
				() =>
				{
					var element = g.DeclareLocal(_elementType);

					g.BeginScope();
					{
						var innerContext = context.CreateChild(g.CreateExpression(element));
						_innerBuilder.GenerateDeserializePart(innerContext);
					}
					g.EndScope();

					g.Load(collection);
					g.BeginCall(_add);
					{
						g.Load(element);
					}
					g.EndCall();

					g.BeginAssign(count);
					{
						g.Load(count);
						g.Decrement();
					}
					g.EndAssign();
				});
			}
			g.EndIf();

			if (!context.Member.IsReadOnly)
			{
				g.BeginAssign(context.Member);
				{
					g.Load(collection);
				}
				g.EndAssign();
			}
		}

		private void GenerateFillCollection(GenFillContext context, IVariable collection)
		{
			var g = context.Generator;

			var count = g.DeclareLocal(typeof(int));
			var element = g.CreateExpression(g.DeclareLocal(_elementType));

			g.BeginAssign(count);
			g.Load(context.NextCollectionSize);
			g.EndAssign();

			g.While(() =>
			{
				g.Load(count);
				g.Load(0);
				return BinaryOperator.GreaterThan;
			}, () =>
			{
				context.InnerFill(_innerBuilder, element);

				bool endIf = false;
				if (!_elementType.IsValueType)
				{
					g.If(() =>
					{
						g.Load(element);
						g.LoadNull();
						return BinaryOperator.AreNotEqual;
					});
					endIf = true;
				}
				else if (_elementType.IsGenericType && _elementType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
				{
					var keyType = _elementType.GetGenericArguments()[0];
					if (!keyType.IsValueType)
					{
						g.If(() =>
						{
							g.Load(element, LoadOptions.ValueAsAddress);
							g.LoadMember("Key");
							g.LoadNull();
							return BinaryOperator.AreNotEqual;
						});
						endIf = true;
					}
				}
				{
					g.If(() =>
					{
						g.Load(collection);
						g.BeginCall(_contains);
						{
							g.Load(element);
						}
						g.EndCall();
						return BinaryOperator.IsFalse;
					});
					{
						g.Load(collection);
						g.BeginCall(_add);
						{
							g.Load(element);
						}
						g.EndCall();
					}
					g.EndIf();
				}
				if (endIf)
				{
					g.EndIf();
				}

				g.BeginAssign(count);
				{
					g.Load(count);
					g.Decrement();
				}
				g.EndAssign();
			});
		}

		public void GenerateFillPart(GenFillContext context)
		{
			var g = context.Generator;

			if (context.Member.IsReadOnly)
			{
				g.If(() =>
				{
					g.Load(context.NextIsNull);
					return BinaryOperator.IsFalse;
				});
				{
					GenerateFillCollection(context, context.Member);
				}
				g.EndIf();
			}
			else
			{
				g.If(() =>
				{
					g.Load(context.NextIsNull);
					return BinaryOperator.IsTrue;
				});
				{
					g.BeginAssign(context.Member);
					{
						g.LoadDefaultOf(_collectionType);
					}
					g.EndAssign();
				}
				g.Else();
				{
					g.BeginAssign(context.Member);
					g.BeginNewObject(_ctor);
					{
						if (_ctorType == CtorType.Capacity)
						{
							g.Load(1);
						}
					}
					g.EndNewObject();
					g.EndAssign();

					GenerateFillCollection(context, context.Member);
				}
				g.EndIf();
			}
		}

		public void GenerateAssertAreEqualPart(GenAssertAreEqualContext context)
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
					g.Call(_getCount);
					g.Box();
					g.Load(context.Actual);
					g.Call(_getCount);
					g.Box();
					g.Load(context.Part.FullName);
				}
				g.EndCall();

				// todo - make this method resolution better
				var joinPairs = typeof(Algorithm).GetMethod("JoinPairs", BindingFlags.Public | BindingFlags.Static);
				joinPairs = joinPairs.MakeGenericMethod(_elementType, _elementType);
				g.BeginCall(joinPairs);
				{
					g.Load(context.Expected);
					g.Load(context.Actual);
				}
				g.EndCall();
				var pair = g.BeginForEach(typeof(KeyValuePair<,>).MakeGenericType(_elementType, _elementType));
				{
					context.GenerateInnerAssert(
						_innerBuilder,
						g.CreateExpression(pair).AddMember("Key"),
						g.CreateExpression(pair).AddMember("Value"));
				}
				g.EndForEach();
			}
			g.EndIf();
		}

		#endregion
	}
}
