using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using MySpace.Common.Dynamic;
using MySpace.Common.IO;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using TypeAttributes = Mono.Cecil.TypeAttributes;

namespace MySpace.Common.Barf
{
	/// <summary>
	/// An <see cref="IInstrumenter"/> implementation that adds <see cref="IBarfSerializer{T}"/> and
	/// <see cref="IBarfSerializer"/> implementations to the assembly for each serializable class
	/// for improved start-up performance.
	/// </summary>
	public class BarfInstrumenter : IInstrumenter
	{
		private static Assembly GetAssembly(string assemblyName)
		{
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (assembly.FullName.Equals(assemblyName, StringComparison.OrdinalIgnoreCase)) return assembly;
			}

			return Assembly.Load(assemblyName);
		}

		#region IInstrumenter Members

		[ThreadStatic]
		private static InstrumentationContext _context;

		private class InstrumentationContext
		{
			private Factory<Type, TypeReference> _types;
			private Factory<MethodBase, MethodReference> _methods;

			public InstrumentationContext(ModuleDefinition module)
			{
				Module = module;
				_types = Algorithm.LazyIndexer<Type, TypeReference>(t => Module.Import(t));
				_methods = Algorithm.LazyIndexer<MethodBase, MethodReference>(m => Module.Import(m));
			}

			public ModuleDefinition Module { get; private set; }

			public BarfTypeDefinition Definition { get; private set; }

			public BarfSerializerBuilder SerializerBuilder { get; private set; }

			public BarfTesterBuilder TesterBuilder { get; private set; }

			public Type Type
			{
				get { return Definition.Type; }
			}

			public TypeDefinition TypeDefinition { get; private set; }

			public TypeDefinition BarfSerializer { get; private set; }

			public Type BaseBarfSerializer
			{
				get { return typeof(BarfSerializer<>).ResolveGenericType(Type); }
			}

			public TypeReference BaseBarfSerializerReference
			{
				get { return Import(typeof(BarfSerializer<>).ResolveGenericType(Type)); }
			}

			public TypeReference BarfTesterInterface
			{
				get { return Import(typeof(IBarfTester<>).ResolveGenericType(Type)); }
			}

			public TypeDefinition BarfTester { get; private set; }

			public IDisposable OpenType(BarfTypeDefinition definition)
			{
				Definition = definition;
				SerializerBuilder = new BarfSerializerBuilder(definition);
				TesterBuilder = new BarfTesterBuilder(definition);
				TypeDefinition = Module.Types[Type.GetCecilFullName()];
				BarfSerializer = new TypeDefinition(
					"AutoSerializer",
					TypeDefinition.Namespace,
					TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.NestedPrivate | TypeAttributes.BeforeFieldInit,
					Import(typeof(BarfSerializer<>).ResolveGenericType(Type)));
				BarfTester = new TypeDefinition(
					"BarfTester",
					TypeDefinition.Namespace,
					TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.NestedPrivate | TypeAttributes.BeforeFieldInit,
					Import(typeof(object)));
				return new AnonymousDisposable(() =>
				{
					Definition = null;
					SerializerBuilder = null;
					TesterBuilder = null;
					TypeDefinition = null;
					BarfSerializer = null;
					BarfTester = null;
				});
			}

			public TypeReference Import(Type type)
			{
				return _types(type);
			}

			public MethodReference Import(MethodBase method)
			{
				return _methods(method);
			}

			private class AnonymousDisposable : IDisposable
			{
				private Action _dispose;

				public AnonymousDisposable(Action dispose)
				{
					_dispose = dispose;
				}

				#region IDisposable Members

				public void Dispose()
				{
					_dispose();
				}

				#endregion
			}
		}

