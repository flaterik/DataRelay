using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MethodBody = Mono.Cecil.Cil.MethodBody;

namespace MySpace.Common.Dynamic
{
	/// <summary>
	/// 	<para>An implementation of <see cref="ICilWriter"/> that uses the Mono.Cecil
	/// 	reflection library to write into <see cref="MethodBody"/> instances.</para>
	/// </summary>
	internal class CecilCilWriter : ICilWriter
	{
		private readonly MethodHeader _methodHeader;
		private readonly ModuleDefinition _module;
		private readonly MethodBody _body;
		private readonly Factory<ArrayTypeKey, ArrayType> _arrayTypes;
		private readonly Stack<ExceptionStructure> _exceptionStructureStack = new Stack<ExceptionStructure>();
		private TypeDefinition _arrayTypeDefinition;
		private bool _isDisposed;

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="CecilCilWriter"/> class.</para>
		/// </summary>
		/// <param name="methodHeader">The header of the method to write to.</param>
		/// <param name="methodBody">The method body to write to.</param>
		public CecilCilWriter(MethodHeader methodHeader, MethodBody methodBody)
		{
			_methodHeader = methodHeader;
			_body = methodBody;
			_module = _body.Method.DeclaringType.Module;

			_arrayTypes = Algorithm.LazyIndexer<ArrayTypeKey, ArrayType>(
				key => (ArrayType)_module.Import(new ArrayType(key.ElementType, key.Rank)));
		}

		internal TypeDefinition ArrayTypeDefinition
		{
			get
			{
				if (_arrayTypeDefinition == null)
				{
					_arrayTypeDefinition = _module.Import(typeof(Array)).Resolve();
				}
				return _arrayTypeDefinition;
			}
		}

		internal ModuleDefinition Module
		{
			[DebuggerStepThrough]
			get { return _module; }
		}

		MethodHeader ICilWriter.MethodHeader
		{
			[DebuggerStepThrough]
			get { return _methodHeader; }
		}

		IVariable ICilWriter.GetParameter(int index)
		{
			Type parameterType;
			string parameterName;

			if (_body.Method.IsStatic)
			{
				parameterType = _methodHeader.ParameterTypes[index];
				parameterName = _body.Method.Parameters[index].Name;
			}
			else if (index > 0)
			{
				parameterType = _methodHeader.ParameterTypes[index - 1];
				parameterName = _body.Method.Parameters[index -1].Name;
			}
			else
			{
				return new SimpleParameter(this, 0, _methodHeader.DeclaringType, "this");
			}

			return new SimpleParameter(this, index, parameterType, parameterName);
		}

		ILabel ICilWriter.DefineLabel()
		{
			if (_isDisposed) throw new ObjectDisposedException(GetType().Name);

			return new Label(this);
		}

		IVariable ICilWriter.DefineLocal(Type type, bool isPinned, string name)
		{
			if (_isDisposed) throw new ObjectDisposedException(GetType().Name);

			if (isPinned)
			{
				throw new InvalidOperationException("Pinned local variables are not yet supported by CecilCilWriter");
			}

			var variableDef = new VariableDefinition(name, _body.Variables.Count, _body.Method, _module.Import(type));
			_body.Variables.Add(variableDef);
			return new SimpleLocal(this, variableDef.Index, type, name, isPinned);
		}

		void ICilWriter.BeginTry()
		{
			_exceptionStructureStack.Push(ExceptionStructure.BeginTry(this));
		}

		void ICilWriter.BeginCatch(Type exceptionType)
		{
			if (_exceptionStructureStack.Count == 0)
			{
				throw new InvalidOperationException("Catch without try.");
			}

			_exceptionStructureStack.Peek().BeginCatch(exceptionType);
		}

		void ICilWriter.BeginFinally()
		{
			if (_exceptionStructureStack.Count == 0)
			{
				throw new InvalidOperationException("Finally without try.");
			}

			_exceptionStructureStack.Peek().BeginFinally();
		}

		void ICilWriter.EndTryCatchFinally()
		{
			if (_exceptionStructureStack.Count == 0)
			{
				throw new InvalidOperationException("Can't end a try/catch/finally block that hasn't started.");
			}

			_exceptionStructureStack.Pop().End();
		}

