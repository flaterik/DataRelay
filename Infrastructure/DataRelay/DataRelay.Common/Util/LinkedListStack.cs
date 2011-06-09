using System;
using System.Collections.Generic;
using System.Threading;

namespace MySpace.DataRelay
{
	/// <summary>
	/// A simple stack (First in/last out) linked list of generics. This class is not thread safe.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class LinkedListStack<T> : IEnumerable<T>
	{

		/// <summary>
		/// Create an empty linked list.
		/// </summary>
		public LinkedListStack()
		{
		}

		/// <summary>
		/// Create a linked list containing all items of the supplied list, while preserving the order
		/// </summary>        
		public LinkedListStack(IList<T> list)
		{
			Push(list);
		}

		private int _count;
		/// <summary>
		/// The number of elements in the list.
		/// </summary>
		public int Count
		{
			get
			{
				return _count;
			}
		}
		
		internal SimpleLinkedListNode head;

		/// <summary>
		/// Push all items of the supplied list on to this one, while preserving the order
		/// </summary>
		public void Push(LinkedListStack<T> list)
		{
			T[] otherListItems = list.PeekAll();
			Push(otherListItems);
		}

		/// <summary>
		/// Push all items of the supplied list, while preserving the order. Note that this means pushing from the back of the list to the front.
		/// </summary>
		/// <param name="list"></param>
		public void Push(IList<T> list)
		{
			int count = list.Count;
			for (int i = count - 1; i >= 0; i--)
			{
				Push(list[i]);
			}
		}

		/// <summary>
		/// Push all items of the supplied list, while preserving the order. Note that this means pushing from the back of the list to the front.
		/// </summary>
		/// <param name="list"></param>
		public void Push(T[] list)
		{
			int count = list.Length;
			for (int i = count - 1; i >= 0; i--)
			{
				Push(list[i]);
			}
		}

		/// <summary>
		/// Push the value onto the head of this list.
		/// </summary>        
		public void Push(T value)
		{
			SimpleLinkedListNode newHead = new SimpleLinkedListNode(value) {Next = head};
			head = newHead;
			_count++;
		}

		/// <summary>
		/// Pop the head off of the list.
		/// </summary>		
		public bool Pop(out T value)
		{
			if (head != null)
			{
				SimpleLinkedListNode prevHead = head;
				head = prevHead.Next;
				value = prevHead.Value;
				_count--;
				return true;
			}
			value = default(T);
			return false;
		}

		/// <summary>
		/// Look at the head of the list without removing it
		/// </summary>
		/// <returns></returns>
		public bool Peek(out T value)
		{
			if (head != null)
			{
				value = head.Value;
				return true;
			}
			value = default(T);
			return false;
		}

		/// <summary>
		/// Return a copy of the entire list.
		/// </summary>
		/// <returns></returns>
		public T[] PeekAll()
		{

			T[] list = new T[Count];
			SimpleLinkedListNode pointer = head;
			int index = 0;
			while (pointer != null)
			{
				list[index++] = pointer.Value;
				pointer = pointer.Next;
			}
			return list;
		}

		/// <summary>
		/// Gets an object that can be used to synchronize access to the LinkedListStack
		/// </summary>
		public object SyncRoot
		{
			get
			{
				if (_syncRoot == null)
				{
					Interlocked.CompareExchange(ref _syncRoot, new object(), null);
				}
				return _syncRoot;
			}
		}
		private object _syncRoot;

		#region IEnumerable<T> Members

		/// <summary>
		/// Creates an enumerator for this linked list
		/// </summary>
		public IEnumerator<T> GetEnumerator()
		{
			return new Enumerator(this);
		}

		#endregion

		#region IEnumerable Members

		/// <summary>
		/// Creates an enumerator for this linked list
		/// </summary>
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return new Enumerator(this);
		}

		#endregion

		/// <summary>
		/// Provides an Enumerator for the SimpleLinkedList class
		/// </summary>
		public struct Enumerator : IEnumerator<T> 
		{
			internal Enumerator(LinkedListStack<T> list)
			{
				_list = list;
				_currentValue = default(T);
				_nextNode = list.head;
				_index = 0;
			}
			
			private readonly LinkedListStack<T> _list;
			private SimpleLinkedListNode _nextNode;
			private T _currentValue;
			private int _index;
			
			#region IEnumerator<T> Members

			public T Current
			{
				get { return _currentValue; }
			}

			#endregion

			#region IDisposable Members

			public void Dispose()
			{
			}

			#endregion

			#region IEnumerator Members

			object System.Collections.IEnumerator.Current
			{
				get
				{
					if(_index == 0 || _index == _list.Count + 1)
						throw new InvalidOperationException("The enumerator for this list is not on a current value.");
					return _currentValue;
				}
			}

			/// <summary>
			/// Moves to the next item in the list.
			/// </summary>
			/// <returns>False if the end of list was reached, true otherwise.</returns>
			public bool MoveNext()
			{
				if (_nextNode == null)
				{
					_index = _list.Count + 1;
					return false;
				}
				_index++;
				_currentValue = _nextNode.Value;
				_nextNode = _nextNode.Next;
				return true;
			}

			/// <summary>
			/// Resets this enumerator to its default state.
			/// </summary>
			public void Reset()
			{
				_currentValue = default(T);
				_nextNode = _list.head;
				_index = 0;
			}

			#endregion
		}

		/// <summary>
		/// Item used in SimpleLinkedList class
		/// </summary>
		/// <typeparam name="T"></typeparam>
		internal class SimpleLinkedListNode
		{
			/// <summary>
			/// Create a node containing the supplied value.
			/// </summary>		
			internal SimpleLinkedListNode(T value)
			{
				Value = value;
			}

			internal T Value;

			internal SimpleLinkedListNode Next;
		}
	}

	

	

}