		/// <summary>
		/// Instruments the specified assembly definition.
		/// </summary>
		/// <param name="assemblyDefinition">The assembly definition.</param>
		public void Instrument(AssemblyDefinition assemblyDefinition)
		{
			int eligibleTypeCount = 0;
			int validTypeCount = 0;
			var reflectionAssembly = GetAssembly(assemblyDefinition.Name.FullName);

			foreach (ModuleDefinition moduleDefinition in assemblyDefinition.Modules)
			{
				Module module = null;

				foreach (var mod in reflectionAssembly.GetModules(false))
				{
					if (moduleDefinition.Name == moduleDefinition.Name)
					{
						module = mod;
					}
				}

				if (module == null) throw new InvalidOperationException("Couldn't resolve module: " + moduleDefinition.Name);

				try
				{
					_context = new InstrumentationContext(moduleDefinition);

					foreach (Type type in module.GetTypes())
					{
						if (!type.IsClass) continue;

						if (!BarfFormatter.IsSerializable(type))
						{
							continue;
						}

						var attribute = Attribute
							.GetCustomAttributes(type, typeof(SerializableClassAttribute), false)
							.OfType<SerializableClassAttribute>()
							.FirstOrDefault<SerializableClassAttribute>();

						if (attribute.RuntimeOnly) continue;

						var barfTypeDef = BarfTypeDefinition.Get(type);

						if (barfTypeDef == null)
						{
							// todo - remove once everything is supported
							continue;
						}

						++eligibleTypeCount;

						if (type.IsGenericType) continue;

						Trace.WriteLine("Instrumenting - " + type.Name);

						using (_context.OpenType(barfTypeDef))
						{
							_context.BarfSerializer.CustomAttributes.Add(new CustomAttribute(_context.Import(typeof(CompilerGeneratedAttribute).GetConstructor(Type.EmptyTypes))));
							_context.TypeDefinition.NestedTypes.Add(_context.BarfSerializer);
							_context.TypeDefinition.Module.Types.Add(_context.BarfSerializer);

							GenerateDefaultCtor(_context.BarfSerializer, _context.BaseBarfSerializer);
							GenerateCreateEmptyMethod();
							GenerateSerializeMethod();
							GenerateInnerDeserializeMethod();

							_context.BarfTester.CustomAttributes.Add(new CustomAttribute(_context.Import(typeof(CompilerGeneratedAttribute).GetConstructor(Type.EmptyTypes))));
							_context.BarfTester.Interfaces.Add(_context.BarfTesterInterface);
							_context.TypeDefinition.NestedTypes.Add(_context.BarfTester);
							_context.TypeDefinition.Module.Types.Add(_context.BarfTester);

							GenerateDefaultCtor(_context.BarfTester, typeof(object));

							GenerateFillMethod();
							GenerateAssertAreEqualMethod();


							++validTypeCount;
						}
					}
				}
				finally
				{
					_context = null;
				}
			}
			Trace.TraceInformation("{0} - {1} of {2} eligible types were generated in {3}", GetType().Name, validTypeCount, eligibleTypeCount, assemblyDefinition.Name.Name);
		}

		private static void GenerateAssertAreEqualMethod()
		{
			var assertMethod = typeof(IBarfTester<>)
				.ResolveGenericType(_context.Type)
				.ResolveMethod("AssertAreEqual");
			var assertMethodReference = _context.Module.Import(assertMethod);
			var method = CreateMethodOverride(assertMethodReference, _context.Import(typeof(void)), false, false, true);
			method.Parameters.Add(new ParameterDefinition(_context.Import(_context.Type)) { Name = "expected" });
			method.Parameters.Add(new ParameterDefinition(_context.Import(_context.Type)) { Name = "actual" });
			method.Parameters.Add(new ParameterDefinition(_context.Import(typeof(AssertArgs))) { Name = "args" });
			_context.BarfTester.Methods.Add(method);
			var methodHeader = new MethodHeader
			{
				Attributes = CallingConventions.HasThis,
				DeclaringType = typeof(void), // a place holder
				ParameterTypes = new[] { _context.Type, _context.Type, typeof(AssertArgs) },
				ReturnType = typeof(void)
			};
			using (var writer = new CecilCilWriter(methodHeader, method.Body))
			{
				_context.TesterBuilder.GenerateAssertAreEqual(writer);
			}
		}

		private static void GenerateFillMethod()
		{
			var fillMethod = typeof(IBarfTester<>)
				.ResolveGenericType(_context.Type)
				.ResolveMethod("Fill");
			var fillMethodReference = _context.Module.Import(fillMethod);
			var method = CreateMethodOverride(fillMethodReference, _context.Import(typeof(void)), false, false, true);
			method.Parameters.Add(new ParameterDefinition(_context.Import(_context.Type.ResolveByRef())) { Name = "instance" });
			method.Parameters.Add(new ParameterDefinition(_context.Import(typeof(FillArgs))) { Name = "args" });
			_context.BarfTester.Methods.Add(method);
			var methodHeader = new MethodHeader
			{
				Attributes = CallingConventions.HasThis,
				DeclaringType = typeof(void), // a place holder
				ParameterTypes = new[] { _context.Type.ResolveByRef(), typeof(FillArgs) },
				ReturnType = typeof(void)
			};
			using (var writer = new CecilCilWriter(methodHeader, method.Body))
			{
				_context.TesterBuilder.GenerateFill(writer);
			}
		}