		void ICilWriter.Emit(System.Reflection.Emit.OpCode opCode, Type operand)
		{
			if (_isDisposed) throw new ObjectDisposedException(GetType().Name);

			Append(_body.CilWorker.Create(
				CecilTranslator.Translate(opCode),
				_module.Import(operand)));
		}

		void ICilWriter.Emit(System.Reflection.Emit.OpCode opCode, MethodInfo operand)
		{
			Append(_body.CilWorker.Create(
				CecilTranslator.Translate(opCode),
				_module.Import(operand)));
		}

		void ICilWriter.Emit(System.Reflection.Emit.OpCode opCode, ConstructorInfo operand)
		{
			Append(_body.CilWorker.Create(
				CecilTranslator.Translate(opCode),
				_module.Import(operand)));
		}

		void ICilWriter.Emit(System.Reflection.Emit.OpCode opCode, FieldInfo operand)
		{
			Append(_body.CilWorker.Create(
				CecilTranslator.Translate(opCode),
				_module.Import(operand)));
		}

		void ICilWriter.Emit(System.Reflection.Emit.OpCode opCode, ILabel operand)
		{
			if (_isDisposed) throw new ObjectDisposedException(GetType().Name);

			var label = (Label)operand;

			var instruction = _body.CilWorker.Create(CecilTranslator.Translate(opCode), label.TargetInstruction);
			label.Referencers.Add(instruction);
			_body.CilWorker.Append(instruction);
		}

		void ICilWriter.Emit(System.Reflection.Emit.OpCode opCode, string operand)
		{
			if (_isDisposed) throw new ObjectDisposedException(GetType().Name);

			Append(_body.CilWorker.Create(
				CecilTranslator.Translate(opCode),
				operand));
		}

		void ICilWriter.Emit(System.Reflection.Emit.OpCode opCode, int operand)
		{
			if (_isDisposed) throw new ObjectDisposedException(GetType().Name);

			var code = CecilTranslator.Translate(opCode);

			switch (code.OperandType)
			{
				case OperandType.InlineVar:
					Append(_body.CilWorker.Create(code, _body.Variables[operand]));
					break;
				case OperandType.InlineParam:
					Append(_body.CilWorker.Create(code, GetParameter(operand)));
					break;
				default:
					Append(_body.CilWorker.Create(code, operand));
					break;
			}
		}

		void ICilWriter.Emit(System.Reflection.Emit.OpCode opCode, long operand)
		{
			if (_isDisposed) throw new ObjectDisposedException(GetType().Name);

			var code = CecilTranslator.Translate(opCode);

			if (code.OperandType == OperandType.InlineVar || code.OperandType == OperandType.InlineParam)
			{
				((ICilWriter)this).Emit(opCode, checked((int)operand));
			}

			Append(_body.CilWorker.Create(code, operand));
		}

		void ICilWriter.Emit(System.Reflection.Emit.OpCode opCode, float operand)
		{
			if (_isDisposed) throw new ObjectDisposedException(GetType().Name);

			var code = CecilTranslator.Translate(opCode);

			Append(_body.CilWorker.Create(code, operand));
		}

		void ICilWriter.Emit(System.Reflection.Emit.OpCode opCode, double operand)
		{
			if (_isDisposed) throw new ObjectDisposedException(GetType().Name);

			var code = CecilTranslator.Translate(opCode);

			Append(_body.CilWorker.Create(code, operand));
		}

		private ParameterDefinition GetParameter(int index)
		{
			if (_body.Method.HasThis)
			{
				if (index == 0)
				{
					return _body.Method.This;
				}
				--index;
			}
			return _body.Method.Parameters[index];
		}

		void ICilWriter.Emit(System.Reflection.Emit.OpCode opCode, byte operand)
		{
			if (_isDisposed) throw new ObjectDisposedException(GetType().Name);

			var code = CecilTranslator.Translate(opCode);
			switch (code.OperandType)
			{
				case OperandType.ShortInlineVar:
					Append(_body.CilWorker.Create(code, _body.Variables[operand]));
					break;
				case OperandType.ShortInlineParam:
					Append(_body.CilWorker.Create(code, GetParameter(operand)));
					break;
				default:
					// work-around: for some reason Cecil doesn't like this particular op-code
					// so we'll use an equivalent one instead
					if (code == OpCodes.Ldc_I4_S)
					{
						Append(_body.CilWorker.Create(OpCodes.Ldc_I4, (int)operand));
					}
					else
					{
						Append(_body.CilWorker.Create(code, operand));
					}
					break;
			}
		}

