using System;

namespace MySpace.Common.Dynamic
{
	/// <summary>
	/// 	<para>A simple, general-purpose implementation of <see cref="IVariable"/>.
	/// 	This class is intended to be used internally by <see cref="ICilWriter"/> implementations.</para>
	/// </summary>
	internal class SimpleParameter : SimpleVariable
	{
		private static Type GetElementType(Type type)
		{
			return type.IsByRef ? type.GetElementType() : type;
		}

		private static string GetName(string name, int index)
		{
			return string.IsNullOrEmpty(name)
				? "(parameter[" + index + "])"
				: name;
		}

		private readonly Type _originalType;

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="SimpleParameter"/> class.</para>
		/// </summary>
		/// <param name="writer">The writer that the parameter belongs to.</param>
		/// <param name="index">The index of the parameter.</param>
		/// <param name="type">The parameter's type.</param>
		/// <param name="name">The parameter's name, or <see langword="null"/> if unavailable.</param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="writer"/> is <see langword="null"/>.</para>
		///	<para>- or -</para>
		///	<para><paramref name="type"/> is <see langword="null"/>.</para>
		/// </exception>
		public SimpleParameter(ICilWriter writer, int index, Type type, string name)
			: base(writer, index, GetElementType(type), GetName(name, index), false)
		{
			_originalType = type;
		}

		/// <summary>
		/// Emits an instruction that loads the value of the variable onto the evaluation stack.
		/// </summary>
		public override void Load(LoadOptions options)
		{
			if (_originalType.IsByRef)
			{
				Writer.EmitLdarg(Index);
				if (!options.ShouldLoadAddress(Type))
				{
					Writer.EmitLdobj(Type);
				}
			}
			else
			{
				if (options.ShouldLoadAddress(_originalType))
				{
					Writer.EmitLdarga(Index);
				}
				else
				{
					Writer.EmitLdarg(Index);
				}
			}
		}

		/// <summary>
		/// Emits the necessary instructions to store to this variable.
		/// </summary>
		/// <exception cref="InvalidOperationException"><see cref="CanStore"/> is <see langword="false"/>.</exception>
		public override void Store()
		{
			if (!CanStore)
			{
				throw new InvalidOperationException("Can't store when Type is a ByRef type.");
			}
			Writer.EmitStarg(Index);
		}

		/// <summary>
		/// Gets a value indicating whether this instance can use the <see cref="Store"/> method.
		/// </summary>
		/// <value>
		/// 	<c>true</c> if this instance can use the <see cref="Store"/> method; otherwise, <c>false</c>.
		/// </value>
		public override bool CanStore
		{
			get { return !_originalType.IsByRef; }
		}

		/// <summary>
		/// 	<para>Emits the start of the instruction block for an assignment to this variable.</para>
		/// 	<para>The value to assign should be loaded between calls to <see cref="BeginAssign"/> and <see cref="EndAssign"/>.</para>
		/// </summary>
		public override void BeginAssign()
		{
			if (_originalType.IsByRef)
			{
				Writer.EmitLdarg(Index);
			}
		}

		/// <summary>
		/// 	<para>Emits the end of the instruction block for an assignment to this variable.</para>
		/// 	<para>The value to assign should be loaded between calls to <see cref="BeginAssign"/> and <see cref="EndAssign"/>.</para>
		/// </summary>
		public override void EndAssign()
		{
			if (_originalType.IsByRef)
			{
				Writer.EmitStobj(Type);
			}
			else
			{
				Writer.EmitStarg(Index);
			}
		}
	}
}
