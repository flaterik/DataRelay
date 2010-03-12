using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.Common
{
    /// <summary>
    /// Informs the serializer that a property or field from a base class
    /// should be included in the serialized data for this class
    /// </summary>
    /// <example>
    /// [SerializableClass]
    /// [SerializableInheritedProperty(1, typeof(BaseClass), "BaseProperty")]
    /// class MyClass : BaseClass
    /// {
    ///     [SerializableProperty(2)]
    ///     public int MyProperty
    ///     {
    ///         get { return this.property; }
    ///         set { this.property = value; }
    ///     }
    /// }
    /// </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=true, Inherited=false)]
    public sealed class SerializableInheritedPropertyAttribute : SerializablePropertyAttribute
    {
        #region Members
        Type    baseClass = null;
        string  basePropertyName = null;
        #endregion
        
        #region Construction
        public SerializableInheritedPropertyAttribute(int version, Type baseClass, string basePropertyName) : base(version)
        {
            this.baseClass = baseClass;
            this.basePropertyName = basePropertyName;
        }
        #endregion
        
        #region Properties
        
        public Type BaseClass
        {
            get { return this.baseClass; }
        }
        
        public string BasePropertyName
        {
            get { return this.basePropertyName; }
        }
        
        #endregion
    }
}
