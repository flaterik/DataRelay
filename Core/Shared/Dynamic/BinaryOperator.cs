using System;
using System.Reflection.Emit;

namespace MySpace.Common.Dynamic
{
	/// <summary>
	///		<para>Provides enumerated values indicating the type of condition to check.</para>
	/// </summary>
	public sealed class BinaryOperator
	{
		/// <summary>
		/// The top value on the stack is <see langword="false"/>, <see langword="null"/>, or zero.
		/// </summary>
		public static BinaryOperator IsNull { get; private set; }
		/// <summary>
		/// The top value on the stack is <see langword="false"/>, <see langword="null"/>, or zero.
		/// </summary>
		public static BinaryOperator IsFalse { get; private set; }
		/// <summary>
		/// The top value on the stack is <see langword="true"/>, non-null, or non-zero.
		/// </summary>
		public static BinaryOperator IsTrue { get; private set; }
		/// <summary>
		/// The top two values on the stack are equal.
		/// </summary>
		public static BinaryOperator AreEqual { get; private set; }
		/// <summary>
		/// The top two values on the stack are not equal.
		/// </summary>
		public static BinaryOperator AreNotEqual { get; private set; }
		/// <summary>
		/// The stack transitional behavior, in sequential order, is:
		/// 1. value1 is pushed onto the stack.
		/// 2. value2 is pushed onto the stack.
		/// 3. value2 and value1 are popped from the stack; if value1 is greater than value2, the condition is <see langword="true"/>.
		/// </summary>
		public static BinaryOperator GreaterThan { get; private set; }
		/// <summary>
		/// The stack transitional behavior, in sequential order, is:
		/// 1. value1 is pushed onto the stack.
		/// 2. value2 is pushed onto the stack.
		/// 3. value2 and value1 are popped from the stack; if value1 is less than value2, the condition is <see langword="true"/>.
		/// </summary>
		public static BinaryOperator LessThan { get; private set; }
		/// <summary>
		/// The stack transitional behavior, in sequential order, is:
		/// 1. value1 is pushed onto the stack.
		/// 2. value2 is pushed onto the stack.
		/// 3. value2 and value1 are popped from the stack; if value1 is greater than or equal to value2, the condition is <see langword="true"/>.
		/// </summary>
		public static BinaryOperator GreaterThanOrEqualTo { get; private set; }
		/// <summary>
		/// The stack transitional behavior, in sequential order, is:
		/// 1. value1 is pushed onto the stack.
		/// 2. value2 is pushed onto the stack.
		/// 3. value2 and value1 are popped from the stack; if value1 is less than or equal to value2, the condition is <see langword="true"/>.
		/// </summary>
		public static BinaryOperator LessThanOrEqualTo { get; private set; }

		private static void SetOpposites(BinaryOperator op1, BinaryOperator op2)
		{
			op1._opposite = op2;
			op2._opposite = op1;
		}

		static BinaryOperator()
		{
			// IsNull, IsFalse, IsTrue
			var op1 = new BinaryOperator
			{
				_branchWriter = (w, l) => w.Emit(OpCodes.Brtrue, l),
				_compareWriter = w => { },
				ArgumentCount = 1
			};
			var op2 = new BinaryOperator
			{
				_branchWriter = (w, l) => w.Emit(OpCodes.Brfalse, l),
				_compareWriter = w => w.Emit(OpCodes.Not),
				ArgumentCount = 1
			};

			SetOpposites(op1, op2);

			IsTrue = op1;
			IsFalse = IsNull = op2;

			// AreEqual, AreNotEqual
			op1 = new BinaryOperator
			{
				_branchWriter = (wde, l) => wde.Emit(OpCodes.Beq, l),
				_compareWriter = w => w.Emit(OpCodes.Ceq),
				ArgumentCount = 2
			};
			op2 = new BinaryOperator
			{
				_branchWriter = (wde, l) => wde.Emit(OpCodes.Bne_Un, l),
				_compareWriter = w => { w.Emit(OpCodes.Ceq); w.Emit(OpCodes.Not); },
				ArgumentCount = 2
			};

			SetOpposites(op1, op2);

			AreEqual = op1;
			AreNotEqual = op2;

			// GreaterThan, LessThanOrEqualTo
			op1 = new BinaryOperator
			{
				_branchWriter = (wde, l) => wde.Emit(OpCodes.Bgt, l),
				_compareWriter = w => w.Emit(OpCodes.Cgt),
				ArgumentCount = 2
			};
			op2 = new BinaryOperator
			{
				_branchWriter = (wde, l) => wde.Emit(OpCodes.Ble, l),
				_compareWriter = w => w.Emit(OpCodes.Clt),
				ArgumentCount = 2
			};

			SetOpposites(op1, op2);

			GreaterThan = op1;
			LessThanOrEqualTo = op2;

			// GreaterThanOrEqualTo, LessThan
			op1 = new BinaryOperator
			{
				_branchWriter = (wde, l) => wde.Emit(OpCodes.Bge, l),
				_compareWriter = w => { w.Emit(OpCodes.Clt); w.Emit(OpCodes.Not); },
				ArgumentCount = 2
			};
			op2 = new BinaryOperator
			{
				_branchWriter = (wde, l) => wde.Emit(OpCodes.Blt, l),
				_compareWriter = w => { w.Emit(OpCodes.Cgt); w.Emit(OpCodes.Not); },
				ArgumentCount = 2
			};

			SetOpposites(op1, op2);

			GreaterThanOrEqualTo = op1;
			LessThan = op2;
		}

		private Action<ICilWriter, ILabel> _branchWriter;
		private Action<ICilWriter> _compareWriter;
		private BinaryOperator _opposite;

		private BinaryOperator() { }

		/// <summary>
		/// Writes a compare instruction that leaves an int32 on the eval stack.
		/// </summary>
		/// <param name="writer">The writer.</param>
		public void WriteCompare(ICilWriter writer)
		{
			_compareWriter(writer);
		}

		/// <summary>
		/// Writes a conditional branch instruction to <paramref name="target"/>.
		/// The instruction will branch if the operator evaluates to <see langword="true"/>.
		/// </summary>
		/// <param name="writer">The writer to write to.</param>
		/// <param name="target">The label to branch to.</param>
		public void WriteBranch(ICilWriter writer, ILabel target)
		{
			_branchWriter(writer, target);
		}

		/// <summary>
		/// Gets the number of arguments that will be popped off of the evaluation stack to perform the operation.
		/// </summary>
		/// <value>The number of arguments that will be popped off of the evaluation stack to perform the operation.</value>
		public int ArgumentCount { get; private set; }

		/// <summary>
		/// Returns the opposite of the operation. e.g. If this instance represents (less than) then
		/// the <see cref="Negate"/> will return (greater than or equal to).
		/// </summary>
		/// <returns>The opposite of the operation such that the operation will have the exact opposite result.</returns>
		public BinaryOperator Negate()
		{
			return _opposite;
		}
	}
}
