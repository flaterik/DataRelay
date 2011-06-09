using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace MySpace.Common.Dynamic
{
	/// <summary>
	/// Encapsulates a functionality for writing a method given a <see cref="ICilWriter"/> instance.
	/// This class has nice features like loops and evaluation stack validation that wouldn't
	/// normally be available by coding against <see cref="ILGenerator"/> or <see cref"ICilWriter"/>
	/// directly.
	/// </summary>
	[DebuggerDisplay("ScopeDepth={_scopeStack.Count}, EvalStackDepth={_evalStack.Count}")]
	public class MethodGenerator
	{
		private static readonly MethodInfo _disposeMethod = typeof(IDisposable).GetMethod("Dispose");

		private struct ConversionKey
		{
			public ConversionKey(Type to, bool overflowCheck)
			{
				To = to;
				OverflowCheck = overflowCheck;
			}

			public readonly Type To;
			public readonly bool OverflowCheck;
		}

		private class ConversionKeyEqualityComparer : IEqualityComparer<ConversionKey>
		{
			public bool Equals(ConversionKey x, ConversionKey y)
			{
				return x.OverflowCheck == y.OverflowCheck
					&& x.To == y.To;
			}

			public int GetHashCode(ConversionKey obj)
			{
				return obj.To.GetHashCode() ^ obj.OverflowCheck.GetHashCode();
			}
		}

		private static readonly Dictionary<ConversionKey, OpCode> _conversions =
			new Dictionary<ConversionKey, OpCode>(new ConversionKeyEqualityComparer())
		{
			// signed, no overflow
			{ new ConversionKey(typeof(sbyte), false), OpCodes.Conv_I1 },
			{ new ConversionKey(typeof(short), false), OpCodes.Conv_I2 },
			{ new ConversionKey(typeof(int), false), OpCodes.Conv_I4 },
			{ new ConversionKey(typeof(long), false), OpCodes.Conv_I8 },
			// unsigned, no overflow
			{ new ConversionKey(typeof(byte), false), OpCodes.Conv_I1 },
			{ new ConversionKey(typeof(ushort), false), OpCodes.Conv_I2 },
			{ new ConversionKey(typeof(uint), false), OpCodes.Conv_I4 },
			{ new ConversionKey(typeof(ulong), false), OpCodes.Conv_I8 },
			// signed, with overflow
			{ new ConversionKey(typeof(sbyte), true), OpCodes.Conv_Ovf_I1 },
			{ new ConversionKey(typeof(short), true), OpCodes.Conv_Ovf_I2 },
			{ new ConversionKey(typeof(int), true), OpCodes.Conv_Ovf_I4 },
			{ new ConversionKey(typeof(long), true), OpCodes.Conv_Ovf_I8 },
			// unsigned, with overflow
			{ new ConversionKey(typeof(byte), true), OpCodes.Conv_Ovf_I1_Un },
			{ new ConversionKey(typeof(ushort), true), OpCodes.Conv_Ovf_I2_Un },
			{ new ConversionKey(typeof(uint), true), OpCodes.Conv_Ovf_I4_Un },
			{ new ConversionKey(typeof(ulong), true), OpCodes.Conv_Ovf_I8_Un }
		};

		private readonly ICilWriter _writer;
		private readonly MethodHeader _header;
		private Parameter _thisParameter;
		private readonly Factory<int, Parameter> _parameters;
		private readonly List<LocalDefinition> _locals = new List<LocalDefinition>();
		private readonly EvaluationStack _evalStack = new EvaluationStack();
		private readonly Stack<Scope> _scopeStack = new Stack<Scope>();
		private readonly Stack<Action> _delayedInstructions = new Stack<Action>();
		private Scope _currentScope;

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="MethodGenerator"/> class.</para>
		/// </summary>
		/// <param name="writer">The writer to write to.</param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="writer"/> is <see langword="null"/>.</para>
		///	<para>- or -</para>
		///	<para><paramref name="writer.MethodHeader"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentException">
		///	<para><paramref name="writer.MethodHeader"/> must define <see cref="MethodHeader.DeclaringType"/> for non-static methods.</para>
		/// </exception>
		public MethodGenerator(ICilWriter writer)
		{
			if (writer == null) throw new ArgumentNullException("writer");

			var header = writer.MethodHeader;

			if (header.DeclaringType == null && (header.Attributes & CallingConventions.HasThis) == CallingConventions.HasThis)
			{
				throw new ArgumentException("header must define a DeclaringType for non-static methods");
			}

			for (int i = 0; i < header.ParameterTypes.Length; ++i)
			{
				if (header.ParameterTypes[i] == null) throw new ArgumentException("writer.Header.Parameters may not contain null values", "writer");
			}

			_writer = writer;
			_header = header;
			_parameters = Algorithm.LazyIndexer<int, Parameter>(parameterIndex =>
			{
				if ((_header.Attributes & CallingConventions.HasThis) == CallingConventions.HasThis)
				{
					++parameterIndex;
				}
				return new Parameter(this, _writer.GetParameter(parameterIndex));
			});
			_currentScope = new Scope(this);
		}

		public void LoadDefaultOf(Type type)
		{
			// todo - load primitives differently
			if (type.IsGenericParameter || type.IsValueType)
			{
				PushScope(new Scope(this, 1));
				{
					var temp = DeclareLocal(type);
					Load(temp, LoadOptions.AnyAsAddress);
					_evalStack.Pop();
					_writer.Emit(OpCodes.Initobj, type);
					Load(temp);
				}
				PopScope(null, null);
			}
			else
			{
				LoadNull();
			}
		}

		public void While(Factory<BinaryOperator> condition, Action loop)
		{
			var startLabel = _writer.DefineLabel();
			var conditionLabel = _writer.DefineLabel();

			PushScope(new Scope(this));

			_writer.Emit(OpCodes.Br, conditionLabel);
			startLabel.Mark();
			loop();
			conditionLabel.Mark();
			var c = condition();
			GoToIf(c, startLabel);

			PopScope(typeof(Scope), "Scope opened but not closed within the while loop or condition.");
		}

		public IExpression CreateExpression(IVariable startVariable)
		{
			if (startVariable == null) throw new ArgumentNullException("startVariable");

			return new Expression(this, startVariable);
		}

		public MethodGenerator Box()
		{
			var item = _evalStack.Pop();
			if (!item.Type.IsValueType)
			{
				throw new InvalidOperationException(string.Format("The current type ({0}) on the eval stack is not a value type", item.Type.Name));
			}
			if (item.IsAddress)
			{
				throw new InvalidOperationException("An address is on the evaluation stack. Boxing requres a value on the evaluation stack.");
			}
			_writer.Emit(OpCodes.Box, item.Type);
			_evalStack.Push(item.Type, LoadOptions.BoxValues);
			return this;
		}

		public void Convert(Type toType, bool withOverflowCheck)
		{
			var item = _evalStack.Pop();
			StackAssert.IsPrimitive(item);
			OpCode conversionCode;
			if (_conversions.TryGetValue(new ConversionKey(toType, withOverflowCheck), out conversionCode))
			{
				_writer.Emit(conversionCode);
				_evalStack.Push(toType);
				return;
			}

			throw new ArgumentException("Can't convert to type " + toType.Name, "toType");
		}

		public void Pop()
		{
			_evalStack.Pop();
			_writer.Emit(OpCodes.Pop);
		}

		public void BeginNewArray(Type elementType)
		{
			BeginNewArray(elementType, 1);
		}

		public void BeginNewArray(Type elementType, int rank)
		{
			if (elementType == null) throw new ArgumentNullException("elementType");
			if (rank < 1) throw new ArgumentOutOfRangeException("rank", "rank must be at least 1");

			var arrayType = rank == 1 ? elementType.MakeArrayType() : elementType.MakeArrayType(rank);
			_delayedInstructions.Push(() =>
			{
				for (int i = 0; i < rank; ++i)
				{
					StackAssert.AreEqual(_evalStack.Pop(), typeof(int));
				}

				if (rank == 1)
				{
					_writer.Emit(OpCodes.Newarr, elementType);
				}
				else
				{
					var paramTypes = new Type[rank];
					for (int i = 0; i < paramTypes.Length; ++i)
					{
						paramTypes[i] = typeof(int);
					}

					_writer.Emit(OpCodes.Newobj, arrayType.GetConstructor(paramTypes));
				}
				_evalStack.Push(arrayType);
			});
		}

		public void EndNewArray()
		{
			_delayedInstructions.Pop()();
		}

		public void BeginLoadElement()
		{
			_delayedInstructions.Push(() =>
			{
				var item = _evalStack.Pop();
				StackAssert.IsAssignable(item, new StackItem(typeof(int)), false);
				item = _evalStack.Pop();
				StackAssert.IsAssignable(item, new StackItem(typeof(Array)), false);

				Debug.Assert(item.Type.IsArray, "item should be an array type but isn't");

				var elementType = item.Type.GetElementType();
				_writer.EmitLdelem(elementType);
				_evalStack.Push(elementType);
			});
		}

		public void EndLoadElement()
		{
			_delayedInstructions.Pop()();
		}

		public void BeginStoreElement()
		{
			_delayedInstructions.Push(() =>
			{
				// value to store
				var valueItem = _evalStack.Pop();

				// int32 index
				StackAssert.IsAssignable(_evalStack.Pop(), typeof(int), false);

				// array reference
				var arrayItem = _evalStack.Pop();
				StackAssert.IsAssignable(arrayItem, typeof(Array), false);

				Debug.Assert(arrayItem.Type.IsArray, "item should be an array type but isn't");

				var elementType = arrayItem.Type.GetElementType();

				StackAssert.IsAssignable(valueItem, new StackItem(elementType, LoadOptions.Default), true);

				_writer.EmitStelem(elementType);
			});
		}

		public void EndStoreElement()
		{
			_delayedInstructions.Pop()();
		}

		public void BeginNewObject(ConstructorInfo constructor)
		{
			if (constructor == null) throw new ArgumentNullException("constructor");
			if (constructor.IsStatic) throw new ArgumentException("constructor must be an instance constructor", "constructor");

			_delayedInstructions.Push(() =>
			{
				var parameters = constructor.GetParameters();
				for (int i = parameters.Length - 1; i >= 0; --i)
				{
					// todo - account for ref & out parameters
					StackAssert.IsAssignable(_evalStack.Pop(), new StackItem(parameters[i].ParameterType, LoadOptions.Default), true);
				}
				_writer.Emit(OpCodes.Newobj, constructor);
				_evalStack.Push(constructor.DeclaringType);
			});
		}

		public void EndNewObject()
		{
			_delayedInstructions.Pop()();
		}

		public MethodGenerator NewObject(Type declaringType, params Type[] parameterTypes)
		{
			if (declaringType == null) throw new ArgumentNullException("declaringType");
			if (declaringType.IsArray) throw new ArgumentException("declaringType may not be an array type. Use NewArray instead", "declaringType");

			parameterTypes = parameterTypes ?? Type.EmptyTypes;

			var ctor = declaringType.GetConstructor(
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
				null,
				parameterTypes,
				null);

			if (ctor == null)
			{
				var parameters = string.Join(", ", parameterTypes.Select<Type, string>(t => t.Name).ToArray<string>());

				throw new MissingMemberException(declaringType.Name, ".ctor(" + parameters + ")");
			}

			return NewObject(ctor);
		}

		public MethodGenerator NewObject(ConstructorInfo constructor)
		{
			if (constructor == null) throw new ArgumentNullException("constructor");

			if (constructor.DeclaringType.IsArray)
			{
				throw new ArgumentException("constructor may not be an array constructor", "constructor");
			}

			PopAndValidateParams(constructor);

			_writer.Emit(OpCodes.Newobj, constructor);
			_evalStack.Push(constructor.DeclaringType);
			return this;
		}

		public void Throw()
		{
			var item = _evalStack.Pop();
			StackAssert.IsAssignable(item, typeof(Exception), false);
			_writer.Emit(OpCodes.Throw);
		}

		public void Ternary(Type type, LoadOptions options, Factory<BinaryOperator> condition, Action trueValue, Action falseValue)
		{
			if (condition == null) throw new ArgumentNullException("condition");
			if (trueValue == null) throw new ArgumentNullException("trueValue");
			if (falseValue == null) throw new ArgumentNullException("falseValue");

			var trueLabel = _writer.DefineLabel();
			var doneLabel = _writer.DefineLabel();

			var c = condition();
			GoToIf(c, trueLabel);
			int count = _evalStack.Count;
			falseValue();
			if (_evalStack.Count != count + 1)
			{
				throw new StackValidationException("The ternary operator expects exactly one more item on the evaluation stack after falseValue completes");
			}
			var falseResult = _evalStack.Pop();
			StackAssert.IsAssignable(falseResult, new StackItem(type, options), true);

			_writer.Emit(OpCodes.Br, doneLabel);
			trueLabel.Mark();
			count = _evalStack.Count();
			trueValue();
			if (_evalStack.Count != count + 1)
			{
				throw new StackValidationException("The ternary operator expects exactly one more item on the evaluation stack after trueValue completes");
			}
			var trueResult = _evalStack.Pop();
			StackAssert.IsAssignable(trueResult, new StackItem(type, options), true);

			doneLabel.Mark();

			_evalStack.Push(type, options);
		}

		public void If(Factory<BinaryOperator> condition)
		{
			if (condition == null) throw new ArgumentNullException("condition");

			var c = condition();
			var ifScope = new LabeledScope(this);
			GoToIf(c.Negate(), ifScope.EndLabel);
			PushScope(ifScope);
		}

		public void Compare(BinaryOperator op)
		{
			for (int i = 0; i < op.ArgumentCount; ++i)
			{
				// todo validate better
				_evalStack.Pop();
			}
			op.WriteCompare(_writer);
			_evalStack.Push(typeof(bool));
		}

		public void Else()
		{
			var elseScope = new LabeledScope(this);
			_writer.Emit(OpCodes.Br, elseScope.EndLabel);
			PopScope(typeof(LabeledScope), "Else without If");
			PushScope(elseScope);
		}

		public void EndIf()
		{
			PopScope(typeof(LabeledScope), "EndIf without If");
		}

		public Type ReturnType
		{
			get { return _header.ReturnType; }
		}

		public IVariable This
		{
			get
			{
				if (_thisParameter == null)
				{
					if ((_header.Attributes & CallingConventions.HasThis) != CallingConventions.HasThis)
					{
						throw new InvalidOperationException("MethodAttributes.HasThis must be set");
					}
					_thisParameter = new Parameter(this, _writer.GetParameter(0));
				}
				return _thisParameter;
			}
		}

		public IVariable GetParameter(int index)
		{
			return _parameters(index);
		}

		public IVariable DeclareLocal(Type type)
		{
			return DeclareLocal(type, false);
		}

		public IVariable DeclareLocal(Type type, bool isPinned)
		{
			return _currentScope.DeclareLocal(type, isPinned);
		}

		public void Cast(Type toType)
		{
			if (toType.IsValueType)
			{
				throw new NotImplementedException();
			}
			var item = _evalStack.Pop();
			if (item.ItemType != ItemType.Reference)
			{
				throw new InvalidOperationException("Expected a reference type on the eval stack");
			}
			// todo validate type
			_writer.Emit(OpCodes.Castclass, toType);
			_evalStack.Push(toType);
		}

		public MethodGenerator LoadNull()
		{
			_writer.Emit(OpCodes.Ldnull);
			_evalStack.Push(typeof(int));
			return this;
		}

		public MethodGenerator Load(int value)
		{
			_writer.EmitLdcI4(value);
			_evalStack.Push(typeof(int));
			return this;
		}

		public MethodGenerator Load(bool value)
		{
			return Load(value ? 1 : 0);
		}

		public MethodGenerator Load(string value)
		{
			if (value == null)
			{
				return LoadNull();
			}
			if (value.Length == 0)
			{
				return LoadField(typeof(string).ResolveField("Empty"));
			}

			_writer.Emit(OpCodes.Ldstr, value);
			_evalStack.Push(typeof(string));
			return this;
		}

		public MethodGenerator Load(IVariable variable)
		{
			return Load(variable, LoadOptions.Default);
		}

		public MethodGenerator Load(IVariable variable, LoadOptions options)
		{
			variable.Load(options);
			return this;
		}

		public MethodGenerator LoadMember(string memberName)
		{
			if (string.IsNullOrEmpty(memberName))
			{
				throw new ArgumentNullException("memberName");
			}

			var item = _evalStack.Peek();

			// todo - fix to work with generics
			//if (item.Type.IsValueType)
			//{
			//   if (item.ItemType != EvalStackItemType.Address)
			//   {
			//      throw new InvalidOperationException("The last item on the stack must be a " + EvalStackItemType.Address);
			//   }
			//}
			//else
			//{
			//   if (item.ItemType != EvalStackItemType.Reference)
			//   {
			//      throw new InvalidOperationException("The last item on the stack must be a " + EvalStackItemType.Reference);
			//   }
			//}

			//if (item.Type == null)
			//{
			//   throw new InvalidOperationException("The last item on the stack is null or doesn't have a type.");
			//}

			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

			var field = item.Type.GetField(memberName, flags);
			if (field != null)
			{
				return LoadField(field);
			}

			var property = item.Type.GetProperty(memberName, flags);
			if (property != null)
			{
				return LoadProperty(property);
			}

			throw new InvalidOperationException(string.Format("{0} does not define a field or property named {1}", item.Type.Name, memberName));
		}

		public MethodGenerator LoadField(FieldInfo field)
		{
			return LoadField(field, LoadOptions.Default);
		}

		public MethodGenerator LoadField(FieldInfo field, LoadOptions mode)
		{
			if (field == null) throw new ArgumentNullException("field");

			OpCode opCode;

			if (ShouldLoadAddress(field.FieldType, mode))
			{
				opCode = field.IsStatic ? OpCodes.Ldsflda : OpCodes.Ldflda;
			}
			else
			{
				opCode = field.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld;
			}

			if (!field.IsStatic)
			{
				StackAssert.IsAssignable(_evalStack.Pop(), new StackItem(field.DeclaringType, LoadOptions.ValueAsAddress), false);
			}
			_writer.Emit(opCode, field);
			_evalStack.Push(field.FieldType, mode);
			return this;
		}

		public MethodGenerator LoadProperty(PropertyInfo property)
		{
			return LoadProperty(property, LoadOptions.Default);
		}

		public MethodGenerator LoadProperty(PropertyInfo property, LoadOptions options)
		{
			if (property == null) throw new ArgumentNullException("property");

			if (options.ShouldLoadAddress(property.PropertyType))
			{
				using (var tempLocal = BorrowLocal(property.PropertyType, false, null))
				{
					BeginAssign(tempLocal);
					Call(property.GetGetMethod(true));
					EndAssign();
					Load(tempLocal, LoadOptions.AnyAsAddress);
				}
			}
			else
			{
				Call(property.GetGetMethod(true));
			}

			return this;
		}

		public MethodGenerator Store(IVariable variable)
		{
			ArgumentAssert.IsNotNull(variable, "variable");

			variable.Store();
			return this;
		}

		public void BeginScope()
		{
			PushScope(new Scope(this));
		}

		public void EndScope()
		{
			PopScope(typeof(Scope), "BeginScope/EndScope mismatch");
		}

		public IVariable BeginUsing()
		{
			var scope = new UsingScope(this);
			PushScope(scope);
			return scope.UsingTarget;
		}

		public void EndUsing()
		{
			PopScope(typeof(UsingScope), "BeginUsing/EndUsing mismatch");
		}

		public IVariable BeginForEach(Type elementType)
		{
			var scope = new ForEachScope(this, elementType);
			PushScope(scope);
			return scope.Current;
		}

		public void EndForEach()
		{
			PopScope(typeof(ForEachScope), "BeginForEach/EndForEach mismatch");
		}

		public MethodGenerator InitializeValue()
		{
			var item = _evalStack.Pop();
			if (item.ItemType != ItemType.AddressToValue)
			{
				throw new StackValidationException("Expected an address to a value type.");
			}
			_writer.Emit(OpCodes.Initobj, item.Type);
			return this;
		}

		public MethodGenerator Increment()
		{
			var item = _evalStack.Pop();
			StackAssert.IsAssignable(item, typeof(int), false);
			_writer.EmitLdcI4(1);
			_writer.Emit(OpCodes.Add);
			_evalStack.Push(typeof(int));
			return this;
		}

		public MethodGenerator Decrement()
		{
			var item = _evalStack.Pop();
			StackAssert.IsAssignable(item, typeof(int), false);
			_writer.EmitLdcI4(1);
			_writer.Emit(OpCodes.Sub);
			_evalStack.Push(typeof(int));
			return this;
		}

		public void BeginAssign(IVariable variable)
		{
			ArgumentAssert.IsNotNull(variable, "variable");
			variable.BeginAssign();
			_delayedInstructions.Push(variable.EndAssign);
		}

		public void BeginAssign(string memberName)
		{
			ArgumentAssert.IsNotNullOrEmpty(memberName, "memberName");

			var target = _evalStack.Peek();
			if (target.ItemType != ItemType.Reference)
			{
				throw new InvalidOperationException("A reference must be loaded onto the evaluation stack before this operation can be performed.");
			}
			if (target.Type == null)
			{
				throw new InvalidOperationException("A typed item must be on the evaluation stack to perform this operation.");
			}

			BeginAssign(GetFieldOrProperty(target.Type, memberName));
		}

		public void BeginAssign(MemberInfo fieldOrProperty)
		{
			if (fieldOrProperty == null) throw new ArgumentNullException("fieldOrProperty");

			if (fieldOrProperty is FieldInfo)
			{
				BeginAssign((FieldInfo)fieldOrProperty);
				return;
			}

			if (fieldOrProperty is PropertyInfo)
			{
				BeginAssign((PropertyInfo)fieldOrProperty);
				return;
			}

			throw new ArgumentException("fieldOrProperty is not an IFieldReference or an IPropertyReference", "fieldOrProperty");
		}

		/// <summary>
		/// Begins an assignment.
		/// Expected stack parameters -
		/// 1. If <paramref name="field"/> is an instance field - Reference to object instance that has <paramref name="field"/>.
		/// 2. The value to assign to <paramref name="field"/>.
		/// </summary>
		/// <param name="field">The field to assign.</param>
		public void BeginAssign(FieldInfo field)
		{
			if (field == null) throw new ArgumentNullException("field");

			StackItem target = default(StackItem);
			if (!field.IsStatic)
			{
				target = _evalStack.Peek();
				StackAssert.IsAssignable(target, new StackItem(field.DeclaringType, LoadOptions.ValueAsAddress), false);
			}

			_delayedInstructions.Push(() =>
			{
				_evalStack.Pop();
				// todo validate

				if (field.IsStatic)
				{
					_writer.Emit(OpCodes.Stsfld, field);
				}
				else
				{
					var item = _evalStack.Pop();
					if (target != item)
					{
						throw new InvalidOperationException("BeginAssign/EndAssign mismatch.");
					}
					_writer.Emit(OpCodes.Stfld, field);
				}
			});
		}

		/// <summary>
		/// Begins an assignment.
		/// Expected stack parameters -
		/// 1. If <paramref name="property"/> is an instance property - Reference to object instance that has <paramref name="property"/>.
		/// 2. The value to assign to <paramref name="property"/>.
		/// </summary>
		/// <param name="property">The property to assign.</param>
		public void BeginAssign(PropertyInfo property)
		{
			if (property == null) throw new ArgumentNullException("property");

			var method = property.GetSetMethod(true);

			if (method == null)
			{
				throw new InvalidOperationException(string.Format("Property {0}.{1} is read-only.", property.DeclaringType, property.Name));
			}

			BeginCall(method);
			_delayedInstructions.Push(EndCall);
		}

		/// <summary>
		/// Ends the assignment.
		/// </summary>
		public void EndAssign()
		{
			_delayedInstructions.Pop()();
		}

		/// <summary>
		/// Begins a method call.
		/// Expected stack parameters -
		/// 1. If the target method is an instance method -
		///	      Reference to object instance that defines the target method
		///	      or a pointer to a value type that defines the method
		///	2. The parameters of the method.
		/// </summary>
		/// <param name="declaringType">Type of the declaring.</param>
		/// <param name="name">The name.</param>
		/// <param name="parameterTypes">The parameter types.</param>
		/// <exception cref="ArgumentNullException">
		///		<para><paramref name="declaringType"/> is <see langword="null"/>.</para>
		///		<para>- or -</para>
		///		<para><paramref name="name"/> is <see langword="null"/>.</para>
		/// </exception>
		public void BeginCall(Type declaringType, string name, params Type[] parameterTypes)
		{
			if (declaringType == null) throw new ArgumentNullException("declaringType");
			if (string.IsNullOrEmpty("name")) throw new ArgumentNullException("name");

			if (parameterTypes == null) parameterTypes = Type.EmptyTypes;

			var method = declaringType.GetMethod(name, parameterTypes);
			if (method == null)
			{
				throw new InvalidOperationException(string.Format(
					"{0}.{1}({2}) does not exist.",
					declaringType.FullName,
					name,
					string.Join(", ", parameterTypes.Select<Type, string>(t => t.Name).ToArray<string>())));
			}
			BeginCall(method);
		}

		/// <summary>
		/// Begins a method call to <param name="method" />.
		/// Expected stack parameters -
		/// 1. If the target method is an instance method -
		///	      Reference to object instance that defines the target method
		///	      or a pointer to a value type that defines the method
		///	2. The parameters of the method.
		/// </summary>
		/// <param name="method">The method to call.</param>
		/// <exception cref="ArgumentNullException">
		///		<para><paramref name="method"/> is <see langword="null"/>.</para>
		/// </exception>
		public void BeginCall(MethodInfo method)
		{
			if (method == null) throw new ArgumentNullException("method");

			_delayedInstructions.Push(() => Call(method));
		}

		/// <summary>
		/// Ends a method call.
		/// </summary>
		public void EndCall()
		{
			_delayedInstructions.Pop()();
		}

		/// <summary>
		/// Emits the instructions required to call <param name="method" />.
		/// Expected stack parameters -
		/// 1. If the target method is an instance method -
		///	      Reference to object instance that defines the target method
		///	      or a pointer to a value type that defines the method
		///	2. The parameters of the method.
		/// </summary>
		/// <param name="method">The method to call.</param>
		/// <exception cref="ArgumentNullException">
		///		<para><paramref name="method"/> is <see langword="null"/>.</para>
		/// </exception>
		public MethodGenerator Call(MethodInfo method)
		{
			if (method == null) throw new ArgumentNullException("method");

			StackItem lastItem = default(StackItem);
			bool arrayCheck = !method.IsStatic && method.Name == "get_Length" && _evalStack.Count > 0;
			if (arrayCheck)
			{
				lastItem = _evalStack.Peek();
			}

			PopAndValidateParams(method);

			if (arrayCheck
				&& lastItem.Type.IsArray
				&& lastItem.Type.GetArrayRank() == 1)
			{
				// special case - array lengths should be loaded via ldlen.
				_writer.Emit(OpCodes.Ldlen);
				_writer.Emit(OpCodes.Conv_I4);
			}
			else
			{
				_writer.EmitCall(method);
			}
			if (method.ReturnParameter != null && typeof(void) != method.ReturnType)
			{
				_evalStack.Push(method.ReturnType);
			}
			return this;
		}

		private void PopAndValidateParams(MethodBase method)
		{
			var parameters = method.GetParameters();
			for (int i = parameters.Length - 1; i >= 0; --i)
			{
				// todo - account for ref & out parameters
				StackAssert.IsAssignable(_evalStack.Pop(), new StackItem(parameters[i].ParameterType, LoadOptions.Default), true);
			}
			if (!method.IsStatic && !method.IsConstructor)
			{
				// todo - validate must be IsAssignableFrom and not ==
				StackAssert.IsAssignable(_evalStack.Pop(), new StackItem(method.DeclaringType, LoadOptions.ValueAsAddress), false);
			}
		}

		/// <summary>
		/// Emits instructions to un-box to the specified <paramref name="type"/>.
		/// </summary>
		/// <param name="type">The type we're un-boxing to.</param>
		/// <exception cref="ArgumentNullException">
		///		<para><paramref name="type"/> is <see langword="null"/>.</para>
		/// </exception>
		public MethodGenerator UnboxAny(Type type)
		{
			if (type == null) throw new ArgumentNullException("type");
			var item = _evalStack.Pop();
			StackAssert.IsAssignable(item, new StackItem(typeof(object), LoadOptions.BoxValues), false);
			_writer.Emit(OpCodes.Unbox_Any, type);
			_evalStack.Push(type);
			return this;
		}

		/// <summary>
		/// Emits instructions to load a <see cref="Type"/> reference onto the evaluation stack.
		/// </summary>
		/// <param name="type">The type instance to load.</param>
		/// <exception cref="ArgumentNullException">
		///		<para><paramref name="type"/> is <see langword="null"/>.</para>
		/// </exception>
		public MethodGenerator Load(Type type)
		{
			if (type == null) throw new ArgumentNullException("type");
			var method = typeof(Type).ResolveMethod("GetTypeFromHandle", typeof(RuntimeTypeHandle));
			if (method == null)
			{
				throw new MissingMethodException(typeof(Type).FullName, "GetTypeFromHandle");
			}
			LoadToken(type);
			Call(method);
			return this;
		}

		/// <summary>
		/// Emits instructions to load a <see cref="RuntimeTypeHandle"/> for <paramref name="type"/> onto the evaluation stack.
		/// </summary>
		/// <param name="type">The type to load.</param>
		/// <exception cref="ArgumentNullException">
		///		<para><paramref name="type"/> is <see langword="null"/>.</para>
		/// </exception>
		public MethodGenerator LoadToken(Type type)
		{
			if (type == null) throw new ArgumentNullException("type");
			_writer.Emit(OpCodes.Ldtoken, type);
			_evalStack.Push(typeof(RuntimeTypeHandle));
			return this;
		}

		/// <summary>
		/// Emits instructions to load a <see cref="RuntimeMethodHandle"/> for <paramref name="method"/> onto the evaluation stack.
		/// </summary>
		/// <param name="method">The method to load.</param>
		/// <exception cref="ArgumentNullException">
		///		<para><paramref name="method"/> is <see langword="null"/>.</para>
		/// </exception>
		public MethodGenerator LoadToken(MethodInfo method)
		{
			if (method == null) throw new ArgumentNullException("method");
			_writer.Emit(OpCodes.Ldtoken, method);
			_evalStack.Push(typeof(RuntimeMethodHandle));
			return this;
		}

		/// <summary>
		/// Emits instructions to load a <see cref="RuntimeFieldHandle"/> for <paramref name="field"/> onto the evaluation stack.
		/// </summary>
		/// <param name="field">The method to load.</param>
		/// <exception cref="ArgumentNullException">
		///		<para><paramref name="field"/> is <see langword="null"/>.</para>
		/// </exception>
		public MethodGenerator LoadToken(FieldInfo field)
		{
			if (field == null) throw new ArgumentNullException("field");
			_writer.Emit(OpCodes.Ldtoken, field);
			_evalStack.Push(typeof(RuntimeFieldHandle));
			return this;
		}

		/// <summary>
		/// Emits instructions to load a <see cref="RuntimeFieldHandle"/> or <see cref="RuntimeMethodHandle"/> for <paramref name="member"/> onto the evaluation stack.
		/// </summary>
		/// <param name="member">The method or field to load.</param>
		/// <exception cref="ArgumentNullException">
		///		<para><paramref name="member"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentException">
		///		<para><paramref name="member"/> is not a field or method.</para>
		/// </exception>
		public MethodGenerator LoadToken(MemberInfo member)
		{
			if (member == null) throw new ArgumentNullException("member");
			if (member is MethodInfo)
			{
				LoadToken((MethodInfo)member);
			}
			else if (member is FieldInfo)
			{
				LoadToken((FieldInfo)member);
			}
			else
			{
				throw new ArgumentException("member must be a field or method", "member");
			}
			return this;
		}

		/// <summary>
		/// Begins a try block.
		/// </summary>
		public void Try()
		{
			PushScope(new Scope(this));
			_writer.BeginTry();
		}

		/// <summary>
		/// Begins a catch block. Must have previously called <see cref="Try"/>.
		/// </summary>
		public void Catch()
		{
			PopScope(typeof(Scope), "Catch without try");
			PushScope(new Scope(this));
			_writer.BeginCatch(typeof(object));
			_writer.Emit(OpCodes.Pop);
		}

		/// <summary>
		/// Begins a catch block that filters for a particular exception type.
		/// Must have previously called <see cref="Try"/>.
		/// Returns a variable that can be used to access the caught exception.
		/// </summary>
		public IVariable Catch(Type exceptionType)
		{
			PopScope(typeof(Scope), "Catch without try");
			PushScope(new Scope(this));
			var ex = DefineLocal(exceptionType, false, "ex");
			BeginAssign(ex);
			_writer.BeginCatch(exceptionType);
			_evalStack.Push(exceptionType);
			EndAssign();
			return ex;
		}

		/// <summary>
		/// Begins a finally block.
		/// </summary>
		public void Finally()
		{
			PopScope(typeof(Scope), "Finally without try");
			PushScope(new Scope(this));
			_writer.BeginFinally();
		}

		/// <summary>
		/// Ends any try/catch/finally block.
		/// </summary>
		public void EndTryCatchFinally()
		{
			PopScope(typeof(Scope), "End of try/catch/finally within an invalid scope");
			_writer.EndTryCatchFinally();
		}

		private static bool ShouldLoadAddress(Type typeToLoad, LoadOptions options)
		{
			if (typeToLoad.IsValueType)
			{
				return (options & LoadOptions.ValueAsAddress) == LoadOptions.ValueAsAddress;
			}
			return (options & LoadOptions.ReferenceAsAddress) == LoadOptions.ReferenceAsAddress;
		}

		/// <summary>
		/// Emits instructions to return from the method.
		/// </summary>
		public void Return()
		{
			if (_header.ReturnType != typeof(void))
			{
				StackAssert.IsAssignable(_evalStack.Pop(), new StackItem(_header.ReturnType, LoadOptions.Default), true);
			}

			if (_evalStack.Count > 0)
			{
				throw new InvalidOperationException("All items on the evaluation stack must be removed before a return instruction can be emitted.");
			}

			_writer.Emit(OpCodes.Ret);
		}

		private LocalDefinition DefineLocal(Type type, bool isPinned, string name)
		{
			var result = new LocalDefinition(this, _writer.DefineLocal(type, isPinned, name));
			_locals.Add(result);
			return result;
		}

		private BorrowedLocal BorrowLocal(Type type, bool isPinned, string name)
		{
			foreach (var localDef in _locals)
			{
				if (!localDef.InScope
					&& localDef.Type == type
					&& localDef.IsPinned == isPinned)
				{
					return new BorrowedLocal(localDef);
				}
			}

			return new BorrowedLocal(DefineLocal(type, isPinned, name));
		}

		private static MemberInfo GetFieldOrProperty(Type type, string memberName)
		{
			const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
			MemberInfo result = type.GetField(memberName, flags);
			if (result != null) return result;
			result = type.GetProperty(memberName, flags);
			if (result != null) return result;

			throw new InvalidOperationException(string.Format("No field or property {0}.{1} exists.", type.FullName, memberName));
		}

		private void PushScope(Scope newScope)
		{
			_scopeStack.Push(_currentScope);
			_currentScope = newScope;
			newScope.Open();
		}

		private void PopScope(Type expectedScopeType, string message)
		{
			if (_scopeStack.Count == 0)
			{
				throw new InvalidOperationException("Popped scope too many times");
			}

			if (expectedScopeType != null && _currentScope.GetType() != expectedScopeType)
			{
				throw new InvalidOperationException(message ?? string.Format(
					"Expected the current scope to be of type '{0}' but found a scope of type '{1}'", expectedScopeType.Name, _currentScope.GetType().Name));
			}

			_currentScope.Dispose();
			_currentScope = _scopeStack.Pop();
		}

		private void GoToIf(BinaryOperator op, ILabel target)
		{
			for (int i = 0; i < op.ArgumentCount; ++i)
			{
				_evalStack.Pop();
				// todo validate item
			}
			op.WriteBranch(_writer, target);
		}

		[DebuggerDisplay("VariableCount={_ownedLocals.Count}, EvalStackDepth={_evalStack.Count}")]
		private class Scope : IDisposable
		{
			private readonly MethodGenerator _owner;
			private readonly List<BorrowedLocal> _ownedLocals = new List<BorrowedLocal>();

			private int _startEvalStackDepth;
			private int _evalStackOverflow;
			private bool _closed;

			public Scope(MethodGenerator owner)
			{
				_owner = owner;
			}

			public Scope(MethodGenerator owner, int evalStackOverflow)
				: this(owner)
			{
				_evalStackOverflow = evalStackOverflow;
			}

			protected MethodGenerator Owner
			{
				[DebuggerStepThrough]
				get { return _owner; }
			}

			public IVariable DeclareLocal(Type type, bool isPinned)
			{
				var result = _owner.BorrowLocal(type, isPinned, null);
				_ownedLocals.Add(result);
				return result;
			}

			public virtual void Open()
			{
				_startEvalStackDepth = Owner._evalStack.Count;
			}

			protected virtual void Close()
			{
				if (_closed) return;

				try
				{
					foreach (var local in _ownedLocals)
					{
						local.Dispose();
					}

					if (Owner._evalStack.Count != (_startEvalStackDepth + _evalStackOverflow))
					{
						throw new InvalidOperationException(string.Format("Begin/End scope validation failure - Began the scope with {0} items on the evaluation stack but ended with {1} items when {2} items were expected", _startEvalStackDepth, Owner._evalStack.Count, _startEvalStackDepth + _evalStackOverflow));
					}
				}
				finally
				{
					_closed = true;
				}
			}

			public void Dispose()
			{
				Close();
			}
		}

		[DebuggerDisplay("Type={Type.Name}, IsPinned={IsPinned}")]
		private sealed class BorrowedLocal : IVariable, IDisposable
		{
			private readonly LocalDefinition _owner;
			private bool _outOfScope;

			public BorrowedLocal(LocalDefinition owner)
			{
				_owner = owner;
				_owner.InScope = true;
			}

			public Type Type
			{
				[DebuggerStepThrough]
				get { return _owner.Type; }
			}

			public bool IsPinned
			{
				[DebuggerStepThrough]
				get { return _owner.IsPinned; }
			}

			public string Name
			{
				[DebuggerStepThrough]
				get { return _owner.Name; }
			}

			[DebuggerStepThrough]
			public void Initialize()
			{
				_owner.Initialize();
			}

			public void Load(LoadOptions options)
			{
				ValidateScope();
				_owner.Load(options);
			}

			public void Store()
			{
				ValidateScope();
				_owner.Store();
			}

			public bool CanStore
			{
				get
				{
					ValidateScope();
					return _owner.CanStore;
				}
			}

			public void BeginAssign()
			{
				ValidateScope();
				_owner.BeginAssign();
			}

			public void EndAssign()
			{
				ValidateScope();
				_owner.EndAssign();
			}

			private void ValidateScope()
			{
				if (_outOfScope) throw new InvalidOperationException("This local is out of scope.");
			}

			public void Dispose()
			{
				_outOfScope = true;
				_owner.InScope = false;
			}
		}

		private class LabeledScope : Scope
		{
			public LabeledScope(MethodGenerator owner)
				: base(owner)
			{
				EndLabel = owner._writer.DefineLabel();
			}

			public ILabel EndLabel { get; protected set; }

			protected override void Close()
			{
				EndLabel.Mark();
				base.Close();
			}
		}

		private class UsingScope : Scope
		{
			private MethodInfo _targetDisposeMethod;

			public UsingScope(MethodGenerator owner)
				: base(owner)
			{
			}

			public override void Open()
			{
				var item = Owner._evalStack.Peek();
				StackAssert.AreEqual(item, new StackItem(item.Type, LoadOptions.Default));

				_targetDisposeMethod = UsingTarget.Type.GetBestCallableOverride(_disposeMethod);

				UsingTarget = Owner.DeclareLocal(item.Type);
				Owner.Store(UsingTarget);

				base.Open();

				Owner._writer.BeginTry();
			}

			public IVariable UsingTarget { get; private set; }

			protected override void Close()
			{
				Owner._writer.BeginFinally();
				{
					Owner.Load(UsingTarget);
					Owner.BeginCall(_targetDisposeMethod);
					Owner.EndCall();
				}
				Owner._writer.EndTryCatchFinally();
				base.Close();
			}
		}

		private class ForEachScope : Scope
		{
			private static readonly MethodInfo _dispose = typeof(IDisposable).GetMethod("Dispose");
			private static readonly MethodInfo _moveNext = typeof(IEnumerator).GetMethod("MoveNext");

			private ILabel _iterateLabel;
			private ILabel _loopBlockLabel;
			private IVariable _current;
			private IVariable _enumerator;
			private MethodInfo _getEnumerator;
			private MethodInfo _getCurrent;

			public ForEachScope(MethodGenerator owner, Type elementType)
				: base(owner)
			{
				ElementType = elementType;
			}

			public IVariable Current
			{
				get
				{
					if (_current == null)
					{
						throw new InvalidOperationException("Can't access the current variable outside the scope of the foreach loop.");
					}
					return _current;
				}
			}

			public Type ElementType { get; private set; }

			private void Initialize(StackItem enumerableItem)
			{
				Type enumerableType = typeof(IEnumerable<>).MakeGenericType(ElementType);
				Type enumeratorType;

				if (enumerableType.IsAssignableFrom(enumerableItem.Type))
				{
					enumeratorType = typeof(IEnumerator<>).MakeGenericType(ElementType);
				}
				else
				{
					enumerableType = typeof(IEnumerable);
					enumeratorType = typeof(IEnumerator);
				}

				_getEnumerator = enumerableItem.Type.GetBestCallableOverride(enumerableType.GetMethod("GetEnumerator"));
				_getCurrent = enumeratorType.GetProperty("Current").GetGetMethod();
				_current = DeclareLocal(ElementType, false);
				_enumerator = DeclareLocal(enumeratorType, false);
			}

			public override void Open()
			{
				var item = Owner._evalStack.Peek();
				StackAssert.IsAssignable(item, new StackItem(typeof(IEnumerable), LoadOptions.ValueAsAddress), false);

				Initialize(item);

				Owner.BeginAssign(_enumerator);
				Owner.Call(_getEnumerator);
				Owner.EndAssign();

				base.Open();

				Owner._writer.BeginTry();

				_iterateLabel = Owner._writer.DefineLabel();
				Owner._writer.Emit(OpCodes.Br, _iterateLabel);
				_loopBlockLabel = Owner._writer.DefineLabel();
				_loopBlockLabel.Mark();

				Owner.BeginAssign(_current);
				{
					Owner.Load(_enumerator);
					Owner.BeginCall(_getCurrent);
					Owner.EndCall();

					if (ElementType.IsValueType && _getCurrent.ReturnType == typeof(object))
					{
						Owner.UnboxAny(ElementType);
					}
				}
				Owner.EndAssign();
			}

			protected override void Close()
			{
				_iterateLabel.Mark();

				Owner.Load(_enumerator);
				Owner.BeginCall(_moveNext);
				Owner.EndCall();
				Owner.GoToIf(BinaryOperator.IsTrue, _loopBlockLabel);

				Owner._writer.BeginFinally();
				Owner.Load(_enumerator);
				Owner.BeginCall(_dispose);
				Owner.EndCall();
				Owner._writer.EndTryCatchFinally();

				base.Close();
				_current = null;
			}
		}

		[DebuggerDisplay("InScope={InScope}, Type={Type.Name}, IsPinned={IsPinned}")]
		private class LocalDefinition : IVariable
		{
			private readonly MethodGenerator _owner;
			private readonly IVariable _innerVariable;

			public LocalDefinition(MethodGenerator owner, IVariable innerVariable)
			{
				_owner = owner;
				_innerVariable = innerVariable;
				InScope = true;
			}

			public bool InScope { get; set; }

			public Type Type
			{
				[DebuggerStepThrough]
				get { return _innerVariable.Type; }
			}

			public bool IsPinned
			{
				[DebuggerStepThrough]
				get { return _innerVariable.IsPinned; }
			}

			public string Name
			{
				[DebuggerStepThrough]
				get { return _innerVariable.Name; }
			}

			public void Initialize()
			{
				_innerVariable.Initialize();
			}

			public void Load(LoadOptions options)
			{
				_innerVariable.Load(options);
				_owner._evalStack.Push(Type, options);
			}

			public void Store()
			{
				StackAssert.IsAssignable(
					_owner._evalStack.Pop(),
					new StackItem(Type, LoadOptions.Default),
					true);
				_innerVariable.Store();
			}

			public bool CanStore
			{
				get { return _innerVariable.CanStore; }
			}

			public void BeginAssign()
			{
				_innerVariable.BeginAssign();
			}

			public void EndAssign()
			{
				StackAssert.IsAssignable(
					_owner._evalStack.Pop(),
					new StackItem(Type, LoadOptions.Default),
					true);
				_innerVariable.EndAssign();
			}
		}

		[DebuggerDisplay("Type={Type.Name}, IsPinned={IsPinned}")]
		private sealed class Parameter : IVariable
		{
			private readonly MethodGenerator _owner;
			private readonly IVariable _innerParameter;

			public Parameter(MethodGenerator owner, IVariable innerParameter)
			{
				_owner = owner;
				_innerParameter = innerParameter;
			}

			public Type Type
			{
				[DebuggerStepThrough]
				get { return _innerParameter.Type; }
			}

			public bool IsPinned
			{
				[DebuggerStepThrough]
				get { return _innerParameter.IsPinned; }
			}

			public string Name
			{
				[DebuggerStepThrough]
				get { return _innerParameter.Name; }
			}

			[DebuggerStepThrough]
			public void Initialize()
			{
				_innerParameter.Initialize();
			}

			public void Load(LoadOptions options)
			{
				_innerParameter.Load(options);
				_owner._evalStack.Push(Type, options);
			}

			public void Store()
			{
				StackAssert.IsAssignable(
					_owner._evalStack.Pop(),
					new StackItem(Type, LoadOptions.Default),
					true);
				_innerParameter.Store();
			}

			public bool CanStore
			{
				get { return _innerParameter.CanStore; }
			}

			public void BeginAssign()
			{
				_innerParameter.BeginAssign();
			}

			public void EndAssign()
			{
				StackAssert.IsAssignable(
					_owner._evalStack.Pop(),
					new StackItem(Type, LoadOptions.Default),
					true);
				_innerParameter.EndAssign();
			}
		}

		[DebuggerDisplay("Expression={Name}, Type={Type.Name}")]
		private class Expression : IExpression
		{
			private readonly MethodGenerator _owner;
			private readonly IVariable _variable;
			private readonly List<MemberInfo> _members;
			private readonly Stack<Action> _delayedInstructions = new Stack<Action>();

			public Expression(MethodGenerator owner, IVariable variable)
			{
				_owner = owner;
				_variable = variable;
				_members = new List<MemberInfo>();
			}

			public void Load(LoadOptions loadOptions)
			{
				if (_members.Count == 0)
				{
					_variable.Load(loadOptions);
				}
				else
				{
					_variable.Load(LoadOptions.ValueAsAddress);
					for (int i = 0; i < _members.Count - 1; ++i)
					{
						LoadMember(_members[i], LoadOptions.ValueAsAddress);
					}
					if (_members.Count > 0)
					{
						LoadMember(_members[_members.Count - 1], loadOptions);
					}
				}
			}

			public void BeginAssign()
			{
				if (IsReadOnly)
				{
					throw new InvalidOperationException("This value is read-only.");
				}

				if (_members.Count == 0)
				{
					_variable.BeginAssign();
					_delayedInstructions.Push(_variable.EndAssign);
				}
				else
				{
					_variable.Load(LoadOptions.ValueAsAddress);

					for (int i = 0; i < _members.Count - 1; ++i)
					{
						LoadMember(_members[i], LoadOptions.ValueAsAddress);
					}
					//var count = _owner._delayedInstructions.Count;
					_owner.BeginAssign(_members[_members.Count - 1]);
					//Debug.Assert(count < _owner._delayedInstructions.Count, "MethodGenerator.BeginAssign did not push any instructions onto _delayInstructions as expected");
					//var endInstruction = _owner._delayedInstructions.Pop();
					_delayedInstructions.Push(_owner.EndAssign);
				}
			}

			public void EndAssign()
			{
				_delayedInstructions.Pop()();
			}

			public IExpression Copy()
			{
				var result = new Expression(_owner, _variable);
				result._members.AddRange(_members);
				return result;
			}

			public IExpression AddMember(string memberName)
			{
				if (memberName == null) throw new ArgumentNullException("memberName");
				ValidateIsModifiable();

				var field = Type.GetField(memberName);
				if (field != null)
				{
					return AddMember(field);
				}

				var property = Type.GetProperty(memberName);
				if (property != null)
				{
					return AddMember(property);
				}

				throw new MissingMemberException(string.Format("Type '{0}' does not define a field or property named '{1}'", Type.Name, memberName));
			}

			public Type Type
			{
				get
				{
					if (_members.Count == 0) return _variable.Type;

					var lastMember = _members[_members.Count - 1];
					if (lastMember is FieldInfo)
					{
						return ((FieldInfo)lastMember).FieldType;
					}
					return ((PropertyInfo)lastMember).PropertyType;
				}
			}

			public IExpression AddMember(FieldInfo field)
			{
				if (field == null) throw new ArgumentNullException("field");
				ValidateIsModifiable();

				_members.Add(field);
				return this;
			}

			public IExpression AddMember(PropertyInfo property)
			{
				if (property == null) throw new ArgumentNullException("property");
				ValidateIsModifiable();

				_members.Add(property);
				return this;
			}

			private void ValidateIsModifiable()
			{
				if (_delayedInstructions.Count > 0)
				{
					throw new InvalidOperationException("This shortcut cannot be modified between BeginAssign/EndAssign calls.");
				}
			}

			private void LoadMember(MemberInfo member, LoadOptions options)
			{
				var field = member as FieldInfo;
				if (field != null)
				{
					_owner.LoadField(field, options);
				}
				else
				{
					var property = (PropertyInfo)member;
					_owner.LoadProperty(property, options);
				}
			}

			public bool IsReadOnly { get; private set; }

			public IExpression MakeReadOnly()
			{
				IsReadOnly = true;
				return this;
			}

			public bool IsPinned
			{
				get { return _members.Count == 0 && _variable.IsPinned; }
			}

			public string Name
			{
				get
				{
					StringBuilder builder = new StringBuilder();
					builder.Append(
						string.IsNullOrEmpty(_variable.Name)
						? "(UnknownVariable)"
						: _variable.Name);

					foreach (var member in _members)
					{
						builder.Append(".");
						builder.Append(member.Name);
					}

					return builder.ToString();
				}
			}

			public void Initialize()
			{
				if (_members.Count == 0)
				{
					_variable.Initialize();
				}
				else
				{
					BeginAssign();
					_owner.LoadDefaultOf(Type);
					EndAssign();
				}
			}

			public void Store()
			{
				if (!CanStore)
				{
					throw new InvalidOperationException("Can't store when Type is a ByRef type.");
				}
				_variable.Store();
			}

			public bool CanStore
			{
				get { return _members.Count == 0 && _variable.CanStore; }
			}
		}
	}
}
