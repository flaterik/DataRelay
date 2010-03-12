using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common.IO;

namespace MySpace.Common
{
	/// <summary>
	///		<para>Represents a method that creates a new instance of a class
	///		from a serialized stream.</para>
	/// </summary>
	/// <typeparam name="T">
	///		<para>The type of object to deserialize.</para>
	/// </typeparam>
	/// <param name="reader">
	///		<para>The reader providing access to the serialized stream.</para>
	/// </param>
	/// <returns>
	///		<para>An object of type <typeparamref name="T"/>, deserialized
	///		from the specified stream; never <see langword="null"/>.</para>
	/// </returns>
	public delegate T DeserializeMethod<T>(IPrimitiveReader reader);
}
