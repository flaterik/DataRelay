using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySpace.Storage
{
	/// <summary>
	/// Specifies the scope at which the underlying storage mechanism lives.
	/// </summary>
	/// <remarks>For example, if <see cref="AppDomain"/> is specified, then
	/// a write in one instance of <see cref="IStorage"/> would be read in
	/// all other instances in the same app domain.</remarks>
	public enum ExecutionScope
	{
		/// <summary>
		/// One underlying storage for each instance.
		/// </summary>
		Instance = 0,

		/// <summary>
		/// One underlying storage for the each app domain.
		/// </summary>
		AppDomain = 1,

		/// <summary>
		/// One underlying storage for each process.
		/// </summary>
		Process = 2,

		/// <summary>
		/// One underlying storage for each machine.
		/// </summary>
		Machine = 3,

		/// <summary>
		/// One underlying storage for each thread.
		/// </summary>
		Thread = 4,

		/// <summary>
		/// Some underlying storage scope that doesn't correspond to any
		/// of the other values of <see cref="ExecutionScope"/>.
		/// </summary>
		Other = 5
	}
}
