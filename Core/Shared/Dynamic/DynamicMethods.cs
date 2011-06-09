using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace MySpace.Common
{
	/// <summary>
	/// 	<para>Encapsulates utility methods that take advantage of dynamically generated IL.</para>
	/// </summary>
	public static class DynamicMethods
	{
		private static int _nextId;

		private static string NextMethodName()
		{
			return string.Format("__MySpace_Common_Dynamic_{0}", Interlocked.Increment(ref _nextId));
		}

		/// <summary>
		///	<para>Gets a dynamic method that calls <typeparamref name="TResult"/>'s default constructor.</para>
		/// </summary>
		/// <typeparam name="TResult">
		///	<para>The type to construct.</para>
		/// </typeparam>
		/// <returns>
		///	<para>A <see cref="Factory{TResult}"/> that calls <typeparamref name="TResult"/>'s default constructor.</para>
		/// </returns>
		/// <exception cref="ArgumentException">
		///	<para><typeparamref name="TResult"/> doesn't have a default constructor.</para>
		/// </exception>
		public static Factory<TResult> GetCtor<TResult>()
		{
			if (TypeSpecific<TResult>.Ctor != null) return TypeSpecific<TResult>.Ctor;
			lock (TypeSpecific<TResult>.CtorRoot)
			{
				if (TypeSpecific<TResult>.Ctor != null) return TypeSpecific<TResult>.Ctor;

				var ctor = CreateCtor(typeof(TResult), Type.EmptyTypes, typeof(Factory<TResult>), "TResult");
				Thread.MemoryBarrier();
				TypeSpecific<TResult>.Ctor = (Factory<TResult>)ctor;
				return TypeSpecific<TResult>.Ctor;
			}
		}

		/// <summary>
		///	<para>Gets a dynamic method that calls <typeparamref name="TResult"/>'s constructor that accepts the parameter <typeparamref name="T"/>.</para>
		/// </summary>
		/// <typeparam name="T">
		///	<para>The type of the constructor's parameter.</para>
		/// </typeparam>
		/// <typeparam name="TResult">
		///	<para>The type to construct.</para>
		/// </typeparam>
		/// <returns>
		///	<para>A dynamic method that calls <typeparamref name="TResult"/>'s constructor that accepts the parameter <typeparamref name="T"/>.</para>
		/// </returns>
		/// <exception cref="ArgumentException">
		///	<para><typeparamref name="TResult"/> doesn't have a constructor that accepts <typeparamref name="T"/>.</para>
		/// </exception>
		public static Factory<T, TResult> GetCtor<T, TResult>()
		{
			if (TypeSpecific<T, TResult>.Ctor != null) return TypeSpecific<T, TResult>.Ctor;
			lock (TypeSpecific<T, TResult>.CtorRoot)
			{
				if (TypeSpecific<T, TResult>.Ctor != null) return TypeSpecific<T, TResult>.Ctor;

				var ctor = CreateCtor(typeof(TResult), new [] { typeof(T) }, typeof(Factory<T, TResult>), "TResult");
				Thread.MemoryBarrier();
				TypeSpecific<T, TResult>.Ctor = (Factory<T, TResult>)ctor;
				return TypeSpecific<T, TResult>.Ctor;
			}
		}

		/// <summary>
		///	<para>Gets a dynamic method that calls <typeparamref name="TResult"/>'s constructor that
		///	accepts a <typeparamref name="T1"/> and a <typeparamref name="T2".</para>
		/// </summary>
		/// <typeparam name="T1">
		///	<para>The type of the constructor's first parameter.</para>
		/// </typeparam>
		/// <typeparam name="T2">
		///	<para>The type of the constructor's second parameter.</para>
		/// </typeparam>
		/// <typeparam name="TResult">
		///	<para>The type to construct.</para>
		/// </typeparam>
		/// <returns>
		///	<para>A dynamic method that calls <typeparamref name="TResult"/>'s constructor that
		///	accepts a <typeparamref name="T1"/> and a <typeparamref name="T2".</para>
		/// </returns>
		/// <exception cref="ArgumentException">
		///	<para><typeparamref name="TResult"/> doesn't have a constructor that accepts
		///	<typeparamref name="T1"/> and <typeparamref name="T2"/>.</para>
		/// </exception>
		public static Factory<T1, T2, TResult> GetCtor<T1, T2, TResult>()
		{
			if (TypeSpecific<T1, T2, TResult>.Ctor != null) return TypeSpecific<T1, T2, TResult>.Ctor;
			lock (TypeSpecific<T1, T2, TResult>.CtorRoot)
			{
				if (TypeSpecific<T1, T2, TResult>.Ctor != null) return TypeSpecific<T1, T2, TResult>.Ctor;

				var ctor = CreateCtor(typeof(TResult), new [] { typeof(T1), typeof(T2) }, typeof(Factory<T1, T2, TResult>), "TResult");
				Thread.MemoryBarrier();
				TypeSpecific<T1, T2, TResult>.Ctor = (Factory<T1, T2, TResult>)ctor;
				return TypeSpecific<T1, T2, TResult>.Ctor;
			}
		}

		/// <summary>
		///	<para>Gets a dynamic method that calls <paramref name="type"/>'s default constructor.</para>
		/// </summary>
		/// <typeparam name="TBase">
		///	<para>The type of return parameter of the dynamic method.
		///	Must be able to cast <paramref name="type"/> to <typeparamref name="TBase"/>.</para>
		/// </typeparam>
		/// <param name="type">
		///	<para>The type to construct.</para>
		/// </param>
		/// <returns>
		///	<para>A <see cref="Factory{TBase}"/> that calls <paramref name="type"/>'s default constructor.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="type"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentException">
		///	<para><paramref name="type"/> is not castable to <typeparamref name="TBase"/>.</para>
		///	<para>- or -</para>
		///	<para><paramref name="type"/> doesn't have a default constructor.</para>
		/// </exception>
		public static Factory<TBase> GetCtor<TBase>(Type type)
		{
			if (type == null) throw new ArgumentNullException("type");

			if (!(typeof(TBase).IsAssignableFrom(type)))
			{
				throw new ArgumentException(string.Format("Type '{0}' does not derive from '{1}'.", type, typeof(TBase)), "type");
			}

			Factory<TBase> result;
			if (TypeSpecific<TBase>.DefaultCtors.TryGetValue(type, out result)) return result;
			lock (TypeSpecific<TBase>.DefaultCtorsRoot)
			{
				if (TypeSpecific<TBase>.DefaultCtors.TryGetValue(type, out result)) return result;

				result = (Factory<TBase>)CreateCtor(type, Type.EmptyTypes, typeof(Factory<TBase>), "type");
				var defaultCtors = new Dictionary<Type, Factory<TBase>>(TypeSpecific<TBase>.DefaultCtors);
				defaultCtors.Add(type, result);
				Thread.MemoryBarrier();
				TypeSpecific<TBase>.DefaultCtors = defaultCtors;
				return result;
			}
		}

		/// <summary>
		///	<para>Gets a dynamic method that calls <paramref name="type"/>'s constructor
		///	with a single parameter of type <typeparamref name="T"/>.</para>
		/// </summary>
		/// <typeparam name="T">
		///	<para>The type of the constructor's parameter.</para>
		/// </typeparam>
		/// <typeparam name="TResultBase">
		///	<para>The type of return parameter of the dynamic method.
		///	Must be able to cast <paramref name="type"/> to <typeparamref name="TResultBase"/>.</para>
		/// </typeparam>
		/// <param name="type">
		///	<para>The type to construct.</para>
		/// </param>
		/// <returns>
		///	<para>A <see cref="Factory{T, TResultBase}"/> that calls <paramref name="type"/>'s constructor
		///	with a single parameter of type <typeparamref name="T"/>.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="type"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentException">
		///	<para><paramref name="type"/> is not castable to <typeparamref name="TResultBase"/>.</para>
		///	<para>- or -</para>
		///	<para><paramref name="type"/> doesn't have a constructor with a single parameter of type <typeparamref name="T"/>.</para>
		/// </exception>
		public static Factory<T, TResultBase> GetCtor<T, TResultBase>(Type type)
		{
			if (type == null) throw new ArgumentNullException("type");

			if (!(typeof(TResultBase).IsAssignableFrom(type)))
			{
				throw new ArgumentException(string.Format("Type '{0}' does not derive from '{1}'.", type, typeof(TResultBase)), "type");
			}

			Factory<T, TResultBase> result;
			if (TypeSpecific<T, TResultBase>.Ctors.TryGetValue(type, out result)) return result;
			lock (TypeSpecific<T, TResultBase>.CtorsRoot)
			{
				if (TypeSpecific<T, TResultBase>.Ctors.TryGetValue(type, out result)) return result;

				result = (Factory<T, TResultBase>)CreateCtor(type, new [] { typeof(T) }, typeof(Factory<T, TResultBase>), "type");
				var defaultCtors = new Dictionary<Type, Factory<T, TResultBase>>(TypeSpecific<T, TResultBase>.Ctors);
				defaultCtors.Add(type, result);
				Thread.MemoryBarrier();
				TypeSpecific<T, TResultBase>.Ctors = defaultCtors;
				return result;
			}
		}

		/// <summary>
		/// 	<para>Gets a dynamic method that calls <paramref name="type"/>'s constructor
		/// with two parameters of types <typeparamref name="T1"/> and <typeparamref name="T2"/>.</para>
		/// </summary>
		/// <typeparam name="T1">
		/// 	<para>The type of the constructor's first parameter.</para>
		/// </typeparam>
		/// <typeparam name="T2">
		/// 	<para>The type of the constructor's second parameter.</para>
		/// </typeparam>
		/// <typeparam name="TResultBase">
		/// 	<para>The type of return parameter of the dynamic method.
		///		Must be able to cast <paramref name="type"/> to <typeparamref name="TResultBase"/>.</para>
		/// </typeparam>
		/// <param name="type">
		/// 	<para>The type to construct.  Must be a descendent of <typeparamref name="TResultBase"/>.</para>
		/// </param>
		/// <returns>
		/// 	<para>A <see cref="Factory{T, TResultBase}"/> that calls <paramref name="type"/>'s constructor
		/// with a single parameter of type <typeparamref name="T"/>.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// 	<para><paramref name="type"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// 	<para><paramref name="type"/> is not castable to <typeparamref name="TResultBase"/>.</para>
		/// 	<para>- or -</para>
		/// 	<para><paramref name="type"/> doesn't have a constructor with a single parameter of type <typeparamref name="T"/>.</para>
		/// </exception>
		public static Factory<T1, T2, TResultBase> GetCtor<T1, T2, TResultBase>(Type type)
		{
			if (type == null) throw new ArgumentNullException("type");

			if (!(typeof(TResultBase).IsAssignableFrom(type)))
			{
				throw new ArgumentException(string.Format("Type '{0}' does not derive from '{1}'.", type, typeof(TResultBase)), "type");
			}

			Factory<T1, T2, TResultBase> result;
			if (TypeSpecific<T1, T2, TResultBase>.Ctors.TryGetValue(type, out result)) return result;
			lock (TypeSpecific<T1, T2, TResultBase>.CtorsRoot)
			{
				if (TypeSpecific<T1, T2, TResultBase>.Ctors.TryGetValue(type, out result)) return result;

				result = (Factory<T1, T2, TResultBase>)CreateCtor(type, new [] { typeof(T1), typeof(T2) }, typeof(Factory<T1, T2, TResultBase>), "type");
				var defaultCtors = new Dictionary<Type, Factory<T1, T2, TResultBase>>(TypeSpecific<T1, T2, TResultBase>.Ctors);
				defaultCtors.Add(type, result);
				Thread.MemoryBarrier();
				TypeSpecific<T1, T2, TResultBase>.Ctors = defaultCtors;
				return result;
			}
		}

		private static Delegate CreateCtor(Type type, Type[] parameterTypes, Type delegateType, string typeParameterName)
		{
			var isVisible = type.IsVisible;
			var defaultValueCtor = type.IsValueType && parameterTypes.Length == 0;
			ConstructorInfo ctorInfo = null;
			if (!defaultValueCtor)
			{
				ctorInfo = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
				                                   parameterTypes, null);
				if (ctorInfo == null)
				{
					string parameterString = string.Empty;
					if (parameterTypes.Length > 0)
					{
						string[] parameterStrings = new string[parameterTypes.Length];
						for (int i = 0; i < parameterTypes.Length; ++i)
						{
							parameterStrings[i] = parameterTypes[i].ToString();
						}
						parameterString = string.Join(",", parameterStrings);
					}
					throw new ArgumentException(string.Format("Type '{0}' does not define .ctor({1}).", type, parameterString),
					                            typeParameterName);
				}

				isVisible &= (ctorInfo.IsPublic && !ctorInfo.IsFamilyOrAssembly);
			}

			DynamicMethod dynamicCtor = new DynamicMethod(NextMethodName(), type, parameterTypes, type.Module, !isVisible);
			var il = dynamicCtor.GetILGenerator();
			if (defaultValueCtor)
			{
				il.DeclareLocal(type);
				il.Emit(OpCodes.Ldloc_0);
			}
			for (int i = 0; i < parameterTypes.Length; ++i)
			{
				switch (i)
				{
					case 0: il.Emit(OpCodes.Ldarg_0); break;
					case 1: il.Emit(OpCodes.Ldarg_1); break;
					case 2: il.Emit(OpCodes.Ldarg_2); break;
					case 3: il.Emit(OpCodes.Ldarg_3); break;
					default: il.Emit(OpCodes.Ldarg, i); break;
				}
			}
			if (!defaultValueCtor)
			{
				il.Emit(OpCodes.Newobj, ctorInfo);
			}
			il.Emit(OpCodes.Ret);
			return dynamicCtor.CreateDelegate(delegateType);
		}

		private static class TypeSpecific<T>
		{
			public static Factory<T> Ctor;
			public static readonly object CtorRoot = new object();
			public static Dictionary<Type, Factory<T>> DefaultCtors = new Dictionary<Type, Factory<T>>();
			public static readonly object DefaultCtorsRoot = new object();
		}

		private static class TypeSpecific<T1, T2>
		{
			public static Factory<T1, T2> Ctor;
			public static readonly object CtorRoot = new object();
			public static Dictionary<Type, Factory<T1, T2>> Ctors = new Dictionary<Type, Factory<T1, T2>>();
			public static readonly object CtorsRoot = new object();
		}

		private static class TypeSpecific<T1, T2, T3>
		{
			public static Factory<T1, T2, T3> Ctor;
			public static readonly object CtorRoot = new object();
			public static Dictionary<Type, Factory<T1, T2, T3>> Ctors = new Dictionary<Type, Factory<T1, T2, T3>>();
			public static readonly object CtorsRoot = new object();
		}
	}
}
