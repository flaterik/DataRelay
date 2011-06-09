using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using MySpace.Common.Dynamic;

namespace MySpace.Common.Barf.Parts
{
	internal class DeferredPartBuilder : IPartBuilder
	{
		private static readonly MethodInfo _genericWriteMethod;
		private static readonly MethodInfo _genericReadMethod;
		private static readonly MethodInfo _genericReadMethodByRef;
		private static readonly MethodInfo _genericFillMethod;
		private static readonly MethodInfo _genericFillMethodByRef;
		private static readonly MethodInfo _genericAssertMethod;

		static DeferredPartBuilder()
		{
#pragma warning disable 618,612
			var methods = typeof(PartFormatter).GetMethods(BindingFlags.Public | BindingFlags.Static);
#pragma warning restore 618,612

			_genericWriteMethod = methods
				.Where<MethodInfo>(m => m.Name == "WritePart")
				.Where<MethodInfo>(m => m.IsGenericMethodDefinition)
				.Where<MethodInfo>(m => m.ReturnType == null || m.ReturnType == typeof(void))
				.Where<MethodInfo>(m => m.GetParameters().Length == 3)
				.Where<MethodInfo>(m => m.GetParameters()[0].ParameterType.IsGenericParameter)
				.Where<MethodInfo>(m => m.GetParameters()[1].ParameterType == typeof(BarfSerializationArgs))
				.Where<MethodInfo>(m => m.GetParameters()[2].ParameterType == typeof(PartFlags))
				.FirstOrDefault<MethodInfo>();

			if (_genericWriteMethod == null)
			{
#pragma warning disable 618,612
				throw new MissingMethodException(typeof(PartFormatter).FullName, "WritePart<T>(T, BarfSerializationArgs, PartFlags):void");
#pragma warning restore 618,612
			}

			_genericReadMethod = methods
				.Where<MethodInfo>(m => m.Name == "ReadPart")
				.Where<MethodInfo>(m => m.IsGenericMethodDefinition)
				.Where<MethodInfo>(m => m.ReturnType == null || m.ReturnType == typeof(void))
				.Where<MethodInfo>(m => m.GetParameters().Length == 3)
				.Where<MethodInfo>(m => !m.GetParameters()[0].ParameterType.IsByRef)
				.Where<MethodInfo>(m => m.GetParameters()[0].ParameterType.IsGenericParameter)
				.Where<MethodInfo>(m => m.GetParameters()[1].ParameterType == typeof(BarfDeserializationArgs))
				.Where<MethodInfo>(m => m.GetParameters()[2].ParameterType == typeof(PartFlags))
				.FirstOrDefault<MethodInfo>();

			if (_genericReadMethod == null)
			{
#pragma warning disable 618,612
				throw new MissingMethodException(typeof(PartFormatter).FullName, "ReadPart<T>(T, BarfDeserializationArgs, PartFlags):void");
#pragma warning restore 618,612
			}

			_genericReadMethodByRef = methods
				.Where<MethodInfo>(m => m.Name == "ReadPart")
				.Where<MethodInfo>(m => m.IsGenericMethodDefinition)
				.Where<MethodInfo>(m => m.ReturnType == null || m.ReturnType == typeof(void))
				.Where<MethodInfo>(m => m.GetParameters().Length == 3)
				.Where<MethodInfo>(m => m.GetParameters()[0].ParameterType.IsByRef)
				.Where<MethodInfo>(m => m.GetParameters()[0].ParameterType.GetElementType().IsGenericParameter)
				.Where<MethodInfo>(m => m.GetParameters()[1].ParameterType == typeof(BarfDeserializationArgs))
				.Where<MethodInfo>(m => m.GetParameters()[2].ParameterType == typeof(PartFlags))
				.FirstOrDefault<MethodInfo>();

			if (_genericReadMethodByRef == null)
			{
#pragma warning disable 618,612
				throw new MissingMethodException(typeof(PartFormatter).FullName, "ReadPart<T>(ref T, BarfDeserializationArgs, PartFlags):void");
#pragma warning restore 618,612
			}

			_genericFillMethod = methods
				.Where<MethodInfo>(m => m.Name == "FillRandom")
				.Where<MethodInfo>(m => m.IsGenericMethodDefinition)
				.Where<MethodInfo>(m => m.ReturnType == null || m.ReturnType == typeof(void))
				.Where<MethodInfo>(m => m.GetParameters().Length == 4)
				.Where<MethodInfo>(m => !m.GetParameters()[0].ParameterType.IsByRef)
				.Where<MethodInfo>(m => m.GetParameters()[0].ParameterType.IsGenericParameter)
				.Where<MethodInfo>(m => m.GetParameters()[1].ParameterType == typeof(FillArgs))
				.Where<MethodInfo>(m => m.GetParameters()[2].ParameterType == typeof(PartFlags))
				.Where<MethodInfo>(m => m.GetParameters()[3].ParameterType == typeof(string))
				.FirstOrDefault<MethodInfo>();

			if (_genericFillMethod == null)
			{
#pragma warning disable 618,612
				throw new MissingMethodException(typeof(PartFormatter).FullName, "FillRandom<T>(T, FillArgs, PartFlags, string):void");
#pragma warning restore 618,612
			}

			_genericFillMethodByRef = methods
				.Where<MethodInfo>(m => m.Name == "FillRandom")
				.Where<MethodInfo>(m => m.IsGenericMethodDefinition)
				.Where<MethodInfo>(m => m.ReturnType == null || m.ReturnType == typeof(void))
				.Where<MethodInfo>(m => m.GetParameters().Length == 4)
				.Where<MethodInfo>(m => m.GetParameters()[0].ParameterType.IsByRef)
				.Where<MethodInfo>(m => m.GetParameters()[0].ParameterType.GetElementType().IsGenericParameter)
				.Where<MethodInfo>(m => m.GetParameters()[1].ParameterType == typeof(FillArgs))
				.Where<MethodInfo>(m => m.GetParameters()[2].ParameterType == typeof(PartFlags))
				.Where<MethodInfo>(m => m.GetParameters()[3].ParameterType == typeof(string))
				.FirstOrDefault<MethodInfo>();

			if (_genericFillMethodByRef == null)
			{
#pragma warning disable 618,612
				throw new MissingMethodException(typeof(PartFormatter).FullName, "FillRandom<T>(ref T, FillArgs, PartFlags, string):void");
#pragma warning restore 618,612
			}

			_genericAssertMethod = methods
				.Where<MethodInfo>(m => m.Name == "AssertAreEqual")
				.Where<MethodInfo>(m => m.IsGenericMethodDefinition)
				.Where<MethodInfo>(m => m.ReturnType == typeof(void))
				.Where<MethodInfo>(m => m.GetParameters().Length == 5)
				.Where<MethodInfo>(m => m.GetParameters()[0].ParameterType.IsGenericParameter)
				.Where<MethodInfo>(m => m.GetParameters()[1].ParameterType.IsGenericParameter)
				.Where<MethodInfo>(m => m.GetParameters()[2].ParameterType == typeof(PartFlags))
				.Where<MethodInfo>(m => m.GetParameters()[3].ParameterType == typeof(AssertArgs))
				.Where<MethodInfo>(m => m.GetParameters()[4].ParameterType == typeof(string))
				.FirstOrDefault<MethodInfo>();

			if (_genericAssertMethod == null)
			{
#pragma warning disable 618,612
				throw new MissingMethodException(typeof(PartFormatter).FullName, "AssertAreEqual<T>(T, T, PartFlags, AssertArgs, string):void");
#pragma warning restore 618,612
			}
		}

