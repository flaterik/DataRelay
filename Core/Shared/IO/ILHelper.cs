using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Diagnostics;

namespace MySpace.Common.IO
{
    public class DynamicMethodHelper
    {
        #region Internal classes
        class Scope
        {
            public Dictionary<string,LocalBuilder> locals = new Dictionary<string,LocalBuilder>();
            public Dictionary<string, LabelInfo> labels = new Dictionary<string, LabelInfo>();
            public Stack<MethodInfo> methodStack = new Stack<MethodInfo>();
        }
        
        class LabelInfo
        {
            ILGenerator il;
            Label       label;
            bool        marked = false;
            bool        referenced = false;
            
            public LabelInfo(ILGenerator il)
            {
                this.il = il;
                this.label = il.DefineLabel();
            }
            
            public static implicit operator Label(LabelInfo li)
            {
                li.referenced = true;
                return li.label;
            }
            
            public void Mark()
            {
                Debug.Assert(marked == false);
                il.MarkLabel(this.label);
                marked = true;
            }
            
            public void Reference()
            {
                referenced = true;
            }
            
            public bool IsMarked
            {
                get { return this.marked; }
            }
            
            public bool IsReferenced
            {
                get { return this.referenced; }
            }
        }

        #endregion

        #region Members
        DynamicMethod               method = null;
        ILGenerator                 il = null;
        Stack<Scope>                scopes = new Stack<Scope>();
        bool                        returned = false;
        #endregion
        
        public DynamicMethodHelper(string methodName, Type retType, Type[] methodArgs, Type ownerType)
        {
            this.method = new DynamicMethod(
                                methodName,
                                retType,
                                methodArgs,
                                ownerType,
                                true);
            this.il = method.GetILGenerator();
            BeginScope();
        }
        
        internal MethodInfo MethodInfo
        {
            get { return this.method; }
        }
        
        #region Scope
        public void BeginScope()
        {
            this.scopes.Push(new Scope());
        }
        
        public void EndScope()
        {
            Scope scope = this.scopes.Pop();
            
            foreach (KeyValuePair<string,LabelInfo> li in scope.labels)
            {
                if (li.Value.IsReferenced && !li.Value.IsMarked)
                {
                    throw new ApplicationException(string.Format(
                                "The label {0} is referenced but not marked in method {1}",
                                li.Key,
                                this.method.Name
                                ));
                }
            }
            if (scope.methodStack.Count > 0)
            {
                throw new ApplicationException(string.Format(
                            "Incomplete method call for {0} in {1}",
                            scope.methodStack.Peek().Name,
                            this.method.Name
                            ));
            };
        }

        public void Return()
        {
            il.Emit(OpCodes.Ret);
            this.returned = true;
        }
        
        public Delegate Compile(Type delegateType)
        {
            if (this.scopes.Count == 0)
            {
                throw new ApplicationException(string.Format("Scope begin/end mismatch in {0}", this.method.Name));
            }
            else
            {
                EndScope();
            }

            if (returned == false)
            {
                throw new ApplicationException(string.Format("Method {0} completed without return", this.method.Name));
            }
            
            try
            {
                return this.method.CreateDelegate(delegateType);
            }
            catch (Exception x)
            {
                throw new ApplicationException(string.Format("Failed to compile {0} method", this.method.Name), x);
            }
        }

		/// <summary>
		/// Compiles this dynamic method to a specified delegate.
		/// </summary>
		/// <typeparam name="TDelegate">The type of delegate created.</typeparam>
		/// <returns>The created <typeparamref name="TDelegate"/>.</returns>
		public TDelegate Compile<TDelegate>() where TDelegate : class
		{
			return Compile(typeof(TDelegate)) as TDelegate;
		}
        
        #endregion
        
        #region Variables
        
        public void DeclareLocal(string name, Type t)
        {
            foreach (Scope scope in this.scopes)
            {
                if (scope.locals.ContainsKey(name))
                {
                    throw new ArgumentException(string.Format("The local {0} is already declared", name));
                }
            }
            this.CurrentScope.locals.Add(name, il.DeclareLocal(t));
        }

        public void PushArg(int index)
        {
            il.Emit(OpCodes.Ldarg, index);
        }
        
        public void PushArgAsRef(int index)
        {
            il.Emit(OpCodes.Ldarga, index);
        }
        
        delegate void WithLocalCallback(LocalBuilder local);
        
        void WithLocal(string name, WithLocalCallback callback)
        {
              LocalBuilder local = GetLocal(name);
              callback(local);
        }
        
