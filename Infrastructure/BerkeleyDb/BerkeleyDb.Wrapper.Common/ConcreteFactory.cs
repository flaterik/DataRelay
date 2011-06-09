using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security;
using System.Text;
using MySpace.Common.IO;

namespace BerkeleyDbWrapper
{
	/// <summary>
	/// Creates instances of implementation classes for specified abstract base classes.
	/// </summary>
	internal class ConcreteFactory
	{
		#region Fields and Init
		private static readonly Assembly _asmWrapper;

		static ConcreteFactory()
		{
			string platform;
			var ptrSize = IntPtr.Size;
			switch (ptrSize)
			{
				case 4:
					platform = "Win32";
					break;
				case 8:
					platform = "x64";
					break;
				default:
					throw new ApplicationException(string.Format("Pointer size of {0} not handled", ptrSize));
			}
			var name = string.Format("MySpace.BerkeleyDb.Wrapper.{0}.exe", platform);
			_asmWrapper = Assembly.LoadFrom(name);				
		}
		#endregion

		public static T Create<T>()
		{
			return CtorHolder<T>.Ctor();
		}

		public static T Create<TParm, T>(TParm parm)
		{
			return CtorHolder<TParm, T>.Ctor(parm);
		}

		public static T Create<TParm1, TParm2, T>(TParm1 parm1, TParm2 parm2)
		{
			return CtorHolder<TParm1, TParm2, T>.Ctor(parm1, parm2);
		}

		#region Helper Classes
		/// <summary>
		/// Holds type information and supplies constructors for an implementation class.
		/// </summary>
		/// <typeparam name="T">The implementation class type.</typeparam>
		private static class TypeHolder<T>
		{
			private static readonly Type _concreteType;
			private static readonly Type _abstractType;

			static TypeHolder()
			{
				_abstractType = typeof(T);
				var typeName = _abstractType.FullName + "Impl";
				_concreteType = _asmWrapper.GetType(typeName, true);
			}

			public static TDlg GetConstructor<TDlg>(Type[] paramTypes)
			{
				var ctor = _concreteType.GetConstructor(paramTypes);
				if (ctor == null)
				{
					throw new ArgumentOutOfRangeException("paramTypes");
				}
				var paramCount = paramTypes.Length;
				var meth = new DynamicMethod(string.Empty, _abstractType, paramTypes, _concreteType, true);
				paramTypes.Select((pt, i) => meth.DefineParameter(i + 1, ParameterAttributes.In,
					string.Empty));				
				var il = meth.GetILGenerator();
				for (var i = 0; i < paramCount; ++i)
				{
					OpCode ldarg;
					switch (i)
					{
						case 0:
							ldarg = OpCodes.Ldarg_0;
							goto SingleCode;
						case 1:
							ldarg = OpCodes.Ldarg_1;
							goto SingleCode;
						case 2:
							ldarg = OpCodes.Ldarg_2;
							goto SingleCode;
						case 3:
							ldarg = OpCodes.Ldarg_3;
						SingleCode:
							il.Emit(ldarg);
							break;
						default:
							il.Emit(OpCodes.Ldarga_S, (byte)i);
							break;
					}
				}
				il.Emit(OpCodes.Newobj, ctor);
				il.Emit(OpCodes.Ret);
				return (TDlg) (object) meth.CreateDelegate(typeof(TDlg));
			}
		}

		/// <summary>
		/// Holds a parameterless constructor for an implementation class.
		/// </summary>
		/// <typeparam name="T">The implementation class type.</typeparam>
		private static class CtorHolder<T>
		{
			public static readonly Func<T> Ctor = TypeHolder<T>.GetConstructor<Func<T>>(Type.EmptyTypes);
		}

		/// <summary>
		/// Holds a 1 parameter constructor for an implementation class.
		/// </summary>
		/// <typeparam name="TParm">The constructor parameter type.</typeparam>
		/// <typeparam name="T">The implementation class type.</typeparam>
		private static class CtorHolder<TParm, T>
		{
			public static readonly Func<TParm, T> Ctor =
				TypeHolder<T>.GetConstructor<Func<TParm, T>>(new[] { typeof(TParm) });
		}

		/// <summary>
		/// Holds a 2 parameter constructor for an implementation class.
		/// </summary>
		/// <typeparam name="TParm1">The first constructor parameter type.</typeparam>
		/// <typeparam name="TParm2">The second constructor parameter type.</typeparam>
		/// <typeparam name="T">The implementation class type.</typeparam>
		private static class CtorHolder<TParm1, TParm2, T>
		{
			public static readonly Func<TParm1, TParm2, T> Ctor =
				TypeHolder<T>.GetConstructor<Func<TParm1, TParm2, T>>(new[] { typeof(TParm1), typeof(TParm2) });
		}
		#endregion
	}
}