		void ICilWriter.Emit(System.Reflection.Emit.OpCode opCode)
		{
			if (_isDisposed) throw new ObjectDisposedException(GetType().Name);

			Append(_body.CilWorker.Create(CecilTranslator.Translate(opCode)));
		}

		private void Append(Instruction instruction)
		{
			if (instruction.Operand != null)
			{
				if (instruction.Operand is TypeReference)
				{
					instruction.Operand = _module.Import((TypeReference)instruction.Operand);
				}
				else if (instruction.Operand is MemberReference)
				{
					var member = (MemberReference)instruction.Operand;
					if (member.DeclaringType.Module != _module)
					{
						if (member is FieldReference)
						{
							instruction.Operand = _module.Import((FieldReference)member);
						}
						else if (member is MethodReference)
						{
							instruction.Operand = _module.Import((MethodReference)member);
						}
						else
						{
							throw new NotSupportedException("Operands of type " + instruction.Operand.GetType() + " are not supported");
						}
					}
				}
			}
			_body.CilWorker.Append(instruction);
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			//_body.Optimize();
			_isDisposed = true;
		}

		private class Label : ILabel
		{
			private readonly CecilCilWriter _owner;

			public Label(CecilCilWriter owner)
			{
				_owner = owner;
				Referencers = new List<Instruction>();
				TargetInstruction = owner._body.CilWorker.Create(OpCodes.Nop);
			}

			public List<Instruction> Referencers { get; private set; }

			public Instruction TargetInstruction { get; private set; }

			public bool IsMarked { get; private set; }

			public bool IsReferenced
			{
				get { return Referencers.Count > 0; }
			}

			void ILabel.Mark()
			{
				if (IsMarked) throw new InvalidOperationException("This label is already marked.");

				_owner.Append(TargetInstruction);
				IsMarked = true;
			}
		}

		private class ArrayTypeKey : IEquatable<ArrayTypeKey>
		{
			public ArrayTypeKey(TypeReference elementType, int rank)
			{
				ElementType = elementType;
				Rank = rank;
			}

			public TypeReference ElementType { get; set; }
			public int Rank { get; set; }

			public override bool Equals(object obj)
			{
				return ((IEquatable<ArrayTypeKey>)this).Equals((ArrayTypeKey)obj);
			}

			public override int GetHashCode()
			{
				return ElementType.FullName.GetHashCode() ^ Rank;
			}

			#region IEquatable<ArrayKey> Members

			public bool Equals(ArrayTypeKey other)
			{
				return ElementType.FullName == other.ElementType.FullName && Rank == other.Rank;
			}

			#endregion
		}

		private class ExceptionStructure
		{
			private abstract class Block
			{
				protected Block(ExceptionStructure owner, Instruction start)
				{
					Owner = owner;
					Start = start;
				}

				protected ExceptionStructure Owner { get; private set; }
				public Instruction Start { get; private set; }
				public Instruction End { get; private set; }

				public virtual void Close()
				{
					End = Owner.LastInstruction;
				}

				public bool IsClosed
				{
					get { return End != null; }
				}
			}

			private class TryBlock : Block
			{
				public TryBlock(ExceptionStructure owner, Instruction start)
					: base(owner, start)
				{
				}

				public override void Close()
				{
					var cilWriter = (ICilWriter)Owner._owner;
					cilWriter.Emit(System.Reflection.Emit.OpCodes.Leave, Owner._endLabel);
					cilWriter.Emit(System.Reflection.Emit.OpCodes.Nop);
					base.Close();
				}
			}

			private class CatchBlock : Block
			{
				public CatchBlock(ExceptionStructure owner, Instruction start, Type exceptionType)
					: base(owner, start)
				{
					ExceptionType = exceptionType;
				}

				public Type ExceptionType { get; private set; }