        public void PushLocal(string name)
        {
            WithLocal(name, delegate(LocalBuilder local){
                il.Emit(OpCodes.Ldloc, local);
                });
        }
        
        public void PushLocalAsRef(string name)
        {
            WithLocal(name, delegate(LocalBuilder local)
                {
                    il.Emit(OpCodes.Ldloca, local);
                });
        }

        public void PushLocalAsObject(string name)
        {
            PushLocalAsObject(GetLocal(name));
        }
        
        public void PushObjectAsType(string name, Type t)
        {
            PushObjectAsType(GetLocal(name), t);
        }
        
        public void PushObjectAsType(LocalBuilder local, Type t)
        {
            il.Emit(OpCodes.Stloc, local);
            il.Emit(OpCodes.Unbox_Any, t);
        }
        
        /// <summary>
        /// Pushes a local variable in preparation for a method call
        /// </summary>
        /// <param name="local">The variable that is the context for the method call</param>
        /// <param name="method">The method that is going to be called</param>
        public void PushThis(LocalBuilder local, MethodInfo method)
        {
            Debug.Assert(method.IsStatic == false);

            if (local.LocalType.IsValueType)
            {
                if (method.DeclaringType.IsClass)
                    PushLocalAsObject(local);
                else
                    il.Emit(OpCodes.Ldloca, local);
            }
            else
            {
                il.Emit(OpCodes.Ldloc, local);
            }
        }

        /// <summary>
        /// Pushes a local variable in preparation for a method call
        /// </summary>
        /// <param name="local">The variable that is the context for the method call</param>
        /// <param name="method">The method that is going to be called</param>
        public void PushThis(string local, MethodInfo method)
        {
            PushThis(GetLocal(local), method);
        }
        
        void PushLocalAsObject(LocalBuilder local)
        {
            if (local.LocalType.IsValueType)
            {
                il.Emit(OpCodes.Ldloca, local);
                il.Emit(OpCodes.Ldobj, local.LocalType);
                il.Emit(OpCodes.Box, local.LocalType);
            }
            else
            {
                il.Emit(OpCodes.Ldloc, local);
            }
        }
        
        public void Cast(Type t)
        {
            il.Emit(OpCodes.Castclass, t);
        }
        
        public void UnboxValueType(Type t)
        {
            if (t.IsValueType) il.Emit(OpCodes.Unbox_Any, t);
        }

        public void GetField(string local, FieldInfo fld)
        {
            PushLocal(local);
            il.Emit(OpCodes.Ldfld, fld);
        }
        
        public void GetField(int argIndex, FieldInfo fld)
        {
            PushArg(argIndex);
            il.Emit(OpCodes.Ldfld, fld);
        }

        public void SetField(FieldInfo fi)
        {
            il.Emit(OpCodes.Stfld, fi);
        }
        
        public void SetField(string instName, string fieldValue, FieldInfo fi)
        {
            this.PushLocal(instName);
            this.PushLocal(fieldValue);
            il.Emit(OpCodes.Stfld, fi);
        }
        
        public void SetField(int argIndex, string fieldValue, FieldInfo fi)
        {
            this.PushArg(argIndex);
            this.PushLocal(fieldValue);
            il.Emit(OpCodes.Stfld, fi);
        }
        
        public void PushNull()
        {
            il.Emit(OpCodes.Ldnull);
        }

        public void PopArg(int index)
        {
            il.Emit(OpCodes.Starg, (byte)index);
        }

        public void PopLocal(string name)
        {
            il.Emit(OpCodes.Stloc, this.GetLocal(name));
        }
        
        public void PopLocalFromObject(string name)
        {
            LocalBuilder local = this.GetLocal(name);
            
            il.Emit(OpCodes.Unbox_Any, local.LocalType);
            il.Emit(OpCodes.Stloc, local);
        }
        
        void ThrowMissingConstructor(Type type, Type[] ctorParams)
        {
            StringBuilder sb = new StringBuilder();
            
            sb.AppendFormat("Serialization: Failed to find public constructor {0}(", type.Name);
            for (int i = 0; i < ctorParams.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(ctorParams[i].Name);
            }
            sb.Append(")");
            
            throw new ApplicationException(sb.ToString());
        }
        
