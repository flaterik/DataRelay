using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using MySpace.BerkeleyDb.Configuration;
using MySpace.Common.Storage;

namespace BerkeleyDbWrapper
{
	/// <summary>
	/// Delegate for an action taken on a <see cref="DatabaseEntry"/>.
	/// </summary>
	public delegate void RMWDelegate(DatabaseEntry entry);

	/// <summary>
	/// The abstract base class for a managed wrapper for a Berkeley Db database.
	/// </summary>
	public abstract class Database : IEnumerable<DatabaseRecord>, IDisposable
	{
		// Static Fields
		private static readonly Database _pseudoSingleton = ConcreteFactory.Create<Database>();

		// Fields
		/// <summary>
		/// The <see cref="Int32"/> identifier of this instance.
		/// </summary>
		public int Id;

		// Methods
		/// <summary>
		/// Backs up the database by copying the database file contents.
		/// </summary>
		/// <param name="backupFile">The backup file to use.</param>
		/// <param name="copyBuffer">The copy buffer to use.</param>
		public abstract void BackupFromDisk(string backupFile, byte[] copyBuffer);
		/// <summary>
		/// Backs up the database by copying the database memory pool contents.
		/// </summary>
		/// <param name="backupFile">The backup file to use.</param>
		/// <param name="copyBuffer">The copy buffer to use.</param>
		public abstract void BackupFromMpf(string backupFile, byte[] copyBuffer);
		/// <summary>
		/// Compacts the database.
		/// </summary>
		/// <param name="fillPercentage">The targetted fill percentage for pages to be considered for
		/// compaction. If 0 then every page is a candidate</param>
		/// <param name="maxPagesFreed">The maximum number pages freed before halting. If 0 then no limit.</param>
		/// <param name="implicitTxnTimeoutMsecs">The timeout in milliseconds of the implicit
		/// transaction used for the compaction. If 0 then ignored.</param>
		/// <returns>The number of pages freed.</returns>
		public abstract int Compact(int fillPercentage, int maxPagesFreed, int implicitTxnTimeoutMsecs);
		/// <summary>
		/// Deletes an entry.
		/// </summary>
		/// <param name="key">The <see cref="Int32"/> key of the entry to delete.</param>
		/// <returns>A <see cref="DbRetVal"/> specifying the results of the deletion.</returns>
		public abstract DbRetVal Delete(int key);
		/// <summary>
		/// Deletes an entry.
		/// </summary>
		/// <param name="key">The <see cref="String"/> key of the entry to delete.</param>
		public abstract void Delete(string key);
		/// <summary>
		/// Deletes an entry.
		/// </summary>
		/// <param name="key">The <see cref="Byte"/> array key of the entry to delete.</param>
		/// <returns>A <see cref="DbRetVal"/> specifying the results of the deletion.</returns>
		public abstract DbRetVal Delete(byte[] key);
		/// <summary>
		/// Deletes an entry.
		/// </summary>
		/// <param name="key">The <see cref="DatabaseEntry"/> key of the entry to delete.</param>
		public abstract void Delete(DatabaseEntry key);
		/// <summary>
		/// Deletes an entry.
		/// </summary>
		/// <param name="key">The <see cref="DataBuffer"/> key of the entry to delete.</param>
		/// <param name="flags">The <see cref="DeleteOpFlags"/> for the operation.</param>
		/// <returns><see langword="true"/> if the entry existed and was deleted; otherwise
		/// <see langword="false"/>.</returns>
		public abstract bool Delete(DataBuffer key, DeleteOpFlags flags);
		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public abstract void Dispose();
		/// <summary>
		/// Determines whether an entry exists.
		/// </summary>
		/// <param name="key">The <see cref="DataBuffer"/> key of the entry.</param>
		/// <param name="flags">The <see cref="ExistsOpFlags"/> for the operation.</param>
		/// <returns><see cref="DbRetVal.SUCCESS"/> if found; <see cref="DbRetVal.NOTFOUND"/> or
		/// <see cref="DbRetVal.KEYEMPTY"/> otherwise.</returns>
		public abstract DbRetVal Exists(DataBuffer key, ExistsOpFlags flags);
		/// <summary>
		/// Gets entry data.
		/// </summary>
		/// <param name="key">The <see cref="String"/> key of the entry.</param>
		/// <returns>The entry data as a <see cref="String"/>; <see langword="null"/> if entry not found.</returns>
		public abstract string Get(string key);
		/// <summary>
		/// Gets entry data.
		/// </summary>
		/// <param name="key">The <see cref="int"/> key of the entry.</param>
		/// <param name="buffer">The <see cref="Byte"/> array to write the entry data to.</param>
		/// <returns>If found and <paramref name="buffer"/> large enough, then a resizing of
		/// <paramref name="buffer"/> with the entry data; otherwise <see langword="null"/>.</returns>
		public abstract byte[] Get(int key, byte[] buffer);
		/// <summary>
		/// Gets entry data.
		/// </summary>
		/// <param name="key">The <see cref="Byte"/> array key of the entry.</param>
		/// <param name="value">The value.</param>
		/// <returns>If found then data copied to <see cref="DatabaseEntry.Buffer"/> of <paramref name="value"/> and
		/// <see cref="DatabaseEntry.Length"/> set to the entry data length; otherwise no action taken.
		/// <paramref name="value"/> is returned regardless.</returns>
		public abstract DatabaseEntry Get(byte[] key, DatabaseEntry value);
		/// <summary>
		/// Gets entry data.
		/// </summary>
		/// <param name="key">The <see cref="DatabaseEntry"/> key of the entry.</param>
		/// <param name="value">The value.</param>
		/// <returns>If found then data copied to <see cref="DatabaseEntry.Buffer"/> of <paramref name="value"/> and
		/// <see cref="DatabaseEntry.Length"/> set to the entry data length; otherwise no action taken.
		/// <paramref name="value"/> is returned regardless.</returns>
		public abstract DatabaseEntry Get(DatabaseEntry key, DatabaseEntry value);
		/// <summary>
		/// Gets entry data.
		/// </summary>
		/// <param name="key">The <see cref="Int32"/> key of the entry.</param>
		/// <param name="value">The value.</param>
		/// <returns>If found then data copied to <see cref="DatabaseEntry.Buffer"/> of <paramref name="value"/> and
		/// <see cref="DatabaseEntry.Length"/> set to the entry data length; otherwise no action taken.
		/// <paramref name="value"/> is returned regardless.</returns>
		public abstract DatabaseEntry Get(int key, DatabaseEntry value);
		/// <summary>
		/// Gets entry data.
		/// </summary>
		/// <param name="key">The <see cref="DataBuffer"/> key.</param>
		/// <param name="offset">The <see cref="Int32"/> offset. If greater than or equal to 0
		/// then it does a partial read, starting at this offset and of the length of
		/// <paramref name="buffer"/>.</param>
		/// <param name="buffer">The <see cref="DataBuffer"/> that receives the entry data.</param>
		/// <param name="flags">The <see cref="GetOpFlags"/>.</param>
		/// <returns>If found, then the length of the entry. If a partial read was specified, then the entry length
		/// is restricted by the range specified by <paramref name="offset"/> and length of
		/// <paramref name="buffer"/>. Otherwise, a negative value.</returns>
		public abstract int Get(DataBuffer key, int offset, DataBuffer buffer, GetOpFlags flags);
		/// <summary>
		/// Gets entry data.
		/// </summary>
		/// <param name="key">The <see cref="DataBuffer"/> key.</param>
		/// <param name="offset">The <see cref="Int32"/> offset. If greater than or equal to 0
		/// then it does a partial read, starting at this offset and of the length of
		/// <paramref name="length"/>.</param>
		/// <param name="length">The <see cref="Int32"/> length. If greater than or equal to 0
		/// then it does a partial read, starting at this offset and of the length of
		/// <paramref name="length"/>.</param>
		/// <param name="flags">The <see cref="GetOpFlags"/>.</param>
		/// <returns>If found, then a <see cref="Stream"/> containing the entry data, or a portion thereof for
		/// partial reads. Otherwise, <see langword="null"/>.</returns>
		public abstract Stream Get(DataBuffer key, int offset, int length, GetOpFlags flags);
		/// <summary>
		/// Gets entry data.
		/// </summary>
		/// <param name="key">The <see cref="DataBuffer"/> key.</param>
		/// <param name="offset">The <see cref="Int32"/> offset. If greater than or equal to 0
		/// then it does a partial read, starting at this offset and of the length of
		/// <paramref name="length"/>.</param>
		/// <param name="length">The <see cref="Int32"/> length. If greater than or equal to 0
		/// then it does a partial read, starting at this offset and of the length of
		/// <paramref name="length"/>.</param>
		/// <param name="flags">The <see cref="GetOpFlags"/>.</param>
		/// <returns>If found, then a <see cref="Byte"/> array containing the entry data, or a portion thereof for
		/// partial reads. Otherwise, <see langword="null"/>.</returns>
		public abstract byte[] GetBuffer(DataBuffer key, int offset, int length, GetOpFlags flags);
		/// <summary>
		/// Gets the size of the cache.
		/// </summary>
		/// <returns>A <see cref="CacheSize"/> specifying the size.</returns>
		public abstract CacheSize GetCacheSize();
		/// <summary>
		/// Gets the configuration for this instance.
		/// </summary>
		/// <returns>The <see cref="DatabaseConfig"/> used to open this instance.</returns>
		public abstract DatabaseConfig GetDatabaseConfig();
		/// <summary>
		/// Returns an enumerator that iterates through the database records in this instance.
		/// </summary>
		/// <returns>A <see cref="IEnumerator{DatabaseRecord}"/> that can be used to iterate through the
		/// <see cref="DatabaseRecord"/>s in this instance.</returns>
		public IEnumerator<DatabaseRecord> GetEnumerator()
		{
			if (Disposed) yield break;
			using (var cursor = GetCursor())
				while (!Disposed)
				{
					var lens = cursor.Get(DataBuffer.Empty, 0, DataBuffer.Empty, 0, CursorPosition.Next, GetOpFlags.Default);
					var code = lens.KeyLength;
					switch (code)
					{
						case Lengths.NotFound:
							yield break;
						case Lengths.Deleted:
						case Lengths.KeyExists:
							continue;
						default:
							if (code < 0)
							{
								throw new ApplicationException(string.Format(
									"Unrecognized cursor return value on database enumeration move next {0}", code));								
							}
							break;
					}
					var ret = cursor.GetBuffers(DataBuffer.Empty, 0, -1, CursorPosition.Current, GetOpFlags.Default);
					code = ret.ReturnCode;
					switch (code)
					{
						case Lengths.NotFound:
							yield break;
						case Lengths.Deleted:
						case Lengths.KeyExists:
							break;
						case (int)DbRetVal.SUCCESS:
							yield return new DatabaseRecord(new DatabaseEntry(ret.KeyBuffer),
								new DatabaseEntry(ret.ValueBuffer));
							break;
						default:
							throw new ApplicationException(string.Format(
								"Unrecognized cursor return value on database enumeration read {0}", code));
					}
				}
		}
		/// <summary>
		/// Returns an enumerator that iterates through a collection.
		/// </summary>
		/// <returns>
		/// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
		/// </returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
		/// <summary>
		/// Gets the error prefix.
		/// </summary>
		/// <returns>The <see cref="String"/> error prefix.</returns>
		public abstract string GetErrorPrefix();
		/// <summary>
		/// Gets the active flags.
		/// </summary>
		/// <returns>The current <see cref="DbFlags"/> of the instance.</returns>
		public abstract DbFlags GetFlags();
		/// <summary>
		/// Gets the hash fill factor.
		/// </summary>
		/// <returns>The current <see cref="Int32"/> hash fill factor.</returns>
		public abstract int GetHashFillFactor();
		/// <summary>
		/// Gets the number of keys in this instance.
		/// </summary>
		/// <param name="statFlag">The <see cref="DbStatFlags"/> that specifies the type of
		/// statistics scan.</param>
		/// <returns>The <see cref="Int32"/> number of kiets.</returns>
		public abstract int GetKeyCount(DbStatFlags statFlag);
		/// <summary>
		/// Gets the entry data length.
		/// </summary>
		/// <param name="key">The <see cref="DataBuffer"/> key.</param>
		/// <param name="flags">The <see cref="GetOpFlags"/>.</param>
		/// <returns>The length of the entry data; a negative value if not found.</returns>
		public abstract int GetLength(DataBuffer key, GetOpFlags flags);
		/// <summary>
		/// Gets the active open flags.
		/// </summary>
		/// <returns>The current <see cref="DbOpenFlags"/> of the instance.</returns>
		public abstract DbOpenFlags GetOpenFlags();
		/// <summary>
		/// Gets the size of the database pages.
		/// </summary>
		/// <returns>The <see cref="Int32"/> size in bytes of the pages.</returns>
		public abstract int GetPageSize();
		/// <summary>
		/// Gets the length of the queue records.
		/// </summary>
		/// <returns>The <see cref="Int32"/> length in bytes of the records for queue databases.</returns>
		public abstract int GetRecordLength();
		/// <summary>
		/// Gets the type of the database.
		/// </summary>
		/// <returns>A <see cref="DatabaseType"/> specifying the type of database.</returns>
		public abstract DatabaseType GetDatabaseType();
		/// <summary>
		/// Prints the default statistics.
		/// </summary>
		/// <param name="statFlags"><see cref="DbStatFlags"/> specifying the options, such as fast (only
		/// statistics reported that don't require scanning the database), or complete.</param>
		/// <remarks>Statistics are printed as a message output to
		/// <see cref="BerkeleyDbWrapper.Environment.MessageCall"/> of <see cref="Environment"/>.</remarks>
		public abstract void PrintStats(DbStatFlags statFlags);
		/// <summary>
		/// Writes entry data.
		/// </summary>
		/// <param name="key">The <see cref="DatabaseEntry"/> key.</param>
		/// <param name="value">The <see cref="DatabaseEntry"/> data.</param>
		public abstract void Put(DatabaseEntry key, DatabaseEntry value);
		/// <summary>
		/// Writes entry data.
		/// </summary>
		/// <param name="key">The <see cref="Byte"/> array key.</param>
		/// <param name="value">The <see cref="DatabaseEntry"/> data.</param>
		public abstract void Put(byte[] key, DatabaseEntry value);
		/// <summary>
		/// Writes entry data.
		/// </summary>
		/// <param name="key">The <see cref="Byte"/> array key.</param>
		/// <param name="value">The <see cref="Byte"/> array data.</param>
		public abstract void Put(byte[] key, byte[] value);
		/// <summary>
		/// Writes entry data.
		/// </summary>
		/// <param name="key">The <see cref="Int32"/> key.</param>
		/// <param name="value">The <see cref="Byte"/> array data.</param>
		public abstract void Put(int key, byte[] value);
		/// <summary>
		/// Writes entry data.
		/// </summary>
		/// <param name="key">The <see cref="Int32"/> key.</param>
		/// <param name="value">The <see cref="DatabaseEntry"/> data.</param>
		public abstract void Put(int key, DatabaseEntry value);
		/// <summary>
		/// Writes entry data.
		/// </summary>
		/// <param name="key">The <see cref="String"/> key.</param>
		/// <param name="value">The <see cref="String"/> data.</param>
		public abstract void Put(string key, string value);
		/// <summary>
		/// Writes entry data.
		/// </summary>
		/// <param name="objectId">The <see cref="Int32"/> federating id.</param>
		/// <param name="key">The <see cref="Byte"/> array key.</param>
		/// <param name="dbEntry">A <see cref="DatabaseEntry"/> used to write data to.</param>
		/// <param name="rmwDelegate">Called after the initial read with <paramref name="dbEntry"/> as the
		/// parameter. The length of <paramref name="dbEntry"/> is set to the length of the data, 0 if not
		/// found. Before return, set the length of the parameter to the length of the new data value; 0 to
		/// delete.</param>
		public abstract void Put(int objectId, byte[] key, DatabaseEntry dbEntry, RMWDelegate rmwDelegate);
		/// <summary>
		/// Writes entry data.
		/// </summary>
		/// <param name="key">The <see cref="DataBuffer"/> key.</param>
		/// <param name="offset">The <see cref="Int32"/> offset. If greater than or equal to 0, then a partial
		/// write is done and this specifies the start of the portion overwritten.</param>
		/// <param name="count">The <see cref="Int32"/> length of the entry portion overwritten for partial writes. </param>
		/// <param name="buffer">The <see cref="DataBuffer"/> containing the data to be written.</param>
		/// <param name="flags">The <see cref="PutOpFlags"/> for this operation.</param>
		/// <returns>The length of the data written.</returns>
		public abstract int Put(DataBuffer key, int offset, int count, DataBuffer buffer, PutOpFlags flags);
		/// <summary>
		/// Flushes any cached changes to the database.
		/// </summary>
		public abstract void Sync();
		/// <summary>
		/// Removes all entries from this instance.
		/// </summary>
		/// <returns>The <see cref="Int32"/> number of entries that were removed.</returns>
		public abstract int Truncate();
		/// <summary>
		/// Creates a cursor that can traverse the database's records.
		/// </summary>
		/// <returns>The new <see cref="Cursor"/>.</returns>
		public abstract Cursor GetCursor();
		/// <summary>
		/// Implementation for <see cref="Remove"/> that uses pseudosingleton.
		/// </summary>
		/// <param name="env">The <see cref="Environment"/> from which to remove a database.</param>
		/// <param name="fileName">Path to the database file.</param>
		protected abstract void DoRemove(Environment env, string fileName);
		/// <summary>
		/// Implementation for <see cref="Verify"/> that uses pseudosingleton.
		/// </summary>
		/// <param name="fileName">Path to the database file to verify.</param>
		/// <returns>A <see cref="DbRetVal"/> of the verification results. <see cref="DbRetVal.SUCCESS"/> indicates
		/// no issues, other values returned otherwise.</returns>
		protected abstract DbRetVal DoVerify(string fileName);
		// static methods
		/// <summary>
		/// Removes a database.
		/// </summary>
		/// <param name="env">The <see cref="Environment"/> from which to remove a database.</param>
		/// <param name="fileName">Path to the database file.</param>
		public static void Remove(Environment env, string fileName)
		{
			_pseudoSingleton.DoRemove(env, fileName);
		}
		/// <summary>
		/// Verifies a database.
		/// </summary>
		/// <param name="fileName">Path to the database file to verify.</param>
		/// <returns>A <see cref="DbRetVal"/> of the verification results. <see cref="DbRetVal.SUCCESS"/> indicates
		/// no issues, other values returned otherwise.</returns>
		public static DbRetVal Verify(string fileName)
		{
			return _pseudoSingleton.DoVerify(fileName);
		}
		/// <summary>
		/// Creates a database.
		/// </summary>
		/// <param name="config">The <see cref="DatabaseConfig"/> of the database to create.</param>
		/// <returns>The new <see cref="Database"/>.</returns>
		/// <remarks><see cref="Environment"/> will be <see langword="null"/> for this instace.</remarks>
		public static Database Create(DatabaseConfig config)
		{
			return ConcreteFactory.Create<DatabaseConfig, Database>(config);
		}

		// Properties
		/// <summary>
		/// Gets a value indicating whether this <see cref="Database"/> has been disposed.
		/// </summary>
		/// <value>
		/// 	<see langword="true"/> if disposed; otherwise, <see langword="false"/>.
		/// </value>
		public abstract bool Disposed { get; }
		/// <summary>
		/// Gets the environment this instance is under.
		/// </summary>
		/// <value>The <see cref="Environment"/> this instance was created under. If this instance was created
		/// without a specified <see cref="Environment"/>, then value is <see langword="null"/>.</value>
		public abstract Environment Environment { get; }
		/// <summary>
		/// Gets the maximum number of deadlock retries.
		/// </summary>
		/// <value>The <see cref="Int32"/> Gets the maximum number of deadlock retries.</value>
		public abstract int MaxDeadlockRetries { get; }
	}
}
