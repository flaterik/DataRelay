using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace MySpace.Common.Dynamic
{
	/// <summary>
	/// Encapsulates extension methods for dynamic code generation.
	/// </summary>
	internal static class DynamicExtensions
	{
		private static readonly Dictionary<Type, OpCode> _primitiveLdelemOpCodes;
		private static readonly Dictionary<Type, OpCode> _primitiveStelemOpCodes;
		private static readonly Dictionary<Type, OpCode> _primitiveLdobjOpCodes;
		private static readonly Dictionary<Type, OpCode> _primitiveStobjOpCodes;
		private static readonly Factory<Type, Type> _byRefTypes = Algorithm.LazyIndexer<Type, Type>(type => type.MakeByRefType());
		private static readonly Dictionary<Type, int> _primitiveSizes = new Dictionary<Type, int>
		{
			{ typeof(bool), 1 },
			{ typeof(char), 1 },
			{ typeof(byte), 1 },
			{ typeof(sbyte), 1 },
			{ typeof(short), 2 },
			{ typeof(ushort), 2 },
			{ typeof(int), 4 },
			{ typeof(uint), 4 },
			{ typeof(long), 8 },
			{ typeof(ulong), 8 },
			{ typeof(float), 4},
			{ typeof(double), 8},
			{ typeof(decimal), 16}
		};

		static DynamicExtensions()
		{
			_primitiveLdelemOpCodes = new Dictionary<Type, OpCode>
            {
                { typeof(sbyte), OpCodes.Ldelem_I1 },
                { typeof(short), OpCodes.Ldelem_I2 },
                { typeof(int), OpCodes.Ldelem_I4 },
                { typeof(long), OpCodes.Ldelem_I8 },
                { typeof(float), OpCodes.Ldelem_R4 },
                { typeof(double), OpCodes.Ldelem_R8 },
                { typeof(byte), OpCodes.Ldelem_U1 },
                { typeof(ushort), OpCodes.Ldelem_U2 },
                { typeof(uint), OpCodes.Ldelem_U4 }
            };
			_primitiveStelemOpCodes = new Dictionary<Type, OpCode>
            {
                { typeof(sbyte), OpCodes.Stelem_I1 },
                { typeof(short), OpCodes.Stelem_I2 },
                { typeof(int), OpCodes.Stelem_I4 },
                { typeof(long), OpCodes.Stelem_I8 },
                { typeof(float), OpCodes.Stelem_R4 },
                { typeof(double), OpCodes.Stelem_R8 }
            };
			_primitiveLdobjOpCodes = new Dictionary<Type, OpCode>
			{
                { typeof(sbyte), OpCodes.Ldind_I1 },
                { typeof(short), OpCodes.Ldind_I2 },
                { typeof(int), OpCodes.Ldind_I4 },
                { typeof(long), OpCodes.Ldind_I8 },
                { typeof(float), OpCodes.Ldind_R4 },
                { typeof(double), OpCodes.Ldind_R8 },
                { typeof(byte), OpCodes.Ldind_U1 },
                { typeof(ushort), OpCodes.Ldind_U2 },
                { typeof(uint), OpCodes.Ldind_U4 }
			};
			_primitiveStobjOpCodes = new Dictionary<Type, OpCode>
			{
                { typeof(sbyte), OpCodes.Stind_I1 },
                { typeof(short), OpCodes.Stind_I2 },
                { typeof(int), OpCodes.Stind_I4 },
                { typeof(long), OpCodes.Stind_I8 },
                { typeof(float), OpCodes.Stind_R4 },
                { typeof(double), OpCodes.Stind_R8 }
			};
		}

		public static Type ResolveByRef(this Type type)
		{
			ArgumentAssert.IsNotNull(type, "type");

			return _byRefTypes(type);
		}

		public static Type ResolveGenericType(this Type type, params Type[] typeArguments)
		{
			// todo - cache
			return type.MakeGenericType(typeArguments);
		}

		public static Type ResolveByRef(this Type type, LoadOptions options)
		{
			ArgumentAssert.IsNotNull(type, "type");

			if (type.IsValueType)
			{
				if (options.IsSet(LoadOptions.ValueAsAddress))
				{
					return _byRefTypes(type);
				}
			}
			else
			{
				if (options.IsSet(LoadOptions.ReferenceAsAddress))
				{
					return _byRefTypes(type);
				}
			}
			return type;
		}

		public static bool IsSet(this LoadOptions options, LoadOptions value)
		{
			return (options & value) == value;
		}

		public static MethodInfo ResolveMethod(this Type type, string methodName, params Type[] parameterTypes)
		{
			ArgumentAssert.IsNotNull(type, "type");
			return type.ResolveMethod(methodName, Type.EmptyTypes, parameterTypes);
		}

		// todo - implement this better if needed
		public static MethodInfo ResolveMethod(this Type type, string methodName, Type[] genericArgTypes, params Type[] parameterTypes)
		{
			ArgumentAssert.IsNotNull(type, "type");
			ArgumentAssert.IsNotNull(methodName, "methodName");

			const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

			// todo - cache
			if (genericArgTypes == null || genericArgTypes.Length == 0)
			{
				return type.GetMethod(methodName, flags, null, parameterTypes, null);
			}

			var methods = type.GetMethods(flags);
			foreach (var method in methods)
			{
				if (method.Name != methodName) continue;
				if (!method.IsGenericMethodDefinition) continue;

				var genericArgs = method.GetGenericArguments();
				if (genericArgTypes.Length != genericArgs.Length) continue;

				var parameterArgs = method.GetParameters();
				if (parameterTypes.Length != parameterArgs.Length) continue;

				bool match = true;
				for (int i = 0; i < parameterArgs.Length; ++i)
				{
					if (parameterTypes[i].GetType() == typeof(GenericParameter))
					{
						var gp = (GenericParameter)parameterTypes[i];
						var parameterType = parameterArgs[i].ParameterType;

						if (parameterType.IsByRef != gp.IsByRef)
						{
							match = false;
							break;
						}

						var targetType = parameterType.IsByRef
							? parameterType.GetElementType()
							: parameterType;

						if (!targetType.IsGenericParameter)
						{
							match = false;
							break;
						}

						if (targetType.GenericParameterPosition != gp.ParameterPosition)
						{
							match = false;
							break;
						}
					}
					else
					{
						if (parameterTypes[i] != parameterArgs[i].ParameterType)
						{
							match = false;
							break;
						}
					}
				}

				if (match) return method.MakeGenericMethod(genericArgTypes);
			}

			return null;
		}

		public static MethodInfo ResolveMethod(this Type type, Type baseType, string methodName, params Type[] parameterTypes)
		{
			// todo - cache
			var baseMethod = baseType.GetMethod(methodName, parameterTypes);
			if (baseMethod == null)
			{
				return null;
			}
			return GetBestCallableOverride(type, baseMethod);
		}

		public static MethodInfo ResolveMethod(this Type type, string methodName)
		{
			// todo - cache
			return type.GetMethod(methodName);
		}

		public static PropertyInfo ResolveProperty(this Type type, string propertyName)
		{
			// todo - cache
			return type.GetProperty(propertyName);
		}

		public static FieldInfo ResolveField(this Type type, string fieldName)
		{
			// todo - cache
			return type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
		}

		internal static void EmitLdobj(this ICilWriter writer, Type type)
		{
			OpCode opCode;
			if (type.IsPrimitive && _primitiveLdobjOpCodes.TryGetValue(type, out opCode))
			{
				writer.Emit(opCode);
				return;
			}

			if (type.IsValueType || type.IsGenericParameter)
			{
				writer.Emit(OpCodes.Ldobj, type);
			}
			else
			{
				writer.Emit(OpCodes.Ldind_Ref);
			}
		}

		internal static void EmitStobj(this ICilWriter writer, Type type)
		{
			OpCode opCode;
			if (type.IsPrimitive && _primitiveStobjOpCodes.TryGetValue(type, out opCode))
			{
				writer.Emit(opCode);
				return;
			}

			if (type.IsValueType || type.IsGenericParameter)
			{
				writer.Emit(OpCodes.Stobj, type);
			}
			else
			{
				writer.Emit(OpCodes.Stind_Ref);
			}
		}

		internal static void EmitLdelem(this ICilWriter writer, Type elementType)
		{
			OpCode opCode;
			if (elementType.IsPrimitive && _primitiveLdelemOpCodes.TryGetValue(elementType, out opCode))
			{
				writer.Emit(opCode);
				return;
			}

			if (elementType.IsValueType || elementType.IsGenericParameter)
			{
				writer.Emit(OpCodes.Ldelem, elementType);
			}
			else
			{
				writer.Emit(OpCodes.Ldelem_Ref);
			}
		}

		internal static void EmitStelem(this ICilWriter writer, Type elementType)
		{
			OpCode opCode;
			if (elementType.IsPrimitive && _primitiveStelemOpCodes.TryGetValue(elementType, out opCode))
			{
				writer.Emit(opCode);
				return;
			}

			if (elementType.IsValueType || elementType.IsGenericParameter)
			{
				writer.Emit(OpCodes.Stelem, elementType);
			}
			else
			{
				writer.Emit(OpCodes.Stelem_Ref);
			}
		}

		internal static ItemType GetItemType(this LoadOptions loadOptions, Type targetType)
		{
			return loadOptions.GetItemType(targetType, false);
		}

		internal static ItemType GetItemType(this LoadOptions loadOptions, Type targetType, bool boxed)
		{
			if (targetType.IsValueType && !boxed)
			{
				return loadOptions.IsSet(LoadOptions.ValueAsAddress)
					? ItemType.AddressToValue
					: ItemType.Value;
			}

			return loadOptions.IsSet(LoadOptions.ReferenceAsAddress)
				? ItemType.AddressToReference
				: ItemType.Reference;
		}

		internal static bool ShouldBox(this LoadOptions loadOptions, Type targetType)
		{
			ArgumentAssert.IsNotNull(targetType, "targetType");

			return targetType.IsValueType && loadOptions.IsSet(LoadOptions.BoxValues);
		}

		internal static bool ShouldLoadAddress(this LoadOptions loadOptions, Type targetType)
		{
			ArgumentAssert.IsNotNull(targetType, "targetType");

			if (targetType.IsValueType)
			{
				return (loadOptions & LoadOptions.ValueAsAddress) == LoadOptions.ValueAsAddress;
			}
			return (loadOptions & LoadOptions.ReferenceAsAddress) == LoadOptions.ReferenceAsAddress;
		}

		internal static MethodInfo GetBestCallableOverride(this Type type, MethodInfo method)
		{
			if (type == null) throw new ArgumentNullException("type");
			if (method == null) throw new ArgumentNullException("method");

			if (type.IsInterface || type.IsAbstract)
			{
				if (!method.DeclaringType.IsAssignableFrom(type))
				{
					throw new ArgumentException("type does not implement method", "type");
				}
				return method;
			}

			if (method.DeclaringType.IsInterface)
			{
				var map = type.GetInterfaceMap(method.DeclaringType);

				for (int i = 0; i < map.InterfaceMethods.Length; ++i)
				{
					if (map.InterfaceMethods[i] == method)
					{
						// implemented explicity (not callable)
						if (map.TargetMethods[i].IsPrivate) break;
						return map.TargetMethods[i];
					}
				}

				if (method.DeclaringType.IsAssignableFrom(type))
				{
					return method;
				}
				throw new MissingMethodException(type.Name, method.Name);
			}

			while (type != null)
			{
				if (type == method.DeclaringType)
				{
					if (method.IsAbstract)
					{
						throw new MissingMethodException(type.Name, method.Name);
					}
					return method;
				}
				type = type.BaseType;
			}
			throw new MissingMethodException(type.Name, method.Name);
		}


		public static IVariable DefineLocal(this ICilWriter writer, Type type, bool isPinned, string name)
		{
			if (writer == null) throw new ArgumentNullException("writer");

			return writer.DefineLocal(type, isPinned, name);
		}

		public static IExpression AddMember(this IExpression valueShortcut, MemberInfo member)
		{
			ArgumentAssert.IsNotNull(member, "member");

			if (member is PropertyInfo)
			{
				return valueShortcut.AddMember((PropertyInfo)member);
			}
			if (member is FieldInfo)
			{
				return valueShortcut.AddMember((FieldInfo)member);
			}
			throw new ArgumentException("member is not a PropertyInfo or a MemberInfo", "member");
		}

		/// <summary>
		/// Emits <see langword="OpCodes.Starg"/> or the best alternative macro depending on the operand.
		/// </summary>
		/// <param name="writer">The writer to write to.</param>
		/// <param name="argumentIndex">The index of the parameter operand.</param>
		public static void EmitStarg(this ICilWriter writer, int argumentIndex)
		{
			if (writer == null) throw new ArgumentNullException("writer");
			if (argumentIndex < 0) throw new ArgumentOutOfRangeException("argumentIndex", "argumentIndex may not be negative");

			if (argumentIndex <= byte.MaxValue)
			{
				writer.Emit(OpCodes.Starg_S, (byte)argumentIndex);
				return;
			}
			writer.Emit(OpCodes.Starg, argumentIndex);
		}

		/// <summary>
		/// Emits <see langword="OpCodes.Ldarga"/> or the best alternative macro depending on the operand.
		/// </summary>
		/// <param name="writer">The writer to write to.</param>
		/// <param name="argumentIndex">The index of the parameter operand.</param>
		public static void EmitLdarga(this ICilWriter writer, int argumentIndex)
		{
			if (writer == null) throw new ArgumentNullException("writer");
			if (argumentIndex < 0) throw new ArgumentOutOfRangeException("argumentIndex", "argumentIndex may not be negative");

			if (argumentIndex <= byte.MaxValue)
			{
				writer.Emit(OpCodes.Ldarga_S, (byte)argumentIndex);
				return;
			}
			writer.Emit(OpCodes.Ldarga, argumentIndex);
		}

		/// <summary>
		/// Emits <see langword="OpCodes.Ldarg"/> or the best alternative macro depending on the operand.
		/// </summary>
		/// <param name="writer">The writer to write to.</param>
		/// <param name="argumentIndex">The index of the parameter operand.</param>
		public static void EmitLdarg(this ICilWriter writer, int argumentIndex)
		{
			if (writer == null) throw new ArgumentNullException("writer");
			if (argumentIndex < 0) throw new ArgumentOutOfRangeException("argumentIndex", "argumentIndex may not be negative");

			switch (argumentIndex)
			{
				case 0:
					writer.Emit(OpCodes.Ldarg_0);
					return;
				case 1:
					writer.Emit(OpCodes.Ldarg_1);
					return;
				case 2:
					writer.Emit(OpCodes.Ldarg_2);
					return;
				case 3:
					writer.Emit(OpCodes.Ldarg_3);
					return;
				default:
					if (argumentIndex <= byte.MaxValue)
					{
						writer.Emit(OpCodes.Ldarg_S, (byte)argumentIndex);
						return;
					}
					writer.Emit(OpCodes.Ldarg, argumentIndex);
					return;
			}
		}

		/// <summary>
		/// Emits <see langword="OpCodes.Ldloc"/> or the best alternative macro depending on the operand.
		/// </summary>
		/// <param name="writer">The writer to write to.</param>
		/// <param name="localIndex">The index of the local variable operand.</param>
		public static void EmitLdloc(this ICilWriter writer, int localIndex)
		{
			if (writer == null) throw new ArgumentNullException("writer");
			if (localIndex < 0) throw new ArgumentOutOfRangeException("localIndex", "localIndex may not be negative");

			switch (localIndex)
			{
				case 0:
					writer.Emit(OpCodes.Ldloc_0);
					return;
				case 1:
					writer.Emit(OpCodes.Ldloc_1);
					return;
				case 2:
					writer.Emit(OpCodes.Ldloc_2);
					return;
				case 3:
					writer.Emit(OpCodes.Ldloc_3);
					return;
				default:
					if (localIndex <= byte.MaxValue)
					{
						writer.Emit(OpCodes.Ldloc_S, (byte)localIndex);
						return;
					}
					writer.Emit(OpCodes.Ldloc, localIndex);
					return;
			}
		}

		/// <summary>
		/// Emits <see langword="OpCodes.Ldloca"/> or the best alternative macro depending on the operand.
		/// </summary>
		/// <param name="writer">The writer to write to.</param>
		/// <param name="localIndex">The index of the local variable operand.</param>
		public static void EmitLdloca(this ICilWriter writer, int localIndex)
		{
			if (writer == null) throw new ArgumentNullException("writer");
			if (localIndex < 0) throw new ArgumentOutOfRangeException("localIndex", "localIndex may not be negative");

			if (localIndex <= byte.MaxValue)
			{
				writer.Emit(OpCodes.Ldloca_S, (byte)localIndex);
				return;
			}
			writer.Emit(OpCodes.Ldloca, localIndex);
		}

		/// <summary>
		/// Emits <see langword="OpCodes.Stloc"/> or the best alternative macro depending on the operand.
		/// </summary>
		/// <param name="writer">The writer to write to.</param>
		/// <param name="localIndex">The index of the local variable operand.</param>
		public static void EmitStloc(this ICilWriter writer, int localIndex)
		{
			if (writer == null) throw new ArgumentNullException("writer");
			if (localIndex < 0) throw new ArgumentOutOfRangeException("localIndex", "localIndex may not be negative");

			switch (localIndex)
			{
				case 0:
					writer.Emit(OpCodes.Stloc_0);
					return;
				case 1:
					writer.Emit(OpCodes.Stloc_1);
					return;
				case 2:
					writer.Emit(OpCodes.Stloc_2);
					return;
				case 3:
					writer.Emit(OpCodes.Stloc_3);
					return;
				default:
					if (localIndex <= byte.MaxValue)
					{
						writer.Emit(OpCodes.Stloc_S, (byte)localIndex);
						return;
					}
					writer.Emit(OpCodes.Stloc, localIndex);
					return;
			}
		}

		/// <summary>
		/// Emits a macro
		/// </summary>
		/// <param name="writer">The MSIL writer to write to.</param>
		/// <param name="value">The constant <see cref="Int32"/> value to load onto the evaluation stack.</param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="writer"/> is <see langword="null"/>.</para>
		/// </exception>
		public static void EmitLdcI4(this ICilWriter writer, int value)
		{
			if (writer == null) throw new ArgumentNullException("writer");

			OpCode opCode;

			switch (value)
			{
				case -1: opCode = OpCodes.Ldc_I4_M1; break;
				case 0: opCode = OpCodes.Ldc_I4_0; break;
				case 1: opCode = OpCodes.Ldc_I4_1; break;
				case 2: opCode = OpCodes.Ldc_I4_2; break;
				case 3: opCode = OpCodes.Ldc_I4_3; break;
				case 4: opCode = OpCodes.Ldc_I4_4; break;
				case 5: opCode = OpCodes.Ldc_I4_5; break;
				case 6: opCode = OpCodes.Ldc_I4_6; break;
				case 7: opCode = OpCodes.Ldc_I4_7; break;
				case 8: opCode = OpCodes.Ldc_I4_8; break;
				default:
					opCode =
						value <= byte.MaxValue
						? OpCodes.Ldc_I4_S
						: OpCodes.Ldc_I4;
					break;
			}

			switch (opCode.OperandType)
			{
				case OperandType.InlineNone:
					writer.Emit(opCode);
					break;
				case OperandType.ShortInlineI:
					writer.Emit(opCode, (byte)value);
					break;
				case OperandType.InlineI:
					writer.Emit(opCode, value);
					break;
				default:
					throw new InvalidOperationException("Unexpected OperandType. This is an indication of a bug.");
			}
		}

		/// <summary>
		/// Emits <see cref="OpCodes.Callvirt"/> or <see cref="OpCodes.Call"/> depending on
		/// which is appropriate for <paramref name="method"/>.
		/// </summary>
		/// <param name="writer">The MSIL writer to write to.</param>
		/// <param name="method">The method to call in the output MSIL.</param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="writer"/> is <see langword="null"/>.</para>
		///	<para>- or -</para>
		///	<para><paramref name="method"/> is <see langword="null"/>.</para>
		/// </exception>
		public static void EmitCall(this ICilWriter writer, MethodInfo method)
		{
			if (writer == null) throw new ArgumentNullException("writer");
			if (method == null) throw new ArgumentNullException("method");

			OpCode opCode = !method.IsStatic && method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call;

			writer.Emit(opCode, method);
		}

		/// <summary>
		/// Gets the size of the primitive type.
		/// </summary>
		/// <param name="type">The primitive type.</param>
		/// <returns>The size of the primitive type.</returns>
		/// <exception cref="ArgumentNullException">
		///		<para><paramref name="type"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentException">
		///		<para><paramref name="type"/> is not primitive.</para>
		/// </exception>
		public static int GetPrimitiveSize(this Type type)
		{
			ArgumentAssert.IsNotNull(type, "type");
			if (!type.IsPrimitive) throw new ArgumentException("type must be a primitive.", "type");

			return _primitiveSizes[type];
		}

		/// <summary>
		/// Determines whether the specified type is a floating point type (double or float).
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns>
		/// 	<c>true</c> if type is a floating point type; otherwise, <c>false</c>.
		/// </returns>
		public static bool IsFloatingPoint(this Type type)
		{
			return type == typeof(float) || type == typeof(double);
		}
	}
}
