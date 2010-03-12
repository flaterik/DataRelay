using System;
using System.Collections;
using System.Collections.Generic;
using BerkeleyDbWrapper;
using MySpace.Logging;

namespace MySpace.BerkeleyDb.Facade
{
	public class BDBStorageEnum : IEnumerator<Database>
	{
		readonly IEnumerator<Database> dbIterator;
		readonly private static LogWrapper Log = new LogWrapper();
		readonly IList<Database> dbList = new List<Database>();

		public BDBStorageEnum(Database[,] databases)
		{
			try
			{
				foreach (Database db in databases)
				{
					if (db != null)
					{
						dbList.Add(db);
					}
				}
				dbIterator = dbList.GetEnumerator();
			}
			catch (Exception ex)
			{
				if (Log.IsErrorEnabled)
				{
					Log.Error("BDBStorageEnum() Error in Constructor", ex);
				}
				throw;
			}
		}

		public bool MoveNext()
		{
			return dbIterator.MoveNext();
		}

		public void Reset()
		{
			dbIterator.Reset();
		}

		public Database Current
		{
			get
			{
				return dbIterator.Current;
			}
		}

		object IEnumerator.Current
		{
			get
			{
				return Current;
			}
		}

		public void Dispose()
		{
			if (dbIterator != null)
			{
				dbIterator.Dispose();
			}
		}
	};
}