        /// <summary>
        /// <para>Initializes a local variable. If the variable is a reference type
        /// then a cosntructor must exist that matches the specified parameters.</para>
        /// <para>For value types, the parameter list must be empty and the variable
        /// will be initialized to default values. In this case the local variable and target
        /// type must match.</para>
        /// </summary>
        /// <param name="local">The name of the local variable to receive the initialized instance</param>
        /// <param name="targetType">The type of the instance to create. This is usually the same type as the local variable.</param>
        /// <param name="ctorParams">Parameters to pass to the instance constructor</param>
        void NewObject(LocalBuilder local, Type targetType, Type[] ctorParams)
        {
        	if (targetType == null) 
            {
                targetType = local.LocalType;
            }

			const BindingFlags flags = BindingFlags.CreateInstance
				| BindingFlags.Instance
				| BindingFlags.Public
				| BindingFlags.NonPublic;

			if (targetType.IsArray && targetType.GetArrayRank() == 1 && ctorParams.Length == 1 && ctorParams[0] == typeof(int))
			{
				il.Emit(OpCodes.Newarr, targetType.GetElementType());
				il.Emit(OpCodes.Stloc, local);
				return;
			}
            
            var ctor = targetType.GetConstructor(flags, null, ctorParams, null);
            if (ctor == null)
            {
                if (targetType.IsClass)
                {
                    ThrowMissingConstructor(targetType, ctorParams);
                }
                else if (local.LocalType != targetType)
                {
                    throw new NotImplementedException(string.Format(
                                "Cannot initialize a value type ({0}) to local variable of a different type ({1})",
                                targetType.FullName,
                                local.LocalType.FullName
                                ));
                }
                else
                {
                    //  Load the address of the value type and initialize it
                    il.Emit(OpCodes.Ldloca, local);
                    il.Emit(OpCodes.Initobj, targetType);
                }
            }
            else
            {
                //  Call the constructor and then store the result in the local variable
                il.Emit(OpCodes.Newobj, ctor);
                il.Emit(OpCodes.Stloc, local);
            }
        }

        public void NewObject(string localName, Type type, Type[] ctorParams)
        {
            NewObject(this.GetLocal(localName), type, ctorParams);
        }

        public void NewObject(string localName, Type[] ctorParams)
        {
            NewObject(this.GetLocal(localName), null, ctorParams);
        }

        public void NewObject(string localName)
        {
            NewObject(this.GetLocal(localName), null, Type.EmptyTypes);
        }

        public void PushInt(int i)
        {
            switch (i)
            {
                case 0: il.Emit(OpCodes.Ldc_I4_0); break;
                case 1: il.Emit(OpCodes.Ldc_I4_1); break;
                case 2: il.Emit(OpCodes.Ldc_I4_2); break;
                case 3: il.Emit(OpCodes.Ldc_I4_3); break;
                case 4: il.Emit(OpCodes.Ldc_I4_4); break;
                case 5: il.Emit(OpCodes.Ldc_I4_5); break;
                case 6: il.Emit(OpCodes.Ldc_I4_6); break;
                case 7: il.Emit(OpCodes.Ldc_I4_7); break;
                case 8: il.Emit(OpCodes.Ldc_I4_8); break;
                case -1: il.Emit(OpCodes.Ldc_I4_M1); break;
                default: il.Emit(OpCodes.Ldc_I4, i); break;
            }
        }
        
        public void PushBool(bool value)
        {
            PushInt(value ? 1 : 0);
        }
        
        public void Pop()
        {
            il.Emit(OpCodes.Pop);
        }
        
        public void Box(Type t)
        {
            il.Emit(OpCodes.Box, t);
        }
        
        public void CopyLocal(string local1, string local2)
        {
            this.PushLocal(local1);
            this.PopLocal(local2);
        }
        
        public void IncrementLocal(string name)
        {
            LocalBuilder    local = this.GetLocal(name);
            
            il.Emit(OpCodes.Ldloc, local);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Stloc, local);
        }
        
        public void CompareEqual()
        {
            il.Emit(OpCodes.Ceq);
        }
        #endregion
        
        #region Flow control
        public void MarkLabel(string name)
        {
            GetLabel(name).Mark();
        }

        public void BeginCallMethod(string localName, string methodName)
        {
            BeginCallMethod(localName, methodName, Type.EmptyTypes);
        }
        
        public void BeginCallMethod(string localName, string methodName, Type[] paramTypes)
        {
            LocalBuilder    local = this.GetLocal(localName);
            MethodInfo      method = local.LocalType.GetMethod(
                                            methodName, 
                                            BindingFlags.Public|BindingFlags.Instance|BindingFlags.FlattenHierarchy,
                                            null,
                                            paramTypes,
                                            null);
            
            if (method == null)
            {
                throw new ArgumentException(string.Format("The method {0} does not exist in type {1}", methodName, local.LocalType.Name));
            }

            BeginCallMethod(localName, method);
        }
        
