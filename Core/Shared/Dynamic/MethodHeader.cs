using System;
using System.Reflection;

namespace MySpace.Common.Dynamic
{
	public class MethodHeader
	{
		private Type _returnType = typeof(void);
		private Type[] _parameters = Type.EmptyTypes;

		public CallingConventions Attributes { get; set; }

		public Type DeclaringType { get; set; }

		public Type ReturnType
		{
			get { return _returnType; }
			set { _returnType = value ?? typeof(void); }
		}

		public Type[] ParameterTypes
		{
			get { return _parameters; }
			set { _parameters = value ?? Type.EmptyTypes; }
		}
	}
}
