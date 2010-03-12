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

using MySpace.Common.CompactSerialization.IO;

namespace MySpace.Common.CompactSerialization
{
	/// <summary> 
	/// Implementations of ICompactSerializable can add their state directly to the output stream, 
	/// enabling them to bypass costly serialization.
	/// </summary>
	/// <remarks>
	/// Objects that implement <see cref="ICompactSerializable"/> must have a default 
	/// constructor (can be private). 
	/// <para>
	/// As per current implementation when a <see cref="ICompactSerializable"/> is deserialized 
	/// the default constructor is not invoked, therefore the object must "construct" itself in 
	/// <see cref="ICompactSerializable.Deserialize"/>.
	/// </para>
	/// </remarks>
	public interface ICompactSerializable
	{
		/// <summary>
		/// Load the state from the passed stream reader object.
		/// </summary>
		/// <param name="reader">A <see cref="CompactBinaryReader"/> object</param>
		/// <remarks>
		/// As per current implementation when a <see cref="ICompactSerializable"/> is deserialized 
		/// the default constructor is not invoked, therefore the object must "construct" itself in 
		/// <see cref="ICompactSerializable.Deserialize"/>.
		/// </remarks>
		void Deserialize(CompactBinaryReader reader);

		/// <summary>
		/// Save the the state to the passed stream reader object.
		/// </summary>
		/// <param name="writer">A <see cref="CompactBinaryWriter"/> object</param>
		void Serialize(CompactBinaryWriter writer);
	}
}
