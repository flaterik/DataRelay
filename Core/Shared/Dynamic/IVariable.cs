using System;
using System.Reflection.Emit;

namespace MySpace.Common.Dynamic
{
	/// <summary>
	/// 	<para>Encapsulates a parameter or local variable that belongs to a <see cref="ICilWriter"/> instance.</para>
	/// </summary>
	public interface IVariable
	{
		/// <summary>
		/// 	<para>Gets the type of the variable.</para>
		/// </summary>
		/// <value>
		/// 	<para>The type of the variable.</para>
		/// </value>
		Type Type { get; }

		/// <summary>
		/// 	<para>Gets a value indicating whether the variable is pinned.</para>
		/// </summary>
		/// <value>
		/// 	<para><see langword="true"/> if the variable is pinned; otherwise, <see langword="false"/>.</para>
		/// </value>
		bool IsPinned { get; }

		/// <summary>
		/// 	<para>Gets the name of the variable, or <see langword="null"/> if not available.</para>
		/// </summary>
		/// <value>
		/// 	<para>The name of the variable; <see langword="null"/> if not available.</para>
		/// </value>
		string Name { get; }

        /// <summary>
        ///     <para>Initializes or resets the value of the variable to zero using the <see cref="OpCodes.Initobj"/> opcode.</para>
        ///     <para>This operation leaves the evaluation stack in the same state as before.</para>
        /// </summary>
        void Initialize();

		/// <summary>
		/// 	<para>Emits an instruction that loads the value of the variable onto the evaluation stack.</para>
		/// </summary>
		void Load(LoadOptions options);

		/// <summary>
		///		<para>Emits the necessary instructions to store to this variable.</para>
		/// </summary>
		/// <exception cref="InvalidOperationException">
		///		<para><see cref="CanStore"/> is <see langword="false"/>.</para>
		/// </exception>
		void Store();

		/// <summary>
		/// Gets a value indicating whether this instance can use the <see cref="Store"/> method.
		/// </summary>
		/// <value><c>true</c> if this instance can use the <see cref="Store"/> method; otherwise, <c>false</c>.</value>
		bool CanStore { get; }

		/// <summary>
		///		<para>Emits the start of the instruction block for an assignment to this variable.</para>
		///		<para>The value to assign should be loaded between calls to <see cref="BeginAssign"/> and <see cref="EndAssign"/>.</para>
		/// </summary>
		void BeginAssign();

		/// <summary>
		///		<para>Emits the end of the instruction block for an assignment to this variable.</para>
		///		<para>The value to assign should be loaded between calls to <see cref="BeginAssign"/> and <see cref="EndAssign"/>.</para>
		/// </summary>
		void EndAssign();
	}
}
