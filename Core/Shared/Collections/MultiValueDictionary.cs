using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MySpace.Common.Collections
{
	/// <summary>
	/// An implementation of <see cref="IDictionary{TKey,TValue}"/> that can contain multiple values in the same key.
	/// </summary>
	/// <typeparam name="TKey">The type of the key.</typeparam>
	/// <typeparam name="TValue">The type of the value.</typeparam>
	public class MultiValueDictionary<TKey, TValue> : IDictionary<TKey, TValue>
	{
		static readonly Slot[] _emptySlots = new Slot[0];

		readonly Dictionary<TKey, int> _keyIndexes;
		readonly IEqualityComparer<TValue> _valueComparer = EqualityComparer<TValue>.Default;
		Slot[] _slots = _emptySlots;
		int _count;
		int _freeListIndex = -1;
		int _emptyStartIndex;

		/// <summary>
		/// Initializes a new instance of the <see cref="MultiValueDictionary&lt;TKey, TValue&gt;"/> class.
		/// </summary>
		public MultiValueDictionary() : this(null)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MultiValueDictionary&lt;TKey, TValue&gt;"/> class.
		/// </summary>
		/// <param name="equalityComparer">The equality comparer used to determine equality between keys.</param>
		public MultiValueDictionary(IEqualityComparer<TKey> equalityComparer)
		{
			_keyIndexes = new Dictionary<TKey,int>(equalityComparer ?? EqualityComparer<TKey>.Default);
		}

		int AllocateSlot(TValue value)
		{
			var result = _freeListIndex;
			if (result >= 0)
			{
				_freeListIndex = _slots[_freeListIndex].Next;
			}
			else
			{
				if (_emptyStartIndex >= _slots.Length)
				{
					var newSlots = new Slot[Math.Max(_slots.Length * 2, 10)];
					Array.Copy(_slots, newSlots, _slots.Length);
					_emptyStartIndex = _slots.Length;
					_slots = newSlots;
				}

				result = _emptyStartIndex++;
			}
			_slots[result].Next = -1;
			_slots[result].Value = value;
			++_count;
			return result;
		}

		void FreeSlot(int index)
		{
			_slots[index].Next = _freeListIndex;
			_slots[index].Value = default(TValue);
			_freeListIndex = index;
			--_count;
		}

		/// <summary>
		/// Adds the specified key to the dictionary. If the key already exists it will add another value to that key.
		/// </summary>
		/// <param name="key">The key to add.</param>
		/// <param name="value">The value to add.</param>
		public void Add(TKey key, TValue value)
		{
			int slotIndex;
			if (_keyIndexes.TryGetValue(key, out slotIndex))
			{
				var newIndex = AllocateSlot(value);
				_slots[newIndex].Next = _slots[slotIndex].Next;
				_slots[slotIndex].Next = newIndex;
			}
			else
			{
				_keyIndexes[key] = AllocateSlot(value);
			}
		}

		/// <summary>
		/// Determines whether any values exist for the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns>
		/// 	<see langword="true"/> if any values exist for the specified key; otherwise, <see langword="false"/>.
		/// </returns>
		public bool ContainsKey(TKey key)
		{
			return _keyIndexes.ContainsKey(key);
		}

		/// <summary>
		/// Gets the keys.
		/// </summary>
		/// <value>The keys.</value>
		public ICollection<TKey> Keys
		{
			get { return _keyIndexes.Keys; }
		}

		/// <summary>
		/// Removes all values with the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns><see langword="true"/> if any values were removed; otherwise, <see langword="false"/>.</returns>
		public bool Remove(TKey key)
		{
			int slotIndex;
			if (_keyIndexes.TryGetValue(key, out slotIndex))
			{
				int next;
				for (; slotIndex >= 0; slotIndex = next)
				{
					next = _slots[slotIndex].Next;
					FreeSlot(slotIndex);
				}
				_keyIndexes.Remove(key);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Gets the value last added to the specified key or returns false if no values were found.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The last value added to the collection with the specified key or default(T) if not found.</param>
		/// <returns>The value last added to the specified key or returns false if no values were found.</returns>
		public bool TryGetValue(TKey key, out TValue value)
		{
			int slotIndex;
			if (_keyIndexes.TryGetValue(key, out slotIndex))
			{
				var next = _slots[slotIndex].Next;
				value = next >= 0 ? _slots[next].Value : _slots[slotIndex].Value;
				return true;
			}
			value = default(TValue);
			return false;
		}

		/// <summary>
		/// Gets all values for the specified key (most recently added items first).
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns>All values for the specified key.</returns>
		public IEnumerable<TValue> GetValues(TKey key)
		{
			int startIndex;
			if (!_keyIndexes.TryGetValue(key, out startIndex))
			{
				return Enumerable.Empty<TValue>();
			}
			return GetValues(startIndex);
		}

		private IEnumerable<TValue> GetValues(int index)
		{
			var first = _slots[index].Value;
			while ((index = _slots[index].Next) >= 0)
			{
				yield return _slots[index].Value;
			}
			yield return first;
		}

		/// <summary>
		/// Gets all values.
		/// </summary>
		/// <value>The values.</value>
		public ICollection<TValue> Values
		{
			get { return this.Select(p => p.Value).ToList(); }
		}

		/// <summary>
		/// Gets or sets the <see cref="TValue"/> with the specified key.
		/// If setting the value all other values with the specified key are over-written.
		/// </summary>
		/// <value>The <see cref="TValue"/> with the specified key.</value>
		/// <exception cref="KeyNotFoundException">
		///		<para>Getting a value and the specified key was not found.</para>
		/// </exception>
		public TValue this[TKey key]
		{
			get
			{
				TValue result;
				if (TryGetValue(key, out result))
				{
					return result;
				}
				throw new KeyNotFoundException("The specified key was not present in the dictionary.");
			}
			set
			{
				Remove(key);
				Add(key, value);
			}
		}

		void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
		{
			Add(item.Key, item.Value);
		}

		/// <summary>
		/// Clears this instance.
		/// </summary>
		public void Clear()
		{
			_slots = _emptySlots;
			_emptyStartIndex = 0;
			_freeListIndex = -1;
			_count = 0;
			_keyIndexes.Clear();
		}

		bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
		{
			return GetValues(item.Key).Contains(item.Value, _valueComparer);
		}

		void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			using (var enumerator = GetEnumerator())
			{
				for (int i = arrayIndex; i < array.Length; ++i)
				{
					if (!enumerator.MoveNext()) return;
					array[i] = enumerator.Current;
				}
			}
		}

		/// <summary>
		/// Gets the number of items stored in this instance.
		/// </summary>
		/// <value>The number of items stored in this instance.</value>
		public int Count
		{
			get { return _count; }
		}

		bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly
		{
			get { return false; }
		}

		bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
		{
			int index;
			if (_keyIndexes.TryGetValue(item.Key, out index))
			{
				if (_valueComparer.Equals(item.Value, _slots[index].Value))
				{
					if (_slots[index].Next >= 0)
					{
						_keyIndexes[item.Key] = _slots[index].Next;
					}
					else
					{
						_keyIndexes.Remove(item.Key);
					}
					FreeSlot(index);
					return true;
				}

				while (true)
				{
					int prevIndex = index;
					index = _slots[index].Next;
					if (index < 0) return false;
					if (_valueComparer.Equals(item.Value, _slots[index].Value))
					{
						_slots[prevIndex].Next = _slots[index].Next;
						FreeSlot(index);
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// Gets the enumerator for all key-value-pairs in this instance.
		/// </summary>
		/// <returns>The enumerator for all key-value-pairs in this instance.</returns>
		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			foreach (var pair in _keyIndexes)
			{
				var index = pair.Value;
				var first = _slots[index].Value;
				while ((index = _slots[index].Next) >= 0)
				{
					yield return new KeyValuePair<TKey, TValue>(pair.Key, _slots[index].Value);
				}
				yield return new KeyValuePair<TKey, TValue>(pair.Key, first);
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		[DebuggerDisplay("Next = {Next}, Value = {Value}")]
		private struct Slot
		{
			public int Next;
			public TValue Value;
		}
	}
}