        public void BeginCallMethod(string localName, MethodInfo method)
        {
            LocalBuilder    local = this.GetLocal(localName);
            
            Debug.Assert(method != null);

            PushThis(local, method);
            
            if (local.LocalType != method.DeclaringType)
            {
                Cast(method.DeclaringType);
            }
            
            this.CurrentScope.methodStack.Push(method);
        }

        public void CallMethod()
        {
            CallMethod(this.CurrentScope.methodStack.Pop());
        }

        public void CallMethod(string localName, string methodName)
        {
            BeginCallMethod(localName, methodName, Type.EmptyTypes);
            CallMethod();
        }
        
        public void CallMethod(string localName, string methodName, Type[] paramTypes)
        {
            BeginCallMethod(localName, methodName, paramTypes);
            CallMethod();
        }
        
        public void CallMethod(MethodInfo method)
        {
            Debug.Assert(method != null, "Method is null");
            il.Emit(method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, method);
        }
        
        public void CallMethod(string localName, MethodInfo method)
        {
            PushLocal(localName);
            CallMethod(method);
        }
        
        public void Goto(string label)
        {
            il.Emit(OpCodes.Br, GetLabel(label));
        }

        public void GotoIfFalse(string l)
        {
            il.Emit(OpCodes.Brfalse, GetLabel(l));
        }

        public void GotoIfTrue(string l)
        {
            il.Emit(OpCodes.Brtrue, GetLabel(l));
        }
        
        public void GotoIfGreaterOrEqual(string l)
        {
            il.Emit(OpCodes.Bge, GetLabel(l));
        }

        public void GotoIfGreater(string l)
        {
            il.Emit(OpCodes.Bgt, GetLabel(l));
        }

        public void GotoIfLess(string l)
        {
            il.Emit(OpCodes.Blt, GetLabel(l));
        }

        public void GotoIfEqual(string l)
        {
            il.Emit(OpCodes.Beq, GetLabel(l));
        }
        
        public void GotoIfNotEqual(string l)
        {
            il.Emit(OpCodes.Ceq);
            GotoIfFalse(l);
        }
        
        #endregion

        #region Output

		[Conditional("DEBUG")]    
        public void DebugWriteNamedLocal(string name)
        {
			DebugWriteLine(string.Format("Local {0}:", name));
            DebugWriteLocal(name);
        }
        
        public void WriteStack()
        {
            il.Emit(
                    OpCodes.Call,
                    typeof(Console).GetMethod("WriteLine", new Type[] { typeof(object) })
                    );
        }
        
        public void WriteString(string s)
        {
            il.EmitWriteLine(s);
        }
        
        [Conditional("DEBUG")]        
        public void DebugWriteLine(string s)
        {
            il.Emit(OpCodes.Ldstr, s);
            il.Emit(OpCodes.Call, typeof(Debug).GetMethod("WriteLine", new Type[] { typeof(string) }));
        }

        [Conditional("DEBUG")]        
        public void DebugWriteLocal(string s)
        {
            PushLocal(s);
            il.Emit(OpCodes.Call, typeof(Debug).GetMethod("WriteLine", new Type[] { typeof(object) }));
        }

        [Conditional("DEBUG")]        
        public void DebugFail(string s)
        {
            il.Emit(OpCodes.Ldstr, s);
            il.Emit(OpCodes.Call, typeof(Debug).GetMethod("Fail", new Type[] { typeof(string) }));
        }
        
        #endregion

        #region Internal helpers
        Scope CurrentScope
        {
            get { return this.scopes.Peek(); }
        }
        
        LocalBuilder GetLocal(string name)
        {
            foreach (Scope scope in this.scopes)
            {
                if (scope.locals.ContainsKey(name))
                {
                    return scope.locals[name];
                }
            }
            
            throw new ArgumentException(string.Format("The local {0} has not been declared", name));
        }

        Dictionary<string, LabelInfo> Labels
        {
            get { return this.CurrentScope.labels; }
        }

        LabelInfo GetLabel(string name)
        {
            if (this.Labels.ContainsKey(name) == false)
            {
                this.Labels.Add(name, new LabelInfo(il));
            }
            return this.Labels[name];
        }
        #endregion

    }
}