		private static void GenerateCreateEmptyMethod()
		{
			var createEmptyMethod = _context.BaseBarfSerializer.ResolveMethod("CreateEmpty", Type.EmptyTypes);
			var createEmtpyMethodReference = _context.Import(createEmptyMethod);
			var method = CreateMethodOverride(createEmtpyMethodReference, _context.Import(_context.Type), false, false, false);
			_context.BarfSerializer.Methods.Add(method);
			var methodHeader = new MethodHeader
			{
				Attributes = CallingConventions.HasThis,
				DeclaringType = _context.BaseBarfSerializer,
				ParameterTypes = Type.EmptyTypes,
				ReturnType = _context.Type
			};
			using (var writer = new CecilCilWriter(methodHeader, method.Body))
			{
				// type is not available yet so we're using void since that type is not referenced anyway.
				_context.SerializerBuilder.GenerateCreateEmptyMethod(writer);
			}
		}

		private static void GenerateInnerDeserializeMethod()
		{
			var deserializeMethod = _context.BaseBarfSerializer.ResolveMethod("InnerDeserialize", _context.Type.ResolveByRef(), typeof(BarfDeserializationArgs));
			var deserializeMethodReference = _context.Import(deserializeMethod);
			var method = CreateMethodOverride(deserializeMethodReference, _context.Import(typeof(void)), false, false, false);
			method.Parameters.Add(new ParameterDefinition(_context.Import(_context.Type.ResolveByRef())) { Name = "instance" });
			method.Parameters.Add(new ParameterDefinition(_context.Import(typeof(BarfDeserializationArgs))) { Name = "args" });
			_context.BarfSerializer.Methods.Add(method);
			var methodHeader = new MethodHeader
			{
				Attributes = CallingConventions.HasThis,
				DeclaringType = _context.BaseBarfSerializer,
				ParameterTypes = new[] { _context.Type.ResolveByRef(), typeof(BarfDeserializationArgs) },
				ReturnType = typeof(void)
			};
			using (var writer = new CecilCilWriter(methodHeader, method.Body))
			{
				// type is not available yet so we're using void since that type is not referenced anyway.
				_context.SerializerBuilder.GenerateInnerDeserializeMethod(writer);
			}
		}

		private static void GenerateSerializeMethod()
		{
			var serializeMethod = _context.BaseBarfSerializer.ResolveMethod("Serialize", _context.Type, typeof(BarfSerializationArgs));
			var serializeMethodReference = _context.Import(serializeMethod);
			var method = CreateMethodOverride(serializeMethodReference, _context.Import(typeof(void)), false, false, false);
			method.Parameters.Add(new ParameterDefinition(_context.Import(_context.Type)) { Name = "instance" });
			method.Parameters.Add(new ParameterDefinition(_context.Import(typeof(BarfSerializationArgs))) { Name = "args" });
			_context.BarfSerializer.Methods.Add(method);
			var methodHeader = new MethodHeader
			{
				Attributes = CallingConventions.HasThis,
				DeclaringType = _context.BaseBarfSerializer,
				ParameterTypes = new[] { _context.Type, typeof(BarfSerializationArgs) },
				ReturnType = typeof(void)
			};
			using (var writer = new CecilCilWriter(methodHeader, method.Body))
			{
				// type is not available yet so we're using void since that type is not referenced anyway.
				_context.SerializerBuilder.GenerateSerializeMethod(writer);
			}
		}

		private static void GenerateDefaultCtor(TypeDefinition definition, Type baseType)
		{
			var defaultCtor = new MethodDefinition(
				".ctor",
				MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
				_context.Import(typeof(void)));
			definition.Constructors.Add(defaultCtor);
			defaultCtor.Body.CilWorker.Emit(Mono.Cecil.Cil.OpCodes.Ldarg_0);
			defaultCtor.Body.CilWorker.Emit(Mono.Cecil.Cil.OpCodes.Call, _context.Import(baseType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null)));
			defaultCtor.Body.CilWorker.Emit(Mono.Cecil.Cil.OpCodes.Ret);
		}

		private static MethodDefinition CreateMethodOverride(MethodReference overrideMethod, TypeReference returnType, bool isExplicit, bool isPropertyMethod, bool isInterface)
		{
			string name = overrideMethod.Name;
			if (isExplicit)
			{
				name = overrideMethod.DeclaringType.Namespace
				+ "." + overrideMethod.DeclaringType.Name
				+ "." + name;
			}

			var methodAttributes = MethodAttributes.HideBySig
				| MethodAttributes.Virtual
				| MethodAttributes.Final;

			if (isInterface)
			{
				methodAttributes |= MethodAttributes.NewSlot;
			}

			var result = new MethodDefinition(name,methodAttributes, returnType)
			{
				HasThis = true,
				IsCompilerControlled = true
			};
			if (isPropertyMethod)
			{
				result.Attributes |= MethodAttributes.SpecialName;
			}
			result.Attributes |= isExplicit ? MethodAttributes.Private : MethodAttributes.Public;
            if(isExplicit)

			result.Overrides.Add(overrideMethod);
			return result;
		}

		#endregion
	}
}
