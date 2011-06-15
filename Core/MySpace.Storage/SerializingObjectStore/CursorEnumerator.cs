using System;
using System.Collections.Generic;
using MySpace.Common.Storage;
using Serializer = MySpace.Common.IO.Serializer;

namespace MySpace.Storage
{
	/// <summary>
	/// Provides header information and enumeration using a binary cursor.
	/// </summary>
	/// <typeparam name="T">The type of object yielded by the enumeration.</typeparam>
	/// <typeparam name="THeader">The type of header information.</typeparam>
	internal sealed class CursorEnumerator<T, THeader> : IEnumerator<T>
	{
		private ObjectListForMultiples<T, THeader> _list;
		private T _current;

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="CursorEnumerator{T, THeader}"/>
		///		class.</para>
		/// </summary>
		/// <param name="list">
		/// 	<para>The underlying cursor based <see cref="ObjectListForMultiples{T, THeader}"/>.</para>
		/// </param>
		public CursorEnumerator(ObjectListForMultiples<T, THeader> list)
		{
			_list = list;
			var dummy = new byte[1];
			if (_list.Cursor.Get(dummy, true) < 0)
			{
				throw new ApplicationException("No header");
			}
		}

		/// <summary>
		/// 	<para>Gets the element in the collection at the current position of the enumerator.</para>
		/// </summary>
		/// <returns>
		/// 	<para>The element in the collection at the current position of the enumerator.</para>
		/// </returns>
		public T Current
		{
			get { return _current; }
		}

		/// <summary>
		/// 	<para>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</para>
		/// </summary>
		public void Dispose()
		{
			_list.ClearCursor();
			_list = null;
		}

		object System.Collections.IEnumerator.Current
		{
			get { return _current; }
		}

		/// <summary>
		/// 	<para>Advances the enumerator to the next element of the collection.</para>
		/// </summary>
		/// <returns>
		/// 	<para>true if the enumerator was successfully advanced to the next element; false if the enumerator has passed the end of the collection.</para>
		/// </returns>
		/// <exception cref="System.InvalidOperationException">
		/// 	<para>The collection was modified after the enumerator was created.</para>
		/// </exception>
		public bool MoveNext()
		{
			//if (!_list.Cursor.MoveNext()) return false;
			using (var itemValue = _list.Storage.StreamPool.GetItem())
			{
				DataBuffer valueBuffer;
				var results = _list.ReadCursor(itemValue, out valueBuffer);
				if (results < 0)
				{
					return false;
				}
				var stream = itemValue.Item;
				stream.Position = 0;
				_current = _list.Creator();
				Serializer.Deserialize(stream, _current);
				return true;
			}
		}

		/// <summary>
		/// 	<para>Not supported.</para>
		/// </summary>
		/// <exception cref="NotSupportedException">
		/// 	<para>Enumeration cannot be reset.</para>
		/// </exception>
		public void Reset()
		{
			throw new NotSupportedException();
		}
	}
}
