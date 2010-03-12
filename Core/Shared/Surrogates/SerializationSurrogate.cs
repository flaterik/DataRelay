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
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;

using MySpace.Common.CompactSerialization.IO;

namespace MySpace.Common.CompactSerialization.Surrogates
{
	/// <summary>
	/// Interface that defines methods to be implemented by a serialization surrogate.
	/// </summary>
	public interface ISerializationSurrogate
	{
		/// <summary> 
		/// Return the type of object for which this object is a surrogate. 
		/// </summary>
		Type ActualType { get; }

		/// <summary> 
		/// Type handle associated with the type provided by the <see cref="TypeSurrogateProvider"/> 
		/// </summary>
		short TypeHandle { get; set; }

		/// <summary>
		/// Read an object of type <see cref="ActualType"/> from the stream reader
		/// </summary>
		/// <param name="reader">stream reader</param>
		/// <returns>object read from the stream reader</returns>
		object Read(CompactBinaryReader reader);

		/// <summary>
		/// Write an object of type <see cref="ActualType"/> to the stream writer
		/// </summary>
		/// <param name="writer">stream writer</param>
		/// <param name="graph">object to be written to the stream reader</param>
		void Write(CompactBinaryWriter writer, object graph);
	}

	/// <summary>
	/// A serialization surrogate in the sense that it is responsible for serialization and
	/// deserialization of another object. 
	/// </summary>
	public class SerializationSurrogate: ISerializationSurrogate
	{
		private short handle;
		private Type  type;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="t">The type for which it is a surrogate</param>
		public SerializationSurrogate(Type t)
		{
			type = t;
		}

		/// <summary> 
		/// Return the type of object for which this object is a surrogate. 
		/// </summary>
		public Type ActualType
		{
			get { return type; }
		}

		/// <summary> 
		/// Magic ID associated with the type provided by the <see cref="TypeSurrogateProvider"/> 
		/// </summary>
		public short TypeHandle
		{
			get { return handle; }
			set { handle = value; }
		}

		/// <summary>
		/// Creates instance of <see cref="ActualType"/>. Calls the default constructor and returns
		/// the object. There must be a default constructor even though it is private.
		/// </summary>
		/// <returns></returns>
		public virtual object CreateInstance()
		{
			return Activator.CreateInstance(
				ActualType,
				BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.CreateInstance|BindingFlags.Instance,
				null,
				null,
				null,
				null);
		}

		/// <summary>
		/// Read an object of type <see cref="ActualType"/> from the stream reader
		/// </summary>
		/// <param name="reader">stream reader</param>
		/// <returns>object read from the stream reader</returns>
		public virtual object Read(CompactBinaryReader reader)
		{
			return null;
		}

		/// <summary>
		/// Write an object of type <see cref="ActualType"/> to the stream writer
		/// </summary>
		/// <param name="writer">stream writer</param>
		/// <param name="graph">object to be written to the stream reader</param>
		public virtual void Write(CompactBinaryWriter writer, object graph)
		{
		}
	}

}
