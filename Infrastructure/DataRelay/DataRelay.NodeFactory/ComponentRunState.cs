using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.DataRelay
{
	/// <summary>
	/// Provides a means for a component to persist or load state information.  Memento pattern.
	/// </summary>
	/// <remarks>
	///		<para>One example of using <see cref="ComponentRunState"/> is when an
	///		assembly is reloaded, the container for the component asks for the run state,
	///		unloads the assembly, reloads a new assembly and initializes the new instance with
	///		<see cref="ComponentRunState"/> from the first assembly.
	///		</para>
	/// </remarks>
	[Serializable()]
	public sealed class ComponentRunState
	{
		private ComponentRunState() { }
		
		/// <summary>
		/// Initializes the <see cref="ComponentRunState"/> instance with a <paramref name="componentName"/>.
		/// </summary>
		/// <param name="componentName">The name of the component</param>
		public ComponentRunState(string componentName)
		{
			ComponentName = componentName;
		}
		
		/// <summary>
		/// The name of the component.
		/// </summary>
		public string ComponentName;
		
		/// <summary>
		/// The state information.
		/// </summary>
		public byte[] SerializedState;
	}
}
