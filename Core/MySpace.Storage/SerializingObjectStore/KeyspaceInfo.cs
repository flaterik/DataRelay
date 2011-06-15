using System;
using System.Reflection;
using System.Reflection.Emit;

namespace MySpace.Storage
{
	/// <summary>
	/// Holder for information about a key space.
	/// </summary>
	internal sealed class KeyspaceInfo
	{
		/// <summary>
		/// Gets the type of object this key space represents.
		/// </summary>
		/// <value>The <see cref="Type"/> of objects stored in this
		/// key space, never null.</value>
		public Type ObjectType { get; private set; }

		/// <summary>
		/// Gets a delegate that produces an instance of type
		/// <see cref="ObjectType"/>.
		/// </summary>
		/// <value>A <see cref="Func{Object}"/> that wraps the invokation
		/// of <see cref="ObjectType"/>'s parameterless ctor. Is null if
		/// there is no such ctor.</value>
		public Func<object> ObjectCreator { get; private set; }

		/// <summary>
		/// Gets the type of header information of lists this key space represents.
		/// </summary>
		/// <value>The <see cref="Type"/> of list header information stored in this
		/// key space. Is null if this key space doesn't store lists.</value>
		public Type HeaderType { get; private set; }

		/// <summary>
		/// Gets a delegate that produces an instance of type
		/// <see cref="HeaderType"/>.
		/// </summary>
		/// <value>A <see cref="Func{Object}"/> that wraps the invokation
		/// of <see cref="HeaderType"/>'s parameterless ctor. Is null if
		/// <see cref="HeaderType"/> is null or if there is no such ctor.</value>		
		public Func<object> HeaderCreator { get; private set; }

		/// <summary>
		/// Gets whether this key space allows multiple items to be stored under
		/// the same key.
		/// </summary>
		/// <value>true if multiple items can be stored under the same key, otherwise
		/// false.</value>
		public bool AllowsMultiple { get; private set; }

		/// <summary>
		/// Creates a new <see cref="KeyspaceInfo"/> for a particular type.
		/// </summary>
		/// <typeparam name="T">The type represented by the keyspace.</typeparam>
		/// <returns>The new <see cref="KeyspaceInfo"/>.</returns>
		public static KeyspaceInfo Create<T>()
		{
			return new KeyspaceInfo
			{
				ObjectType = typeof(T),
				ObjectCreator = GetConstructor<T>()
			};
		}

		/// <summary>
		/// Creates a new <see cref="KeyspaceInfo"/> for list of a particular
		/// type and header information type.
		/// </summary>
		/// <typeparam name="T">The type of items for the lists represented by the keyspace.</typeparam>
		/// <typeparam name="THeader">The type of header for the lists represented by the keyspace.</typeparam>
		/// <param name="allowsMultiple">Whether or not the keyspace will store multltiple list items under
		/// the same key.</param>
		/// <returns>The new <see cref="KeyspaceInfo"/>.</returns>
		public static KeyspaceInfo CreateForList<T, THeader>(bool allowsMultiple)
		{
			return new KeyspaceInfo
			{
				ObjectType = typeof(T),
				ObjectCreator = GetConstructor<T>(),
				HeaderType = typeof(THeader),
				HeaderCreator = GetConstructor<THeader>(),
				AllowsMultiple = allowsMultiple
			};
		}
		private KeyspaceInfo() { }

		private static Func<object> GetConstructor<T>()
		{
			var type = typeof(T);
			var constructor = type.GetConstructor(BindingFlags.Instance |
				BindingFlags.Public | BindingFlags.NonPublic, null,
				Type.EmptyTypes, null);
			if (constructor == null) return null;
			var method = new DynamicMethod(string.Empty, typeof(object), 
				null, constructor.Module, true);
			var gen = method.GetILGenerator();
			gen.Emit(OpCodes.Newobj, constructor);
			gen.Emit(OpCodes.Ret);
			return (Func<object>)method.CreateDelegate(typeof(Func<object>));
		}
	}
}
