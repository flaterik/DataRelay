using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.Common.IO
{
    /// <summary>
    /// Internal use only
    /// </summary>
    public class TypeSerializationArgs
    {
        public IPrimitiveWriter         Writer = null;
        public IPrimitiveReader         Reader = null;
        public SerializerFlags          Flags = SerializerFlags.Default;
        public TypeNameTable            NameTable = null;
        public TypeSerializationHeader  Header = null;
        public SerializationInfo        SerializationInfo = null;
        public bool                     Succeeded = true;        
        public bool                     IsBaseClass = false;
        
        /// <summary>
        /// Called by serializer when recursing to base classes or
        /// class member variables to duplicate the context args of
        /// the caller so caller isn't unexpectedly affected by logic
        /// in the child processing
        /// </summary>
        /// <returns></returns>
        public TypeSerializationArgs Clone()
        {
            TypeSerializationArgs clone = new TypeSerializationArgs();
            
            clone.Writer = this.Writer;
            clone.Reader = this.Reader;
            clone.Flags = this.Flags;
            clone.NameTable = this.NameTable;            
            clone.SerializationInfo = this.SerializationInfo;
            
            //  IsBaseClass
            //  Indicates that the serialization call is being made to
            //  automatically serialize a base class. This needs to be
            //  cleared at each level so that it isn't propagated to
            //  the processing of the base class' properties so we don't
            //  retain the value from the source args.            
            
            //  Succeeded
            //  Success is determined at each level and shouldn't be
            //  affected by caller state. This technically shouldn't
            //  matter because the caller should have stopped processing
            //  if an error occurred.
            
            return clone;
        }
    }
}
