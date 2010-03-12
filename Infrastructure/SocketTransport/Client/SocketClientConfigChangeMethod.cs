using System;

namespace MySpace.SocketTransport
{
	/// <summary>
	/// Represents a method that is called when the <see cref="SocketClientConfig"/> is changed
	/// or modified.
	/// </summary>
	/// <param name="newConfig">The new config, after the change.</param>
	public delegate void SocketClientConfigChangeMethod(SocketClientConfig newConfig);
	
}