		private readonly Type _type;
		private readonly PartFlags _flags;
		private readonly LazyInitializer<MethodInfo> _writeMethod;
		private readonly LazyInitializer<MethodInfo> _readMethod;
		private readonly LazyInitializer<MethodInfo> _readMethodByRef;
		private readonly LazyInitializer<MethodInfo> _fillMethod;
		private readonly LazyInitializer<MethodInfo> _fillMethodByRef;

		public DeferredPartBuilder(Type type, PartFlags partFlags)
		{
			_type = type;
			_flags = partFlags;

			_writeMethod = new LazyInitializer<MethodInfo>(() => _genericWriteMethod.MakeGenericMethod(type));
			_readMethod = new LazyInitializer<MethodInfo>(() => _genericReadMethod.MakeGenericMethod(type));
			_readMethodByRef = new LazyInitializer<MethodInfo>(() => _genericReadMethodByRef.MakeGenericMethod(type));
			_fillMethod = new LazyInitializer<MethodInfo>(() => _genericFillMethod.MakeGenericMethod(type));
			_fillMethodByRef = new LazyInitializer<MethodInfo>(() => _genericFillMethodByRef.MakeGenericMethod(type));
		}

		#region IPartBuilder Members

		public void GenerateSerializePart(GenSerializeContext context)
		{
			var g = context.Generator;

			g.BeginCall(_writeMethod);
			{
				g.Load(context.Member);
				g.Load(context.SerializationArgs);
				g.Load((int)_flags);
			}
			g.EndCall();
		}

