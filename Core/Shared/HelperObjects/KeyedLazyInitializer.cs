using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace MySpace.Common
{
	/// <summary>
	///	<para>A lazily initialized collection indexed by <typeparamref name="TKey"/>.
	///	You provide a factory method that will be called to construct values for keys that
	///	have not yet been encountered. You may also provide an <see cref="IEqualityComparer{TKey}"/>
	///	implementation that will be used to compare keys.
	///	This is a thread-safe class.</para>
	/// </summary>
	/// <typeparam name="TKey">The type of the key.</typeparam>
	/// <typeparam name="TValue">The type of the value.</typeparam>
	[DebuggerDisplay("Count={Count}")]
	public class KeyedLazyInitializer<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
	{
		private readonly object _syncRoot = new object();
		private readonly Func<TKey, TValue> _valueFactory;
		private readonly IEqualityComparer<TKey> _comparer;
		private Dictionary<TKey, TValue> _values = new Dictionary<TKey,TValue>();

		/// <summary>
		/// Initializes a new instance of the <see cref="KeyedLazyInitializer&lt;TKey, TValue&gt;"/> class.
		/// </summary>
		/// <param name="valueFactory">The value factory that will produce a new value given a key.</param>
		public KeyedLazyInitializer(Func<TKey, TValue> valueFactory)
			: this(valueFactory, null)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="KeyedLazyInitializer&lt;TKey, TValue&gt;"/> class.
		/// </summary>
		/// <param name="valueFactory">The value factory that will produce a new value given a key.</param>
		/// <param name="comparer">The equality comparer that will be used to compare keys.</param>
		public KeyedLazyInitializer(Func<TKey, TValue> valueFactory, IEqualityComparer<TKey> comparer)
		{
			ArgumentAssert.IsNotNull(valueFactory, "valueFactory");

			_valueFactory = valueFactory;
			_comparer = comparer ?? EqualityComparer<TKey>.Default;
		}

		/// <summary>
		/// Gets the <see cref="TValue"/> with the specified key.
		/// </summary>
		/// <value>The value for the specified key.</value>
		public TValue this[TKey key]
		{
			get
			{
				TValue result;
				if (_values.TryGetValue(key, out result)) return result;
				lock (_syncRoot)
				{
					if (_values.TryGetValue(key, out result)) return result;

					var newValue =_valueFactory(key);
					var newDictionary = new Dictionary<TKey, TValue>(_values.Count + 1, _comparer);
					foreach (var pair in _values)
					{
						newDictionary.Add(pair.Key, pair.Value);
					}
					newDictionary.Add(key, newValue);
					Thread.MemoryBarrier();
					_values = newDictionary;
					return newValue;
				}
			}
		}

		/// <summary>
		/// Removes the specified key. Returns <see langword="true"/> if the value
		/// was found and removed or <see langword="false"/> if the value was
		/// not found and the collection remained un-modified.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns><see langword="true"/> if the value
		/// was found and removed or <see langword="false"/> if the value was
		/// not found and the collection remained un-modified.</returns>
		public bool Remove(TKey key)
		{
			if (!_values.ContainsKey(key)) return false;
			lock (_syncRoot)
			{
				if (!_values.ContainsKey(key)) return false;

				var newDictionary = new Dictionary<TKey, TValue>(_values.Count - 1, _comparer);
				foreach (var pair in _values)
				{
					if (!_comparer.Equals(key, pair.Key))
					{
						newDictionary.Add(pair.Key, pair.Value);
					}
				}
				Thread.MemoryBarrier();
				_values = newDictionary;
				return true;
			}
		}

		/// <summary>
		/// Clears all items that have been created.
		/// </summary>
		public void Clear()
		{
			lock (_syncRoot)
			{
				_values = new Dictionary<TKey, TValue>(0);
			}
		}

		/// <summary>
		/// Gets the number of items that have been created so far.
		/// </summary>
		/// <value>The number of items that have been created so far.</value>
		public int Count
		{
			get { return _values.Count; }
		}

		/// <summary>
		/// Returns an enumerator that iterates through the collection.
		/// </summary>
		/// <returns>
		/// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
		/// </returns>
		/// <filterpriority>1</filterpriority>
		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			return _values.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
