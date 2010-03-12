using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.Common
{
    /// <summary>
    /// Declares serialization information for a struct
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public class SerializableStructAttribute : Attribute
    {
        Type            targetType = null;
        
        /// <summary>
        /// Declares serialization information for a struct
        /// </summary>
        /// <param name="targetType">A primitive type to serialize the struct as</param>
        public SerializableStructAttribute(Type targetType)
        {
            this.targetType = targetType;
        }
        
        /// <summary>
        /// A primitive type to serialize the struct as
        /// </summary>
        public Type TargetType
        {
            get { return this.targetType; }
        }
    }
}