		public void GenerateDeserializePart(GenDeserializeContext context)
		{
			var g = context.Generator;

			if (!context.Member.IsReadOnly)
			{
				context.Member.Initialize();
			}

			if (context.IsBaseType)
			{
				Debug.Assert(!_type.IsValueType, "Value types shouldn't be able to get here.");
				g.If(() =>
				{
					g.Load(context.Member);
					return BinaryOperator.IsNull;
				});
				{
					g.BeginNewObject(typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) }));
					{
						g.Load("Base type not initialized before attempting to deserialize.");
					}
					g.EndNewObject();
					g.Throw();
				}
				g.EndIf();
				g.BeginCall(_readMethod);
				{
					g.Load(context.Member, LoadOptions.Default);
					g.Load(context.DeserializationArgs);
					g.Load((int)_flags);
				}
				g.EndCall();
			}
			else
			{
				g.BeginScope();

				var member = context.Member.CanStore
					? context.Member
					: g.DeclareLocal(context.Member.Type);

				member.Initialize();

				g.BeginCall(_readMethodByRef);
				{
					g.Load(member, LoadOptions.AnyAsAddress);
					g.Load(context.DeserializationArgs);
					g.Load((int)_flags);
				}
				g.EndCall();

				if (context.Member != member)
				{
					g.BeginAssign(context.Member);
					{
						g.Load(member);
					}
					g.EndAssign();
				}

				g.EndScope();
			}
		}

		public void GenerateFillPart(GenFillContext context)
		{
			var g = context.Generator;

			if (!context.Member.IsReadOnly)
			{
				context.Member.Initialize();
			}

			if (context.IsBaseType)
			{
				Debug.Assert(!_type.IsValueType, "Value types shouldn't be able to get here.");
				g.If(() =>
				{
					g.Load(context.Member);
					return BinaryOperator.IsNull;
				});
				{
					g.BeginNewObject(typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) }));
					{
						g.Load("Base type not initialized before attempting to deserialize.");
					}
					g.EndNewObject();
					g.Throw();
				}
				g.EndIf();
				g.BeginCall(_fillMethod);
				{
					g.Load(context.Member, LoadOptions.Default);
					g.Load(context.FillArgs);
					g.Load((int)_flags);
					g.Load(context.Part.FullName);
				}
				g.EndCall();
			}
			else
			{
				g.BeginScope();

				var member = context.Member.CanStore
					? context.Member
					: g.DeclareLocal(context.Member.Type);

				member.Initialize();

				g.BeginCall(_fillMethodByRef);
				{
					g.Load(member, LoadOptions.AnyAsAddress);
					g.Load(context.FillArgs);
					g.Load((int)_flags);
					g.Load(context.Part.FullName);
				}
				g.EndCall();

				if (context.Member != member)
				{
					g.BeginAssign(context.Member);
					{
						g.Load(member);
					}
					g.EndAssign();
				}

				g.EndScope();
			}
		}

		public void GenerateAssertAreEqualPart(GenAssertAreEqualContext context)
		{
			var g = context.Generator;
			var method = _genericAssertMethod.MakeGenericMethod(_type);

			g.BeginCall(method);
			{
				g.Load(context.Expected);
				g.Load(context.Actual);
				g.Load((int)context.Part.Flags);
				g.Load(context.AssertArgs);
				g.Load(context.Part.FullName);
			}
			g.EndCall();
		}

		#endregion
	}
}
