using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MySpace.Common;

namespace MySpace.DataRelay.Client
{
	/// <summary>
	/// This class is a read only collection of type <see cref="RelayResult{ICacheParameter}"/>.
	/// </summary>
	public class RelayResults : ReadOnlyCollection<RelayResult<ICacheParameter>>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="RelayResults"/> class.
		/// </summary>
		internal RelayResults() : this(new List<RelayResult<ICacheParameter>>()) { }

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="RelayResults"/> class
		///		with a supplied list.</para>
		/// </summary>
		/// <param name="results">
		/// 	<para>The <see cref="IList{RelayResult{ICacheParameter}}"/></para> of
		///		elements.
		/// </param>
		internal RelayResults(IList<RelayResult<ICacheParameter>> results) :
			base(results) { }

		/// <summary>
		/// Adds a <see cref="RelayResult{ICacheParameter}"/> type to the list.
		/// </summary>
		/// <param name="item">Item to add to the colections; <see langword="null"/> is not valid.</param>
		/// <exception cref="ArgumentNullException">
		/// When <see langword="null"/> is added to <see cref="RelayResults"/>.
		/// </exception>
		internal void Add(RelayResult<ICacheParameter> item)
		{
			if (item == null)
			{
				throw new System.ArgumentNullException("item");
			}
			this.Items.Add(item);
		}
	}
}
