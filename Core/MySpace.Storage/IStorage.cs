using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySpace.Common.Storage;

namespace MySpace.Storage
{
	/// <summary>
	/// <para>Descriptive properties and administration methods for objects
	/// that store and retrieve items. Items are stored by keys, which
	/// generally are treated as unique within a key space. Key spaces may
	/// be further partitioned into numerically identified partitions.</para>
	/// <para>Store may also participate in the transactions of the
	/// System.Transactions framework.</para>
	/// </summary>
	public interface IStorage : IDisposable
	{
		#region Descriptive Properties

		/// <summary>
		/// Gets whether the stores supports key spaces.
		/// </summary>
		/// <value>If <see langword="true"/>, then keys are unique only within a
		/// key space value. Otherwise, keys are unique within the entire store,
		/// and key space values are ignored.</value>
		bool SupportsKeySpaces { get; }

		/// <summary>
		/// Gets whether the store supports key partitions.
		/// </summary>
		/// <value>If <see langword="true"/>, then keys are unique within a
		/// partition. Otherwise, keys are unique within the store/key space,
		/// and partition identifiers are ignored.</value>
		bool SupportsKeySpacePartitions { get; }

		/// <summary>
		/// Gets whether a particular key space supports key partitions.
		/// </summary>
		/// <param name="keySpace">The key space.</param>
		/// <returns><see langword="true"/> if the key space supports
		/// partitions; otherwise <see langword="false"/>. Should
		/// return <see langword="false"/> if
		/// <see cref="SupportsKeySpacePartitions"/> is <see langword="false"/>.</returns>
		bool GetKeySpacePartitionSupport(DataBuffer keySpace);

		/// <summary>
		/// Gets the type of transaction support the store provides, if any.
		/// </summary>
		/// <value>A <see cref="TransactionSupport"/> that specifies the
		/// transaction support.</value>
		/// <remarks>If supported, transactions will be used via the
		/// System.Transactions framework.</remarks>
		TransactionSupport TransactionSupport { get; }

		/// <summary>
		/// Gets the type of transaction commit the store provides for transactional
		/// stores.
		/// </summary>
		/// <remarks>If supported, transactions will be used via the
		/// System.Transactions framework.</remarks>
		TransactionCommitType CommitType { get; }

		/// <summary>
		/// Gets the scope within which the store exists.
		/// </summary>
		/// <value>An <see cref="ExecutionScope"/> describing the scope of the store.</value>
		ExecutionScope ExecutionScope { get; }

		/// <summary>
		/// Gets the behavior of the store as available space runs out.
		/// </summary>
		/// <value>An <see cref="OutOfSpacePolicy"/> that describes the behavior
		/// of the store as space runs out.</value>
		OutOfSpacePolicy OutOfSpacePolicy { get; }
		#endregion

		#region Administration
		/// <summary>
		/// Initializes the store.
		/// </summary>
		/// <param name="config">An object containing the configuration of the store.</param>
		void Initialize(object config);

		/// <summary>
		/// Reinitializes the store. To be called after <see cref="Initialize"/>.
		/// </summary>
		/// <param name="config">An object containing the new configuration of the store.</param>
		void Reinitialize(object config);
		#endregion
	}
}
