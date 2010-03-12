using System.Collections.Generic;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// A simple queue (First in/First out) linked list of generics
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class SimpleLinkedList<T>
	{

		/// <summary>
		/// Create an empty linked list.
		/// </summary>
		public SimpleLinkedList()
		{
		}

		/// <summary>
		/// Create a linked list containing all items of the supplied list.
		/// </summary>        
		public SimpleLinkedList(IList<T> list)
		{
			for (int i = 0; i < list.Count; i++)
			{
				Push(list[i]);
			}
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
		
		internal SimpleLinkedListNode<T> head;

		/// <summary>
		/// Push all items of the supplied list on to this one.
		/// </summary>
		/// <param name="list"></param>
		public void Push(SimpleLinkedList<T> list)
		{
			while (list.head != null)
			{
				SimpleLinkedListNode<T> next = list.head.Next;
				list.head.Next = null;
				Push(list.head);
				list.head = next;
			}
		}		
		
		/// <summary>
		/// Push the value onto the head of this list.
		/// </summary>        
		public void Push(T value)
		{
			SimpleLinkedListNode<T> newHead = new SimpleLinkedListNode<T>(value) {Next = head};
			head = newHead;
			_count++;
		}

		/// <summary>
		/// Push the supplied node onto this list.
		/// </summary>        
		public void Push(SimpleLinkedListNode<T> node)
		{
			node.Next = head;
			head = node;
			_count++;
		}

		/// <summary>
		/// Pop the head off of the list.
		/// </summary>		
		public bool Pop(out T value)
		{
			if (head != null)
			{
				SimpleLinkedListNode<T> prevHead = head;
				head = prevHead.Next;
				value = prevHead.Value;
				_count--;
				return true;
			}
			value = default(T);
			return false;
		}

		/// <summary>
		/// Return a copy of the entire list.
		/// </summary>
		/// <returns></returns>
		public List<T> PeekAll()
		{
			List<T> list = new List<T>(Count);
			SimpleLinkedListNode<T> pointer = head;
			while (pointer != null)
			{
				list.Add(pointer.Value);
				pointer = pointer.Next;
			}
			return list;
		}
	}

	/// <summary>
	/// Item used in SimpleLinkedList class
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class SimpleLinkedListNode<T>
	{
		/// <summary>
		/// Create a node containing the supplied value.
		/// </summary>		
		public SimpleLinkedListNode(T value)
		{
			Value = value;
		}
		
		internal T Value;
		
		internal SimpleLinkedListNode<T> Next;
	}


}
