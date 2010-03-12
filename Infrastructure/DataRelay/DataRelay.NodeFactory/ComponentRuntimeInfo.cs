using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.DataRelay
{
	/// <summary>
	/// Provides a base class that inheritors can use to provide runtime information
	/// about a component.  
	/// </summary>
	/// <remarks>
	///		<para>The concept is that there may be information for diagnostics, reporting or 
	///		other needs that require information about a running component.  This information
	///		is retreived from sublasses of <see cref="ComponentRuntimeInfo"/>.
	/// </para>
	/// </remarks>
	[Serializable()]
	public abstract class ComponentRuntimeInfo
	{
		/// <summary>
		/// Initializes <see cref="ComponentRuntimeInfo"/> with the <see cref="componentName"/>.
		/// </summary>
		/// <param name="componentName"></param>
		public ComponentRuntimeInfo(string componentName)
		{
			ComponentName = componentName;
		}

		/// <summary>
		/// The component name.
		/// </summary>
		public string ComponentName;

		/// <summary>
		/// If the Runtime Info can be represented as a string, this method should return it. If there is not a meaningful string representation, then this returns null.
		/// </summary>
		/// <returns></returns>
		public virtual string GetRuntimeInfoAsString()
		{
			return null;
		}
		
	}
}
