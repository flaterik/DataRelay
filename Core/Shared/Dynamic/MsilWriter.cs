using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace MySpace.Common.Dynamic.Reflection
{
	/// <summary>
	/// 	<para>An <see cref="ICilWriter"/> implementation for writing to <see cref="DynamicMethod"/> instances.</para>
	/// </summary>
	public class MsilWriter : ICilWriter
	{
		private readonly SimpleParameter[] _parameters;
		private readonly ILGenerator _ilGenerator;
		private readonly MethodHeader _methodHeader;

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="MsilWriter"/> class.</para>
		/// </summary>
		/// <param name="method">The dynamic method to write to.</param>
		public MsilWriter(DynamicMethod method)
		{
			var parameters = method.GetParameters();
			_methodHeader = new MethodHeader
			{
				Attributes = method.CallingConvention,
				DeclaringType = method.DeclaringType,
				ParameterTypes = method.GetParameters()
					.Select<ParameterInfo, Type>(p => p.ParameterType)
					.ToArray<Type>(),
				ReturnType = method.ReturnType ?? typeof(void)
			};
			_parameters = new SimpleParameter[parameters.Length];
			int positionOffset = method.IsStatic ? 0 : 1;
			for (int i = 0; i < parameters.Length; ++i)
			{
				_parameters[i] = new SimpleParameter(
					this,
					parameters[i].Position + positionOffset,
					parameters[i].ParameterType,
					null);
			}
			_ilGenerator = method.GetILGenerator();
		}

		#region ICilWriter Members

		MethodHeader ICilWriter.MethodHeader
		{
			[DebuggerStepThrough]
			get { return _methodHeader; }
		}

		IVariable ICilWriter.GetParameter(int index)
		{
			return _parameters[index];
		}

		IVariable ICilWriter.DefineLocal(Type type, bool isPinned, string name)
		{
			var localBuilder = _ilGenerator.DeclareLocal(type, isPinned);
			return new SimpleLocal(this, localBuilder.LocalIndex, type, name, localBuilder.IsPinned);
		}

		ILabel ICilWriter.DefineLabel()
		{
			return new LabelInfo(this, _ilGenerator.DefineLabel());
		}

		void ICilWriter.BeginTry()
		{
			_ilGenerator.BeginExceptionBlock();
		}

		void ICilWriter.BeginCatch(Type exceptionType)
		{
			_ilGenerator.BeginCatchBlock(exceptionType);
		}

		void ICilWriter.BeginFinally()
		{
			_ilGenerator.BeginFinallyBlock();
		}

		void ICilWriter.EndTryCatchFinally()
		{
			_ilGenerator.EndExceptionBlock();
		}

		void ICilWriter.Emit(OpCode opCode)
		{
			_ilGenerator.Emit(opCode);
		}

		void ICilWriter.Emit(OpCode opCode, byte operand)
		{
			_ilGenerator.Emit(opCode, operand);
		}

		void ICilWriter.Emit(OpCode opCode, int operand)
		{
			_ilGenerator.Emit(opCode, operand);
		}

		void ICilWriter.Emit(OpCode opCode, long operand)
		{
			_ilGenerator.Emit(opCode, operand);
		}

		void ICilWriter.Emit(OpCode opCode, float operand)
		{
			_ilGenerator.Emit(opCode, operand);
		}

		void ICilWriter.Emit(OpCode opCode, double operand)
		{
			_ilGenerator.Emit(opCode, operand);
		}

		void ICilWriter.Emit(OpCode opCode, string operand)
		{
			_ilGenerator.Emit(opCode, operand);
		}

		void ICilWriter.Emit(OpCode opCode, Type operand)
		{
			_ilGenerator.Emit(opCode, operand);
		}

		void ICilWriter.Emit(OpCode opCode, MethodInfo operand)
		{
			_ilGenerator.Emit(opCode, operand);
		}

		void ICilWriter.Emit(OpCode opCode, ConstructorInfo operand)
		{
			_ilGenerator.Emit(opCode, operand);
		}

		void ICilWriter.Emit(OpCode opCode, FieldInfo operand)
		{
			_ilGenerator.Emit(opCode, operand);
		}

		void ICilWriter.Emit(OpCode opCode, ILabel label)
		{
			var l = (LabelInfo)label;
			l.IsReferenced = true;
			_ilGenerator.Emit(opCode, l.Label);
		}

		#endregion

		#region IDisposable Members

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
		}

		#endregion

		private class LabelInfo : ILabel
		{
			private readonly MsilWriter _writer;
			private readonly Label _label;

			public LabelInfo(MsilWriter writer, Label label)
			{
				_writer = writer;
				_label = label;
			}

			public Label Label
			{
				get { return _label; }
			}

			public void Mark()
			{
				_writer._ilGenerator.MarkLabel(_label);
				IsMarked = true;
			}

			public bool IsReferenced { get; set; }

			public bool IsMarked { get; set; }
		}
	}
}
