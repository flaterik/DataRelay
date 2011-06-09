using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace MySpace.Common
{
	/// <summary>
	/// Represents a Lazy Indexer than can be enumerated.  You provide a factory method that will be called to 
	///	construct values for keys that have not yet been encountered. You also provide
	///	an <see cref="IEqualityComparer{TKey}"/> implementation that will be used to compare keys.
	///	This is a thread-safe pattern.
	/// </summary>
	/// <typeparam name="TKey">The datatype of the key.</typeparam>
	/// <typeparam name="TValue">The datatype of the vaule.</typeparam>
	public class ReadableLazyIndexer<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
	{
		private Dictionary<TKey, TValue> _dictionary;
		private readonly Factory<TKey, TValue> _indexer;
		private readonly object _syncRoot = new object();

		/// <summary>
		///	Initializes a new <see cref="ReadableLazyIndexer{TKey,TValue}"/>. 
		/// </summary>
		/// <typeparam name="TKey">The type indexer argument.</typeparam>
		/// <typeparam name="TValue">The type values stored in the indexer.</typeparam>
		/// <param name="valueFactory">
		///	<para>A factory method for creating values given a key. This method will
		///	only be called once for each key.</para>
		/// </param>
		/// <returns>
		///	<para>A lazily initialized <typeparamref name="TValue"/> given a <typeparamref name="TKey"/>.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="valueFactory"/> is <see langword="null"/>.</para>
		/// </exception>
		public ReadableLazyIndexer(Factory<TKey, TValue> valueFactory)
		{
			if (valueFactory == null) throw new ArgumentNullException("valueFactory");
			_dictionary = new Dictionary<TKey, TValue>();
			_indexer = _lazyIndexer(valueFactory, null);
		}

		/// <summary>
		///	Initializes a new <see cref="ReadableLazyIndexer{TKey,TValue}"/>. 
		/// </summary>
		/// <typeparam name="TKey">The type indexer argument.</typeparam>
		/// <typeparam name="TValue">The type values stored in the indexer.</typeparam>
		/// <param name="valueFactory">
		///	<para>A factory method for creating values given a key. This method will
		///	only be called once for each key.</para>
		/// </param>
		/// <param name="comparer">
		///	<para>The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys,
		///	or <see langword="null"/> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</para>
		/// </param>
		/// <returns>
		///	<para>A lazily initialized <typeparamref name="TValue"/> given a <typeparamref name="TKey"/>.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="valueFactory"/> is <see langword="null"/>.</para>
		/// </exception>
		public ReadableLazyIndexer(Factory<TKey, TValue> valueFactory, IEqualityComparer<TKey> comparer)
		{
			if (valueFactory == null) throw new ArgumentNullException("valueFactory");
			_dictionary = new Dictionary<TKey, TValue>(comparer);
			_indexer = _lazyIndexer(valueFactory, comparer);
		}
			
		/// <summary>
		/// Gets the indexer.
		/// </summary>
		public Factory<TKey, TValue> Indexer
		{
			get { return _indexer; }
		}

		/// <summary>
		/// Gets the number of items in the indexer.
		/// </summary>
		public int Count
		{
			get { return _dictionary.Count; }
		}

		private Factory<TKey, TValue> _lazyIndexer(Factory<TKey, TValue> valueFactory, IEqualityComparer<TKey> comparer)
		{
			return key =>
			{
				TValue result;
				if (_dictionary.TryGetValue(key, out result)) return result;
				lock (_syncRoot)
				{
					if (_dictionary.TryGetValue(key, out result)) return result;

					var newValue = valueFactory(key);
					var newDictionary = new Dictionary<TKey, TValue>(_dictionary.Count + 1, comparer);
					foreach (var pair in _dictionary)
					{
						newDictionary.Add(pair.Key, pair.Value);
					}
					newDictionary.Add(key, newValue);
					Interlocked.Exchange(ref _dictionary, newDictionary);
					return newValue;
				}
			};
		}

		#region IEnumerable<KeyValuePair<TKey,TValue>> Members

		/// <summary>
		/// Returns an enumerator to iterate the current collection.  If a new item has been added since this method was invoked it will not appear in the iteration.
		/// </summary>
		/// <returns>Returns an enumerator.</returns>
		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			return _dictionary.GetEnumerator();
		}

		#endregion

		#region IEnumerable Members
		/// <summary>
		/// Returns an enumerator to iterate the current collection.  If a new item has been added since this method was invoked it will not appear in the iteration.
		/// </summary>
		/// <returns>Returns an enumerator.</returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable) _dictionary).GetEnumerator();
		}

		#endregion
	}
	
}