				public override void Close()
				{
					var cilWriter = (ICilWriter)Owner._owner;
					cilWriter.Emit(System.Reflection.Emit.OpCodes.Leave, Owner._endLabel);
					cilWriter.Emit(System.Reflection.Emit.OpCodes.Nop);
					base.Close();
				}
			}

			private class FinallyBlock : Block
			{
				public FinallyBlock(ExceptionStructure owner, Instruction start)
					: base(owner, start)
				{
				}

				public override void Close()
				{
					var cilWriter = (ICilWriter)Owner._owner;
					cilWriter.Emit(System.Reflection.Emit.OpCodes.Endfinally);
					cilWriter.Emit(System.Reflection.Emit.OpCodes.Nop);
					base.Close();
				}
			}

			public static ExceptionStructure BeginTry(CecilCilWriter owner)
			{
				return new ExceptionStructure(owner);
			}

			private readonly CecilCilWriter _owner;
			private readonly ILabel _endLabel;
			private Block _tryBlock;
			private readonly List<CatchBlock> _catchBlocks = new List<CatchBlock>();
			private Block _finallyBlock;
			private bool _ended;

			private ExceptionStructure(CecilCilWriter owner)
			{
				_owner = owner;
				_endLabel = ((ICilWriter)owner).DefineLabel();
				AppendNop();
				_tryBlock = new TryBlock(this, LastInstruction);
			}

			public void BeginCatch(Type exceptionType)
			{
				if (_ended)
				{
					throw new InvalidOperationException("This try... block has already ended.");
				}
				if (_finallyBlock != null)
				{
					throw new InvalidOperationException("Can't begin a catch block after the finally block has already ended.");
				}

				CloseLastBlock();
				_catchBlocks.Add(new CatchBlock(this, LastInstruction, exceptionType));
			}

			public void BeginFinally()
			{
				if (_ended)
				{
					throw new InvalidOperationException("This try... block has already ended.");
				}
				if (_finallyBlock != null)
				{
					throw new InvalidOperationException("Can't begin more than one finally block.");
				}

				CloseLastBlock();
				_finallyBlock = new FinallyBlock(this, LastInstruction);
			}

			public void End()
			{
				if (_ended)
				{
					throw new InvalidOperationException("This try... block has already ended.");
				}

				if (_catchBlocks.Count == 0 && _finallyBlock == null)
				{
					throw new InvalidOperationException("Can't have a try block with no finally or catch.");
				}

				_ended = true;
				CloseLastBlock();

				Instruction lastEndInstruction = _tryBlock.End;

				foreach (var catchBlock in _catchBlocks)
				{
					var handler = new ExceptionHandler(ExceptionHandlerType.Catch)
					{
						TryStart = _tryBlock.Start,
						TryEnd = _tryBlock.End,
						HandlerStart = catchBlock.Start,
						HandlerEnd = catchBlock.End,
						CatchType = _owner._module.Import(catchBlock.ExceptionType)
					};
					_owner._body.ExceptionHandlers.Add(handler);
					lastEndInstruction = catchBlock.End;
				}

				if (_finallyBlock != null)
				{
					var handler = new ExceptionHandler(ExceptionHandlerType.Finally)
					{
						TryStart = _tryBlock.Start,
						TryEnd = lastEndInstruction,
						HandlerStart = _finallyBlock.Start,
						HandlerEnd = _finallyBlock.End
					};
					_owner._body.ExceptionHandlers.Add(handler);
				}

				_endLabel.Mark();
			}

			private void CloseLastBlock()
			{
				if (!_tryBlock.IsClosed)
				{
					_tryBlock.Close();
					return;
				}

				foreach (var catchBlock in _catchBlocks)
				{
					if (!catchBlock.IsClosed)
					{
						catchBlock.Close();
						return;
					}
				}

				if (!_finallyBlock.IsClosed)
				{
					_finallyBlock.Close();
					return;
				}

				throw new InvalidOperationException("Unexpected state.");
			}

			private Instruction LastInstruction
			{
				get
				{
					if (_owner._body.Instructions.Count == 0) return null;
					return _owner._body.Instructions[_owner._body.Instructions.Count - 1];
				}
			}

			private void AppendNop()
			{
				_owner.Append(_owner._body.CilWorker.Create(OpCodes.Nop));
			}
		}
	}
}
