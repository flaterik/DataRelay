using System;

namespace MySpace.SocketTransport
{
	/// <summary>
	/// 	<para>Encapsulates the results of a completable operation.</para>
	/// </summary>
	internal interface ICompletion
	{
		/// <summary>
		/// 	<para>Completes the operation.</para>
		/// </summary>
		void Complete();
	}
}
