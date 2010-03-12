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
using System.Collections.Specialized;
using System.Reflection;

using MySpace.Common.CompactSerialization.Surrogates;

namespace MySpace.Common.CompactSerialization
{
	/// <summary>
	/// Provides the common type identification system. Takes care of registering type surrogates
	/// and type handles. Provides methods to register <see cref="ICompactSerializable"/> implementations
	/// utilizing the built-in surrogate for <see cref="ICompactSerializable"/>.
	/// </summary>
	public sealed class TypeSurrogateProvider
	{
		private	static IDictionary				typeSurrogateMap = Hashtable.Synchronized(new Hashtable());
		private	static IDictionary				handleSurrogateMap = Hashtable.Synchronized(new Hashtable());

		private static ISerializationSurrogate	nullSurrogate = new NullSerializationSurrogate();
		private static ISerializationSurrogate	defaultSurrogate = new ObjectSerializationSurrogate(typeof(object));
		
		private static short					typeHandle = 0;

		/// <summary>
		/// Static constructor registers built-in surrogates with the system.
		/// </summary>
		static TypeSurrogateProvider()
		{
			RegisterSerializationSurrogate(nullSurrogate);
			RegisterSerializationSurrogate(defaultSurrogate);

			RegisterSerializationSurrogate(new BooleanSerializationSurrogate());
			RegisterSerializationSurrogate(new ByteSerializationSurrogate());
			RegisterSerializationSurrogate(new CharSerializationSurrogate());
			RegisterSerializationSurrogate(new SingleSerializationSurrogate());
			RegisterSerializationSurrogate(new DoubleSerializationSurrogate());
			RegisterSerializationSurrogate(new DecimalSerializationSurrogate());
			RegisterSerializationSurrogate(new Int16SerializationSurrogate());
			RegisterSerializationSurrogate(new Int32SerializationSurrogate());
			RegisterSerializationSurrogate(new Int64SerializationSurrogate());
			RegisterSerializationSurrogate(new StringSerializationSurrogate());
			RegisterSerializationSurrogate(new DateTimeSerializationSurrogate());
			RegisterSerializationSurrogate(new SByteSerializationSurrogate());
			RegisterSerializationSurrogate(new UInt16SerializationSurrogate());
			RegisterSerializationSurrogate(new UInt32SerializationSurrogate());
			RegisterSerializationSurrogate(new UInt64SerializationSurrogate());

			RegisterSerializationSurrogate(new ObjectArraySerializationSurrogate());
			RegisterSerializationSurrogate(new BooleanArraySerializationSurrogate());
			RegisterSerializationSurrogate(new ByteArraySerializationSurrogate());
			RegisterSerializationSurrogate(new CharArraySerializationSurrogate());
			RegisterSerializationSurrogate(new SingleArraySerializationSurrogate());
			RegisterSerializationSurrogate(new DoubleArraySerializationSurrogate());
			RegisterSerializationSurrogate(new DecimalArraySerializationSurrogate());
			RegisterSerializationSurrogate(new Int16ArraySerializationSurrogate());
			RegisterSerializationSurrogate(new Int32ArraySerializationSurrogate());
			RegisterSerializationSurrogate(new Int64ArraySerializationSurrogate());
			RegisterSerializationSurrogate(new StringArraySerializationSurrogate());
			RegisterSerializationSurrogate(new DateTimeArraySerializationSurrogate());
			RegisterSerializationSurrogate(new SByteArraySerializationSurrogate());
			RegisterSerializationSurrogate(new UInt16ArraySerializationSurrogate());
			RegisterSerializationSurrogate(new UInt32ArraySerializationSurrogate());
			RegisterSerializationSurrogate(new UInt64ArraySerializationSurrogate());
			
			RegisterSerializationSurrogate(new ArraySerializationSurrogate(typeof(Array)));
			RegisterSerializationSurrogate(new IListSerializationSurrogate(typeof(ArrayList)));
			RegisterSerializationSurrogate(new IDictionarySerializationSurrogate(typeof(Hashtable)));
		}


		#region /       ISerializationSurrogate specific        /

		/// <summary>
		/// Finds and returns an appropriate <see cref="ISerializationSurrogate"/> for the given
		/// object.
		/// </summary>
		/// <param name="graph">specified object</param>
		/// <returns><see cref="ISerializationSurrogate"/> object</returns>
		static internal ISerializationSurrogate GetSurrogateForObject(object graph)
		{
			if(graph == null)
				return nullSurrogate;
			return GetSurrogateForType(graph.GetType());
		}

		/// <summary>
		/// Finds and returns an appropriate <see cref="ISerializationSurrogate"/> for the given
		/// type.
		/// </summary>
		/// <param name="type">specified type</param>
		/// <returns><see cref="ISerializationSurrogate"/> object</returns>
		static public ISerializationSurrogate GetSurrogateForType(Type type)
		{
			ISerializationSurrogate surrogate = (ISerializationSurrogate)typeSurrogateMap[type];
			if(surrogate == null)
				surrogate = defaultSurrogate;
			return surrogate;
		}

		/// <summary>
		/// Finds and returns an appropriate <see cref="ISerializationSurrogate"/> for the given
		/// type handle.
		/// </summary>
		/// <param name="handle">type handle</param>
		/// <returns><see cref="ISerializationSurrogate"/> object</returns>
		static internal ISerializationSurrogate GetSurrogateForTypeHandle(short handle)
		{
			ISerializationSurrogate surrogate = (ISerializationSurrogate)handleSurrogateMap[handle];
			if(surrogate == null)
				surrogate = defaultSurrogate;
			return surrogate;
		}

