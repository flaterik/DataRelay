using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace BerkeleyDbWrapper
{
public class DatabaseRecord : IDisposable
{
    // Methods
    public DatabaseRecord()
    {
        Key = null;
        Value = null;
    }

	public DatabaseRecord(DatabaseEntry key, DatabaseEntry value)
	{
		Key = key;
		Value = value;
	}

    public DatabaseRecord(int nKeyCapacity, int nValueCapacity)
    {
        Key = new DatabaseEntry(nKeyCapacity);
        Value = new DatabaseEntry(nValueCapacity);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

	~DatabaseRecord()
	{
		Dispose(false);
	}

    protected virtual void Dispose(bool disposing)
    {
    }

    // Properties
	public DatabaseEntry Key { get; set; }

    public DatabaseEntry Value { get; set; }
}

}
