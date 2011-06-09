using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Xml.Serialization;

using BerkeleyDb;

namespace MySpace.DataRelay.RelayComponent.Bdb
{
    [XmlRoot("DatabaseConfig", Namespace = "http://myspace.com/DatabaseConfig.xsd")]
    public class DatabaseConfig
    {
        private string adminDbName = "admin.bdb";
        private DbCreateFlags createFlags = DbCreateFlags.None;
        private string errorPrefix = "BdbDB";
        private string fileName = "data.bdb";
        private short id;
        private Db.OpenFlags openFlags = Db.OpenFlags.Create;//DB_CREATE|DB_DIRTY_READ|DB_THREAD
        private DbOpenFlagCollection dbOpenFlagCollection;
        private bool openFlagsInitialized = false;
        //private DbType dbType = DbType.Hash;


        private void InitOpenFlags()
        {
            if (this.dbOpenFlagCollection == null)
            {
                return;
            }
            foreach (DatabaseOpenFlags flag in this.dbOpenFlagCollection)
            {
                switch (flag)
                {
                    case DatabaseOpenFlags.AutoCommit:
                        this.openFlags = this.openFlags | Db.OpenFlags.AutoCommit;
                        break;
                    case DatabaseOpenFlags.Create:
                        this.openFlags = this.openFlags | Db.OpenFlags.Create;
                        break;
                    case DatabaseOpenFlags.DirtyRead:
                        this.openFlags = this.openFlags | Db.OpenFlags.DirtyRead;
                        break;
                    case DatabaseOpenFlags.Exclusive:
                        this.openFlags = this.openFlags | Db.OpenFlags.Exclusive;
                        break;
                    case DatabaseOpenFlags.NoMemoryMap:
                        this.openFlags = this.openFlags | Db.OpenFlags.NoMemoryMap;
                        break;
                    case DatabaseOpenFlags.None:
                        this.openFlags = this.openFlags | Db.OpenFlags.None;
                        break;
                    case DatabaseOpenFlags.ReadOnly:
                        this.openFlags = this.openFlags | Db.OpenFlags.ReadOnly;
                        break;
                    case DatabaseOpenFlags.ThreadSafe:
                        this.openFlags = this.openFlags | Db.OpenFlags.ThreadSafe;
                        break;
                    case DatabaseOpenFlags.Truncate:
                        this.openFlags = this.openFlags | Db.OpenFlags.Truncate;
                        break;
                   default:
                       throw new ApplicationException("Unknown Db.OpenFlag '" + flag + "'");
                }
            }

        }

        public DatabaseConfig()
        {
        }

        public DatabaseConfig(short id)
        {
            this.id = id;
        }

        [XmlIgnore]
        public DbCreateFlags CreateFlags 
        { 
            get 
            {
                //if (!createFlagsInitialized)
                //{
                //    InitCreateFlags();
                //    createFlagsInitialized = true;
                //}
                return createFlags; 
            } 
            set 
            { 
                createFlags = value; 
            } 
        }


        public string ErrorPrefix { get { return errorPrefix; } set { errorPrefix = value; } }

        public string FileName
        {
            get
            {
                if (id == -1)
                {
                    return adminDbName;
                }
                return fileName + id.ToString();
            }
            set
            {
                fileName = value;
            }
        }

        [XmlAttribute("Id")]
        public short Id { get { return id; } set { id = value; } }

        [XmlIgnore]
        public Db.OpenFlags OpenFlags 
        { 
            get 
            {
                if (!openFlagsInitialized)
                {
                    InitOpenFlags();
                    openFlagsInitialized = true;
                }
                return openFlags; 
            } 
            set 
            { 
                openFlags = value; 
            } 
        }

        [XmlArray("OpenFlags")]
        [XmlArrayItem("OpenFlag")]
        public DbOpenFlagCollection DbOpenFlagCollection
        {
            get
            {
                return this.dbOpenFlagCollection;
            }
            set
            {
                this.dbOpenFlagCollection = value;
            }
        }

        //[XmlIgnore]
        //public DbType Type { get { return dbType; } set { dbType = value; } }


        public DatabaseConfig Clone(short id)
        {
            DatabaseConfig newDbConfig = new DatabaseConfig(id);
            newDbConfig.CreateFlags = this.createFlags;
            newDbConfig.ErrorPrefix = this.errorPrefix;
            newDbConfig.FileName = this.fileName;
            newDbConfig.OpenFlags = this.openFlags;
            newDbConfig.DbOpenFlagCollection = this.dbOpenFlagCollection;
            return newDbConfig;
        }
    }

    //public class DbCreateFlagCollection : KeyedCollection<string, DatabaseCreateFlags>
    //{
    //    protected override string GetKeyForItem(DatabaseCreateFlags item)
    //    {
    //        return item.ToString();
    //    }

    //    public string GetGroupNameForId(string id)
    //    {
    //        if (this.Contains(id))
    //        {
    //            return this[id].ToString();
    //        }
    //        else
    //        {
    //            return null;
    //        }
    //    }
    //}

    public class DbOpenFlagCollection : KeyedCollection<string, DatabaseOpenFlags>
    {
        protected override string GetKeyForItem(DatabaseOpenFlags item)
        {
            return item.ToString();
        }

        public string GetGroupNameForId(string id)
        {
            if (this.Contains(id))
            {
                return this[id].ToString();
            }
            else
            {
                return null;
            }
        }
    }

    /// <remarks/>
    //[System.SerializableAttribute()]
    //[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://myspace.com/DatabaseConfig.xsd")]
    //public enum DatabaseCreateFlags
    //{

    //    /// <remarks/>
    //    None,

    //    /// <remarks/>
    //    RepCreate,

    //    /// <remarks/>
    //    XACreate,
    //}

    /// <remarks/>
    [System.FlagsAttribute()]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://myspace.com/DatabaseConfig.xsd")]
    public enum DatabaseOpenFlags
    {

        /// <remarks/>
        None = 1,

        /// <remarks/>
        Create = 2,

        /// <remarks/>
        NoMemoryMap = 4,

        /// <remarks/>
        ReadOnly = 8,

        /// <remarks/>
        ThreadSafe = 16,

        /// <remarks/>
        Truncate = 32,

        /// <remarks/>
        Exclusive = 64,

        /// <remarks/>
        AutoCommit = 128,

        /// <remarks/>
        DirtyRead = 256,
    }

    ///// <remarks/>
    //[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "2.0.50727.42")]
    //[System.SerializableAttribute()]
    //[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://myspace.com/DatabaseConfig.xsd")]
    //public enum DbType
    //{

    //    /// <remarks/>
    //    BTree,

    //    /// <remarks/>
    //    Hash,

    //    /// <remarks/>
    //    Recno,

    //    /// <remarks/>
    //    Queue,

    //    /// <remarks/>
    //    Unknown,
    //}
}
