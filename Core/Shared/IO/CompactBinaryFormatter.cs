/*

Compact Serialization Framework
Copyright (C) 2006 Shoaib Ali

This library is free software; you can redistribute it and/or
modify it under the terms of the GNU Lesser General Public
License as published by the Free Software Foundation; either
version 2.1 of the License, or (at your option) any later version.

This library is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public
License along with this library; if not, write to the Free Software
Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA

for bug-reports and suggestions alleey@gmail.com

*/
using System;
using System.Collections;
using System.IO;

using MySpace.Common.CompactSerialization.IO;
using MySpace.Common.CompactSerialization.Surrogates;

namespace MySpace.Common.CompactSerialization.Formatters
{
	/// <summary>
	/// Serializes and deserializes an object, or an entire graph of connected objects, in binary format.
	/// Uses the compact serialization framework to achieve better stream size and cpu time utlization.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The basic idea behind space conservation is that every 'known type' is assigned a 2-byte 
	/// type handle by the system. Native .NET serialization stores the complete type information
	/// with serialized object data, which includes assembly, version and tokens etc. Instead of storing
	/// such information only a type handle is stored, which lets the system uniquely identify 'known types'.
	/// 
	/// A known type is a type that is registered with the <see cref="TypeSurrogateProvider"/>. Moreover
	/// surrogate types take care of serializing only the required information. Information related to fields
	/// and attributes is not stored as in case of native serialization.
	/// </para>
	/// <para>
	/// From performance's perspective reflection is avoided by using surrogates for types. A type surrogate
	/// is intimate with the internals of a type and therefore does not need reflection to guess 
	/// object schema.
	/// </para>
	/// For types that are not known to the system the formatter reverts to the default .NET 
	/// serialization scheme.
	/// </remarks>
	public class CompactBinaryFormatter
	{
		/// <summary>
		/// Serializes an object and returns its binary representation.
		/// </summary>
		/// <param name="graph">object to serialize</param>
		/// <returns>binary form of object</returns>
		static public byte[] ToByteBuffer(object graph)
		{
			using(MemoryStream stream = new MemoryStream())
			{
				Serialize(stream, graph);
				return stream.ToArray();
			}
		}		

		/// <summary>
		/// Deserializes the binary representation of an object.
		/// </summary>
		/// <param name="buffer">binary representation of the object</param>
		/// <returns>deserialized object</returns>
		static public object FromByteBuffer(byte[] buffer)
		{
			using(MemoryStream stream = new MemoryStream(buffer))
			{
				return Deserialize(stream);
			}
		}

		/// <summary>
		/// Serializes an object into the specified stream.
		/// </summary>
		/// <param name="stream">specified stream</param>
		/// <param name="graph">object</param>
		static public void Serialize(Stream stream, object graph)
		{
			using(CompactBinaryWriter writer = new CompactBinaryWriter(new BinaryWriter(stream)))
			{
				Serialize(writer, graph);
			}
		}

		/// <summary>
		/// Deserializes an object from the specified stream.
		/// </summary>
		/// <param name="stream">specified stream</param>
		/// <returns>deserialized object</returns>
		static public object Deserialize(Stream stream)
		{
			using(CompactBinaryReader reader = new CompactBinaryReader(new BinaryReader(stream)))
			{
				return Deserialize(reader);
			}
		}

		/// <summary>
		/// Serializes an object into the specified compact binary writer.
		/// </summary>
		/// <param name="writer">specified compact binary writer</param>
		/// <param name="graph">object</param>
		static internal void Serialize(CompactBinaryWriter writer, object graph)
		{
			// Find an appropriate surrogate for the object
			ISerializationSurrogate surrogate = 
				TypeSurrogateProvider.GetSurrogateForObject(graph);
			// write type handle
			writer.Write(surrogate.TypeHandle);
			surrogate.Write(writer, graph);
		}

		/// <summary>
		/// Deserializes an object from the specified compact binary writer.
		/// </summary>
		/// <param name="writer">specified compact binary writer</param>
		/// <param name="graph">object</param>
		static internal object Deserialize(CompactBinaryReader reader)
		{
			// read type handle
			short handle = reader.ReadInt16();
			// Find an appropriate surrogate by handle
			ISerializationSurrogate surrogate = 
				TypeSurrogateProvider.GetSurrogateForTypeHandle(handle);
			return surrogate.Read(reader);
		}
	}
}