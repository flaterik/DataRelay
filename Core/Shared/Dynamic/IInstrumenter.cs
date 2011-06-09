using System;
using Mono.Cecil;

namespace MySpace.Common.Dynamic
{
	/// <summary>
	/// 	<para>Encapsulates a type that instruments <see cref="AssemblyDefinition"/>'s.</para>
	/// </summary>
	public interface IInstrumenter
	{
		/// <summary>
		/// 	<para>Instruments the specified assembly definition.</para>
		/// </summary>
		/// <param name="assemblyDefinition">The assembly definition.</param>
		void Instrument(AssemblyDefinition assemblyDefinition);
	}
}
