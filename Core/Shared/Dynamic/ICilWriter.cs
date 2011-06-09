using System;
using System.Reflection;
using System.Reflection.Emit;

namespace MySpace.Common.Dynamic
{
	/// <summary>
	/// 	<para>Encapsulates methods for emitting MSIL instructions into a method.</para>
	/// </summary>
	public interface ICilWriter : IDisposable
	{
		/// <summary>
		/// Gets the method header.
		/// </summary>
		/// <value>The method header.</value>
		MethodHeader MethodHeader { get; }

		/// <summary>
		/// Gets an <see cref="IVariable"/> representation of the parameter at the specified index.
		/// </summary>
		/// <param name="index">The index of the parameter to get.</param>
		/// <returns>An <see cref="IVariable"/> representation of the parameter at the specified index.</returns>
		IVariable GetParameter(int index);

		/// <summary>
		/// Defines a local variable and returns an <see cref="IVariable"/> representation of it.
		/// </summary>
		/// <param name="type">The type of local to define.</param>
		/// <param name="isPinned"><see langword="true"/> to pin the local variable in memory; <see langword="false"/> otherwise.</param>
		/// <param name="name">The name of the local. <see langword="null"/> if not specified.</param>
		/// <returns>
		///	<para>An <see cref="IVariable"/> representation of the defined local.</para>
		/// </returns>
		IVariable DefineLocal(Type type, bool isPinned, string name);

		/// <summary>
		/// Defines a label that may be referenced and marked.
		/// </summary>
		/// <returns>
		///	<para>An <see cref="ILabel"/> instance that may be referenced and marked.</para>
		/// </returns>
		ILabel DefineLabel();

		/// <summary>
		/// Begins a try block; exceptions thrown by instructions written within the try block will be caught.
		/// </summary>
		void BeginTry();

		/// <summary>
		/// Begins a catch block and pushes the exception onto the evaluation stack. This ends the existing try block.
		/// </summary>
		/// <param name="exceptionType">
		///	<para>The type of exception to catch. Use <see cref="Object"/> if you intend to pop the exception
		///	off of the evaluation stack and leave it unused.</para>
		/// </param>
		/// <exception cref="InvalidOperationException">
		///	<para>This call does not immediately follow a try block.</para>
		/// </exception>
		void BeginCatch(Type exceptionType);

		/// <summary>
		/// Begins a finally block. This ends an existing try or catch block.
		/// </summary>
		/// <exception cref="InvalidOperationException">
		///	<para>This call does not immediately follow a try or catch block.</para>
		/// </exception>
		void BeginFinally();

		/// <summary>
		/// Ends a try/catch/finally sequence.
		/// </summary>
		/// <exception cref="InvalidOperationException">
		///	<para>This call does not immediately follow a catch or finally block.</para>
		/// </exception>
		void EndTryCatchFinally();

		/// <summary>
		/// 	<para>Emits the specified <paramref name="opCode"/> with no operand.</para>
		/// </summary>
		/// <param name="opCode">The op-code to emit.</param>
		void Emit(OpCode opCode);

		/// <summary>
		/// 	<para>Emits the specified <paramref name="opCode"/> with the specified <param name="operand" />.</para>
		/// </summary>
		/// <param name="opCode">The op-code to emit.</param>
		/// <param name="operand">The operand.</param>
		void Emit(OpCode opCode, Type operand);

		/// <summary>
		/// 	<para>Emits the specified <paramref name="opCode"/> with the specified <param name="operand" />.</para>
		/// </summary>
		/// <param name="opCode">The op-code to emit.</param>
		/// <param name="operand">The operand.</param>
		void Emit(OpCode opCode, MethodInfo operand);

		/// <summary>
		/// 	<para>Emits the specified <paramref name="opCode"/> with the specified <param name="operand" />.</para>
		/// </summary>
		/// <param name="opCode">The op-code to emit.</param>
		/// <param name="operand">The operand.</param>
		void Emit(OpCode opCode, FieldInfo operand);

		/// <summary>
		/// 	<para>Emits the specified <paramref name="opCode"/> with the specified <param name="operand" />.</para>
		/// </summary>
		/// <param name="opCode">The op-code to emit.</param>
		/// <param name="operand">The operand.</param>
		void Emit(OpCode opCode, ConstructorInfo operand);

		/// <summary>
		/// 	<para>Emits the specified <paramref name="opCode"/> with the specified <param name="operand" />.</para>
		/// </summary>
		/// <param name="opCode">The op-code to emit.</param>
		/// <param name="operand">The operand.</param>
		void Emit(OpCode opCode, ILabel operand);

		/// <summary>
		/// 	<para>Emits the specified <paramref name="opCode"/> with the specified <param name="operand" />.</para>
		/// </summary>
		/// <param name="opCode">The op-code to emit.</param>
		/// <param name="operand">The operand.</param>
		void Emit(OpCode opCode, string operand);

		/// <summary>
		/// 	<para>Emits the specified <paramref name="opCode"/> with the specified <param name="operand" />.</para>
		/// </summary>
		/// <param name="opCode">The op-code to emit.</param>
		/// <param name="operand">The operand.</param>
		void Emit(OpCode opCode, int operand);

		/// <summary>
		/// 	<para>Emits the specified <paramref name="opCode"/> with the specified <param name="operand" />.</para>
		/// </summary>
		/// <param name="opCode">The op-code to emit.</param>
		/// <param name="operand">The operand.</param>
		void Emit(OpCode opCode, long operand);

		/// <summary>
		/// 	<para>Emits the specified <paramref name="opCode"/> with the specified <param name="operand" />.</para>
		/// </summary>
		/// <param name="opCode">The op-code to emit.</param>
		/// <param name="operand">The operand.</param>
		void Emit(OpCode opCode, byte operand);

		/// <summary>
		/// 	<para>Emits the specified <paramref name="opCode"/> with the specified <param name="operand" />.</para>
		/// </summary>
		/// <param name="opCode">The op-code to emit.</param>
		/// <param name="operand">The operand.</param>
		void Emit(OpCode opCode, float operand);

		/// <summary>
		/// 	<para>Emits the specified <paramref name="opCode"/> with the specified <param name="operand" />.</para>
		/// </summary>
		/// <param name="opCode">The op-code to emit.</param>
		/// <param name="operand">The operand.</param>
		void Emit(OpCode opCode, double operand);
	}
}
