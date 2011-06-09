using System;
using System.Diagnostics;
using System.Linq;

namespace MySpace.Common.Dynamic
{
	[DebuggerDisplay("{Display}")]
	internal class GenericParameter : Type
	{
		public GenericParameter(int parameterPosition)
			: this(parameterPosition, false)
		{
		}

		public GenericParameter(int parameterPosition, bool isByRef)
		{
			ParameterPosition = parameterPosition;
			_isByRef = isByRef;
		}

		private string Display 
		{
			get { return "T" + ParameterPosition + (_isByRef ? "&" : string.Empty); }
		}

		private readonly bool _isByRef;
		public int ParameterPosition { get; private set; }

		public override System.Reflection.Assembly Assembly
		{
			get { throw new NotImplementedException(); }
		}

		public override string AssemblyQualifiedName
		{
			get { throw new NotImplementedException(); }
		}

		public override Type BaseType
		{
			get { throw new NotImplementedException(); }
		}

		public override string FullName
		{
			get { throw new NotImplementedException(); }
		}

		public override Guid GUID
		{
			get { throw new NotImplementedException(); }
		}

		protected override System.Reflection.TypeAttributes GetAttributeFlagsImpl()
		{
			throw new NotImplementedException();
		}

		protected override System.Reflection.ConstructorInfo GetConstructorImpl(System.Reflection.BindingFlags bindingAttr, System.Reflection.Binder binder, System.Reflection.CallingConventions callConvention, Type[] types, System.Reflection.ParameterModifier[] modifiers)
		{
			throw new NotImplementedException();
		}

		public override System.Reflection.ConstructorInfo[] GetConstructors(System.Reflection.BindingFlags bindingAttr)
		{
			throw new NotImplementedException();
		}

		public override Type GetElementType()
		{
			throw new NotImplementedException();
		}

		public override System.Reflection.EventInfo GetEvent(string name, System.Reflection.BindingFlags bindingAttr)
		{
			throw new NotImplementedException();
		}

		public override System.Reflection.EventInfo[] GetEvents(System.Reflection.BindingFlags bindingAttr)
		{
			throw new NotImplementedException();
		}

		public override System.Reflection.FieldInfo GetField(string name, System.Reflection.BindingFlags bindingAttr)
		{
			throw new NotImplementedException();
		}

		public override System.Reflection.FieldInfo[] GetFields(System.Reflection.BindingFlags bindingAttr)
		{
			throw new NotImplementedException();
		}

		public override Type GetInterface(string name, bool ignoreCase)
		{
			throw new NotImplementedException();
		}

		public override Type[] GetInterfaces()
		{
			throw new NotImplementedException();
		}

		public override System.Reflection.MemberInfo[] GetMembers(System.Reflection.BindingFlags bindingAttr)
		{
			throw new NotImplementedException();
		}

		protected override System.Reflection.MethodInfo GetMethodImpl(string name, System.Reflection.BindingFlags bindingAttr, System.Reflection.Binder binder, System.Reflection.CallingConventions callConvention, Type[] types, System.Reflection.ParameterModifier[] modifiers)
		{
			throw new NotImplementedException();
		}

		public override System.Reflection.MethodInfo[] GetMethods(System.Reflection.BindingFlags bindingAttr)
		{
			throw new NotImplementedException();
		}

		public override Type GetNestedType(string name, System.Reflection.BindingFlags bindingAttr)
		{
			throw new NotImplementedException();
		}

		public override Type[] GetNestedTypes(System.Reflection.BindingFlags bindingAttr)
		{
			throw new NotImplementedException();
		}

		public override System.Reflection.PropertyInfo[] GetProperties(System.Reflection.BindingFlags bindingAttr)
		{
			throw new NotImplementedException();
		}

		protected override System.Reflection.PropertyInfo GetPropertyImpl(string name, System.Reflection.BindingFlags bindingAttr, System.Reflection.Binder binder, Type returnType, Type[] types, System.Reflection.ParameterModifier[] modifiers)
		{
			throw new NotImplementedException();
		}

		protected override bool HasElementTypeImpl()
		{
			throw new NotImplementedException();
		}

		public override object InvokeMember(string name, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder binder, object target, object[] args, System.Reflection.ParameterModifier[] modifiers, System.Globalization.CultureInfo culture, string[] namedParameters)
		{
			throw new NotImplementedException();
		}

		protected override bool IsArrayImpl()
		{
			throw new NotImplementedException();
		}

		public override Type MakeByRefType()
		{
			if (_isByRef) throw new InvalidOperationException("This is already a by ref type.");
			return new GenericParameter(ParameterPosition, true);
		}

		protected override bool IsByRefImpl()
		{
			return _isByRef;
		}

		protected override bool IsCOMObjectImpl()
		{
			throw new NotImplementedException();
		}

		protected override bool IsPointerImpl()
		{
			throw new NotImplementedException();
		}

		protected override bool IsPrimitiveImpl()
		{
			throw new NotImplementedException();
		}

		public override System.Reflection.Module Module
		{
			get { throw new NotImplementedException(); }
		}

		public override string Namespace
		{
			get { throw new NotImplementedException(); }
		}

		public override Type UnderlyingSystemType
		{
			get { throw new NotImplementedException(); }
		}

		public override object[] GetCustomAttributes(Type attributeType, bool inherit)
		{
			throw new NotImplementedException();
		}

		public override object[] GetCustomAttributes(bool inherit)
		{
			throw new NotImplementedException();
		}

		public override bool IsDefined(Type attributeType, bool inherit)
		{
			throw new NotImplementedException();
		}

		public override string Name
		{
			get { throw new NotImplementedException(); }
		}
	}
}
