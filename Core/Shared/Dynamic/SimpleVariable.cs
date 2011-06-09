using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace MySpace.Common.Dynamic
{
	/// <summary>
	/// 	<para>A simple, abstract, general-purpose implementation of <see cref="IVariable"/>.
	/// 	This class is intended to be used internally by <see cref="ICilWriter"/> implementations.</para>
	/// </summary>
	internal abstract class SimpleVariable : IVariable
	{
		protected SimpleVariable(ICilWriter writer, int index, Type type, string name, bool isPinned)
		{
			ArgumentAssert.IsNotNull(writer, "writer");
			ArgumentAssert.IsNotNull(type, "type");

			Writer = writer;
			Index = index;
			Type = type;
			Name = name;
			IsPinned = isPinned;
		}

		#region IVariable Members

		protected ICilWriter Writer { get; private set; }

		protected int Index { get; private set; }

		/// <summary>
		/// Gets the type of the variable.
		/// </summary>
		/// <value>The type of the variable.</value>
		public Type Type { get; private set; }

		public string Name { get; private set; }

		/// <summary>
		/// Gets a value indicating whether the variable is pinned.
		/// </summary>
		/// <value>
		/// 	<see langword="true"/> if the variable is pinned; otherwise, <see langword="false"/>.
		/// </value>
		public bool IsPinned { get; private set; }

		/// <summary>
		/// 	<para>Initializes or resets the value of the variable to zero using the <see cref="OpCodes.Initobj"/> opcode.</para>
		/// 	<para>This operation leaves the evaluation stack in the same state as before.</para>
		/// </summary>
		public void Initialize()
		{
			if (Type.IsPrimitive)
			{
				var size = Type.GetPrimitiveSize();
				if (Type.IsFloatingPoint())
				{
					if (size == 4)
					{
						Writer.Emit(OpCodes.Ldc_R4, 0f);
						Store();
						return;
					}
					if (size == 8)
					{
						Writer.Emit(OpCodes.Ldc_R8, 0d);
						Store();
						return;
					}
				}
				else
				{
					if (size <= 4)
					{
						Writer.EmitLdcI4(0);
						Store();
						return;
					}
					if (size == 8)
					{
						Writer.Emit(OpCodes.Ldc_I8, 0L);
						Store();
						return;
					}
				}
			}

			if (Type.IsValueType || Type.IsGenericParameter)
			{
				Load(LoadOptions.AnyAsAddress);
				Writer.Emit(OpCodes.Initobj, Type);
			}
			else
			{
				Writer.Emit(OpCodes.Ldnull);
				Store();
			}
		}

		/// <summary>
		/// Emits an instruction that loads the value of the variable onto the evaluation stack.
		/// </summary>
		public abstract void Load(LoadOptions options);

		/// <summary>
		/// Emits the necessary instructions to store to this variable.
		/// </summary>
		/// <exception cref="InvalidOperationException"><see cref="CanStore"/> is <see langword="false"/>.</exception>
		public abstract void Store();

		/// <summary>
		/// Gets a value indicating whether this instance can use the <see cref="Store"/> method.
		/// </summary>
		/// <value>
		/// 	<c>true</c> if this instance can use the <see cref="Store"/> method; otherwise, <c>false</c>.
		/// </value>
		public virtual bool CanStore
		{
			get { return true; }
		}

		/// <summary>
		/// 	<para>Emits the start of the instruction block for an assignment to this variable.</para>
		/// 	<para>The value to assign should be loaded between calls to <see cref="BeginAssign"/> and <see cref="EndAssign"/>.</para>
		/// </summary>
		public virtual void BeginAssign()
		{
		}

		/// <summary>
		/// 	<para>Emits the end of the instruction block for an assignment to this variable.</para>
		/// 	<para>The value to assign should be loaded between calls to <see cref="BeginAssign"/> and <see cref="EndAssign"/>.</para>
		/// </summary>
		public virtual void EndAssign()
		{
			Store();
		}

		#endregion
	}
}