		/// <summary>
		/// Registers the specified <see cref="ISerializationSurrogate"/> with the system.
		/// </summary>
		/// <param name="surrogate">specified surrogate</param>
		/// <returns>false if the surrogated type already has a surrogate</returns>
		static public bool RegisterSerializationSurrogate(ISerializationSurrogate surrogate)
		{
			if(surrogate == null) throw new ArgumentNullException("surrogate");
			lock (typeSurrogateMap.SyncRoot)
			{
				if (!typeSurrogateMap.Contains(surrogate.ActualType))
				{
					surrogate.TypeHandle = ++typeHandle;
					typeSurrogateMap.Add(surrogate.ActualType, surrogate);
					handleSurrogateMap.Add(surrogate.TypeHandle, surrogate);
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Unregisters the specified <see cref="ISerializationSurrogate"/> from the system.
		/// </summary>
		/// <param name="surrogate">specified surrogate</param>
		static public void UnregisterSerializationSurrogate(ISerializationSurrogate surrogate)
		{
			if(surrogate == null) throw new ArgumentNullException("surrogate");
			lock (typeSurrogateMap.SyncRoot)
			{
				typeSurrogateMap.Remove(surrogate.ActualType);
				handleSurrogateMap.Remove(surrogate.TypeHandle);
			}
		}

		#endregion

		#region /       ICompactSerializable specific        /

		/// <summary>
		/// Registers a type that implements <see cref="ICompactSerializable"/> with the system. If the
		/// type is an array of <see cref="ICompactSerializable"/>s appropriate surrogates for arrays
		/// and the element type are also registered.
		/// </summary>
		/// <param name="type">type that implements <see cref="ICompactSerializable"/></param>
		/// <returns>false if the type is already registered</returns>
		static public bool RegisterCompactType(Type type)
		{
			Type original = type;
			while(type.IsArray) { type = type.GetElementType(); }

			if(!typeof(ICompactSerializable).IsAssignableFrom(type))
				return false;
			
			if(!typeSurrogateMap.Contains(type))
			{
				System.Diagnostics.Debug.WriteLine("Registered suurogate for type " + type.FullName);
				ISerializationSurrogate surrogate = new ICompactSerializableSerializationSurrogate(type);
				RegisterSerializationSurrogate(surrogate);

				while(original.IsArray)
				{
					System.Diagnostics.Debug.WriteLine("Registered suurogate for type array " + original.FullName);
					surrogate = new ArraySerializationSurrogate(original);
					RegisterSerializationSurrogate(surrogate);
					original = original.GetElementType();
				}

				return true;
			}
			return false;
		}

		/// <summary>
		/// Registers the type and its base types that implements <see cref="ICompactSerializable"/> 
		/// with the system.
		/// </summary>
		/// <param name="type">type that implements <see cref="ICompactSerializable"/></param>
		static public void RegisterCompactTypeTree(Type type)
		{
			do
			{
				if(!RegisterCompactType(type))
				{
					return;
				}
				type = type.BaseType;
			} while(type != null);
		}

		/// <summary>
		/// Registers the type, its base types and aggregated members that implements 
		/// <see cref="ICompactSerializable"/> with the system.
		/// </summary>
		/// <param name="type">type that implements <see cref="ICompactSerializable"/></param>
		static public void RegisterCompactTypeGraph(Type type)
		{
			do
			{
				if(!RegisterCompactType(type))
				{
					return;
				}
				MemberInfo[] members = 
					System.Runtime.Serialization.FormatterServices.GetSerializableMembers(type);
				for (Int32 i = 0 ; i < members.Length; i++) 
				{
					FieldInfo field = (FieldInfo) members[i];
					RegisterCompactTypeGraph(field.FieldType);
				}

				type = type.BaseType;
			} while(type != null);
		}

		/// <summary>
		/// Unregisters the surrogate for the specified type that implements 
		/// <see cref="ICompactSerializable"/> from the system.
		/// </summary>
		/// <param name="type">the specified type</param>
		static public void UnregisterCompactType(Type type)
		{
			if(!type.IsSubclassOf(typeof(ICompactSerializable)))
				return;

			ISerializationSurrogate surrogate = GetSurrogateForType(type);
			UnregisterSerializationSurrogate(surrogate);
			System.Diagnostics.Debug.WriteLine("Unregistered suurogate for type " + type.FullName);
//			while(type.IsArray)
//			{
//				surrogate = GetSurrogateForType(type);
//				UnregisterSerializationSurrogate(surrogate);
//				type = type.GetElementType();
//			}
		}

		/// <summary>
		/// Unregisters the surrogate for the specified type and its base types that implements 
		/// <see cref="ICompactSerializable"/> from the system.
		/// </summary>
		/// <param name="type">the specified type</param>
		static public void UnregisterCompactTypeTree(Type type)
		{
			if(!typeof(ICompactSerializable).IsAssignableFrom(type))
				return;
			do
			{
				UnregisterCompactType(type);
				type = type.BaseType;
			} while(type != null);
		}

		/// <summary>
		/// Unregisters the surrogate for the specified type, its base types and aggregated 
		/// members that implements <see cref="ICompactSerializable"/> from the system.
		/// </summary>
		/// <param name="type">the specified type</param>
		static public void UnregisterCompactTypeGraph(Type type)
		{
			if(!typeof(ICompactSerializable).IsAssignableFrom(type))
				return;
			do
			{
				UnregisterCompactType(type);
				MemberInfo[] members = 
					System.Runtime.Serialization.FormatterServices.GetSerializableMembers(type);
				for (Int32 i = 0 ; i < members.Length; i++) 
				{
					FieldInfo field = (FieldInfo) members[i];
					UnregisterCompactTypeGraph(field.FieldType);
				}

				type = type.BaseType;
			} while(type != null);
		}

		#endregion
	}
}
