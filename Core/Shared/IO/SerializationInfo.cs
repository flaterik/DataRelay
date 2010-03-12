using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace MySpace.Common.IO
{
    public class SerializationInfo
    {
        int         version = 0;
        int         minVersion = 0;
        byte[]      unhandledData = null;
        IList<TypeNameTable.TypeInfo> unhandledTypeNames = null;
        
        internal SerializationInfo()
        {
        }
        
        public int Version
        {
            get { return this.version; }
            internal set { Debug.Assert(value <= byte.MaxValue); this.version = value; }
        }
        
        public int MinVersion
        {
            get { return this.minVersion; }
            internal set { Debug.Assert(value <= byte.MaxValue); this.minVersion = value; }
        }
        
        public byte[] UnhandledData
        {
            get { return this.unhandledData; }
            internal set { this.unhandledData = value; }
        }
        
        public IList<TypeNameTable.TypeInfo> UnhandledTypeNames
        {
            get { return this.unhandledTypeNames; }
            internal set { this.unhandledTypeNames = value; }
        }
        
        public SerializationInfo BaseClassInfo
        {
            get; internal set;
        }
    }
    
    public interface ISerializationInfo
    {
        SerializationInfo SerializationInfo { get; set; }
    }
}
