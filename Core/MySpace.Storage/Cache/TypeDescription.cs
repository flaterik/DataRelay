using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using MySpace.Common;
using MySpace.Common.Storage;

namespace MySpace.Storage.Cache
{
	/// <summary>
	/// Holds information about a cached type.
	/// </summary>
	public struct TypeDescription
	{
		/// <summary>
		/// Gets the key space associated with the cached type.
		/// </summary>
		/// <value>The <see cref="DataBuffer"/> that specifies the
		/// key space.</value>
		public DataBuffer KeySpace { get; private set; }

		/// <summary>
		/// Gets the duration from current time to a new expiration time.
		/// </summary>
		/// <value>The number of seconds in the time to live.</value>
		public int Ttl { get; private set; }

		/// <summary>
		/// Gets the cached type being described.
		/// </summary>
		/// <value>The cached <see cref="Type"/>.</value>
		public Type Type { get; private set; }

		private bool _constructorChecked;
		private Factory<object> _creator;

		/// <summary>
		/// Gets a delegate that returns a new instance of the cached type.
		/// </summary>
		/// <value>A <see cref="Factory{Object}"/> that creates a new instance of the
		/// cached type from its parameterless constructor; <see langword="null"/>
		/// if the type doesn't have such a constructor.</value>
		public Factory<object> Creator
		{
			get
			{
				if (!_constructorChecked)
				{
					_constructorChecked = true;
					if (Type != null)
					{
						try
						{
							_creator = DynamicMethods.GetCtor<object>(Type);							
						}
						catch(ArgumentException exc)
						{
							if (exc.ParamName != "TResult")
							{
								throw;
							}
						}

					}
				}
				return _creator;
			}
		}

		/// <summary>
		/// Gets the parameterless constructor for <typeparamref name="T"/>, if any.
		/// </summary>
		/// <typeparam name="T">The type of object to create.</typeparam>
		/// <returns>Null if <typeparamref name="T"/> isn't the same as
		/// <see cref="Type"/>, or the parameterless constructor of
		/// <typeparamref name="T"/> if any.</returns>
		public Factory<T> GetCreator<T>()
		{
			var type = typeof(T);
			if (!type.Equals(Type)) return null;
			try
			{
				return DynamicMethods.GetCtor<T>();
			}
			catch (ArgumentException exc)
			{
				if (exc.ParamName != "TResult")
				{
					throw;
				}
				return null;
			}
		}

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="TypeDescription"/> structure.</para>
		/// </summary>
		/// <param name="type">
		///		<para>The cached <see cref="Type"/>.</para>
		/// </param>
		/// <param name="keySpace">
		/// 	<para>The key space of the cached type.</para>
		/// </param>
		/// <param name="ttl">
		/// 	<para>The time to live, in seconds.</para>
		/// </param>
		public TypeDescription(Type type, DataBuffer keySpace, int ttl) : this()
		{
			Type = type;
			KeySpace = keySpace;
			Ttl = ttl;
		}

		private static readonly TypeDescription _notFound = new TypeDescription
       	{
       		Type = null,
       		_constructorChecked = true,
       		_creator = null,
       		KeySpace = DataBuffer.Empty,
       		Ttl = int.MinValue
       	};
		/// <summary>
		/// Gets a description instance representing no description found.
		/// </summary>
		/// <value>A <see cref="TypeDescription"/> where <see cref="Type"/>
		/// is <see langword="null"/>, <see cref="KeySpace"/> is
		/// <see cref="DataBuffer.Empty"/>, and <see cref="Ttl"/> is
		/// <see cref="Int32.MinValue"/>.</value>
		public static TypeDescription NotFound { get { return _notFound; } }

		/// <summary>
		/// Gets whether this instance represents no description found.
		/// </summary>
		public bool IsNotFound
		{
			get { return Type == null && KeySpace.IsEmpty; }
		}
	}
}