using System;
using System.Collections;
using System.Collections.Generic;

namespace MySpace.Common.Collections
{
	/// <summary>
	/// 	<para>A stack implementation that will discard old values
	/// 	when new values are added once a specified capacity
	/// 	threshold is reached.</para>
	/// </summary>
	/// <typeparam name="T">
	///	<para>The type of object contained by the stack.</para>
	/// </typeparam>
	public class CountBoundStack<T> : IEnumerable<T>
	{
		private readonly T[] _items;
		private int _top = -1;
		private int _count;

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="CountBoundStack{T}"/> class.</para>
		/// </summary>
		/// <param name="maxCount">
		///	<para>The maximum capacity of the stack. If exceeded old values will be discarded.</para>
		/// </param>
		/// <exception cref="ArgumentOutOfRangeException">
		///	<para><paramref name="maxCount"/> is less then one.</para>
		/// </exception>
		public CountBoundStack(int maxCount)
		{
			if (maxCount < 1) throw new ArgumentOutOfRangeException("maxCount", "maxCount must be greater than zero");

			_items = new T[maxCount];
		}

		/// <summary>
		/// 	<para>Pushes the specified value onto the stack.</para>
		/// </summary>
		/// <param name="value">The value to push.</param>
		public void Push(T value)
		{
			if (_items.Length == ++_top) _top = 0;
			if (_count < _items.Length) ++_count;
			_items[_top] = value;
		}

		/// <summary>
		/// 	<para>Pops a value off of the stack.</para>
		/// </summary>
		/// <returns>
		///	<para>The value that was popped off.</para>
		/// </returns>
		/// <exception cref="InvalidOperationException">
		///	<para>The stack is empty and there are no values to pop.</para>
		/// </exception>
		public T Pop()
		{
			if (_count == 0) throw new InvalidOperationException("Empty Stack");

			T result = _items[_top];
			if (--_count == 0)
			{
				_top = -1;
			}
			else if (--_top < 0)
			{
				_top = _items.Length - 1;
			}
			return result;
		}

		/// <summary>
		/// 	<para>Gets the value on the top of the stack without removing it.</para>
		/// </summary>
		/// <returns>
		///	<para>The value on the top of the stack.</para>
		/// </returns>
		/// <exception cref="InvalidOperationException">
		///	<para>The stack is empty and there are no values to peek.</para>
		/// </exception>
		public T Peek()
		{
			if (_count == 0) throw new InvalidOperationException("Empty Stack");

			return _items[_top];
		}

		/// <summary>
		///	<para>Gets an array of values on the stack starting with the top one
		///	until a value fails the given predicate or <paramref name="maxResults"/> is reached.</para>
		/// </summary>
		/// <param name="condition">
		///	<para>Will be invoked on each result. If <see langword="false"/> is returned
		///	the method will end and the results obtained thus far will be returned; otherwise
		///	the method will continue.</para>
		/// </param>
		/// <param name="maxResults">
		///	<para>The maximum number of results to return. If zero, no results
		///	are returned. If negative, the result count is unbound.</para>
		/// </param>
		/// <returns>
		///	<para>An array of values on the stack starting with the top one
		///	until a value fails the given predicate or <paramref name="maxResults"/> is reached.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="condition"/> is <see langword="null"/>.</para>
		/// </exception>
		public T[] PeekWhile(Predicate<T> condition, int maxResults)
		{
			if (condition == null) throw new ArgumentNullException("condition");

			if (_count == 0 || maxResults == 0) return new T[] { };

			List<T> result = new List<T>();

			foreach (T item in this)
			{
				if (condition(item))
				{
					result.Add(item);
					if (maxResults > 0 && result.Count == maxResults) break;
				}
				else
				{
					break;
				}
			}
			return result.ToArray();
		}

		/// <summary>
		/// 	<para>Gets the number of items in the stack.</para>
		/// </summary>
		/// <value>
		/// 	<para>The number of items in the stack.</para>
		/// </value>
		public int Count
		{
			get { return _count; }
		}

		/// <summary>
		/// 	<para>Gets the maximum number of items that the stack can
		/// 	hold before discarding old values.</para>
		/// </summary>
		/// <value>
		/// 	<para>The maximum number of items that the stack can
		/// 	hold before discarding old values.</para>
		/// </value>
		public int MaxCount
		{
			get { return _items.Length; }
		}

		#region IEnumerable<T> Members

		/// <summary>
		/// Returns an enumerator that iterates through the collection.
		/// </summary>
		/// <returns>
		/// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
		/// </returns>
		public IEnumerator<T> GetEnumerator()
		{
			int index = _top;
			for (int i = 0; i < _count; i++)
			{
				yield return _items[index];
				if (--index < 0) index = _items.Length - 1;
			}
		}

		#endregion

		#region IEnumerable Members

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		#endregion
	}
}