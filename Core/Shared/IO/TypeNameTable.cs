using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace MySpace.Common.IO
{
/// <summary>
/// This type is only intended to be used internally by the serializer.
/// </summary>
public class TypeNameTable
{
    #region Helper Classes
    struct TypeData
    {
        public byte     TypeIndex;
        public byte     AssemblyIndex;
        public byte     NamespaceIndex;
        public string   TypeName;
        /// <summary>
        /// Specifies the highest version of a property
        /// that needs this type info. This is used when
        /// dealing with forward-compatibility issues
        /// </summary>
        public byte     Version;
    }
    
    class NameList : List<string>
    {
        public new byte Add(string val)
        {
            if (base.Contains(val) == false)
            {
                base.Add(val);
                return (byte)(base.Count - 1);
            }
            else
            {
                return (byte)base.IndexOf(val);
            }
        }
    }
    
    public class TypeInfo
    {
        string  typeName = null;
        Type    type = null;
        byte    version = 0;
        
        public string TypeName
        {
            get { return this.typeName; }
            internal set { this.typeName = value; }
        }
        
        public Type Type
        {
            get { return this.type; }
            internal set { this.type = value; }
        }
        
        public byte Version
        {
            get { return this.version; }
            internal set { this.version = value; }
        }
    }
    #endregion
    
    #region Constants
    //***************************************************************
    //
    //  Constants
    //
    //***************************************************************

    const short                 Signature = unchecked((short)0xABCD);
    const int                   HeaderLength = sizeof(ushort)*2;
    #endregion

    #region Fields
    //***************************************************************
    //
    //  Fields
    //
    //***************************************************************

    TypeInfo[]                      resolvedTypeTable = null;
    NameList                        assemblyTable = null;
    NameList                        namespaceTable = null;
    Dictionary<string, TypeData>    typeTable = null;
    
    #endregion
    
    #region Construction
    
    public TypeNameTable()
    {
    }
    
    #endregion
    
    #region Methods

    public void GetTypeInfo(byte typeIndex, out Type type, out string typeName)
    {
        TypeInfo    typeInfo = resolvedTypeTable[typeIndex];
        
        type = typeInfo.Type;
        typeName = typeInfo.TypeName;
    }
    
    public void SetResolvedType(byte typeIndex, Type type)
    {
        resolvedTypeTable[typeIndex].Type = type;
    }
    
    //***************************************************************
    //
    //  Add
    //
    /// <summary>
    /// Adds a type to the type table
    /// </summary>
    /// <param name="t">The type to add</param>
    /// <returns>The index of the type in the table</returns>
    public byte Add(Type t, int propertyVersion)
    {
        string  assemblyName = t.Assembly.GetName().Name;
        string  namespaceName = t.Namespace;
        string  typeName = t.FullName.Substring(t.Namespace.Length + 1);
        string  fullName = string.Format("{0}.{1},{2}", namespaceName, typeName, assemblyName);
        
        return Add(fullName, typeName, namespaceName, assemblyName, propertyVersion);
    }
    
    //***************************************************************
    //
    //  Add
    //
    /// <summary>
    /// Adds names from cached serialization info
    /// </summary>
    /// <param name="info"></param>
    public void Add(SerializationInfo info)
    {
        if ((info != null) && (info.UnhandledTypeNames != null))
        {
            int     index = 0;
            string  assemblyName = null;
            string  namespaceName = null;
            string  typeName = null;
            
            foreach (TypeInfo t in info.UnhandledTypeNames)
            {
                index = t.TypeName.LastIndexOf(',');
                assemblyName = t.TypeName.Substring(index+1);
                namespaceName = t.TypeName.Remove(index);
                index = namespaceName.LastIndexOf('.');
                typeName = namespaceName.Substring(index+1);
                namespaceName = namespaceName.Remove(index);
                
                Add(t.TypeName, typeName, namespaceName, assemblyName, t.Version);
            }
        }
    }
    
    //***************************************************************
    //
    //  Add
    //
    byte Add(string fullName, string typeName, string namespaceName, string assemblyName, int propertyVersion)
    {
        TypeData data;
        
        if ((this.typeTable == null) || (this.typeTable.ContainsKey(fullName) == false))
        {
            data = new TypeData();

            if (this.typeTable == null)
            {
                this.assemblyTable = new NameList();
                this.namespaceTable = new NameList();
                this.typeTable = new Dictionary<string,TypeData>();
            }
            
            data.AssemblyIndex = this.assemblyTable.Add(assemblyName);
            data.NamespaceIndex = this.namespaceTable.Add(namespaceName);
            data.TypeIndex = (byte)this.typeTable.Count;
            data.TypeName = typeName;
            data.Version = (byte)propertyVersion;
            
            this.typeTable.Add(fullName, data);
        }
        else
        {
            data = this.typeTable[fullName];
            
            if (propertyVersion > data.Version)
            {
                data.Version = (byte)propertyVersion;
            }
        }
        
        return data.TypeIndex;
    }
    
    //***************************************************************
    //
    //  Serialize
    //
    /// <summary>
    /// Serializes the name table if it contains any entries
    /// </summary>
    /// <param name="writer"></param>
    public void Serialize(IPrimitiveWriter writer)
    {
        if (this.typeTable == null) return;
        
        long    startPosition = writer.BaseStream.Position;
        
        writer.Write((byte)this.assemblyTable.Count);
        foreach (string assemblyName in this.assemblyTable)
        {
            writer.Write(assemblyName);
        }
        
        writer.Write((byte)this.namespaceTable.Count);
        foreach (string namespaceName in this.namespaceTable)
        {
            writer.Write(namespaceName);
        }
        
        writer.Write((byte)this.typeTable.Count);
        foreach (TypeData data in this.typeTable.Values)
        {
            writer.Write(data.Version);
            writer.Write(data.AssemblyIndex);
            writer.Write(data.NamespaceIndex);
            writer.Write(data.TypeName);
        }
        
        writer.Write((short)(writer.BaseStream.Position - startPosition));
        writer.Write(Signature);
    }
    
    //***************************************************************
    //
    //  Deserialize
    //
    /// <summary>
    /// Determines if the stream contains a name table and loads it if it exists
    /// </summary>
    /// <param name="reader"></param>
    public void Deserialize(IPrimitiveReader reader, TypeSerializationHeader header)
    {
        long        oldPosition = reader.BaseStream.Position;
        long        endPosition = 0;
        short       size = 0;
        short       signature = 0;
        byte        assemblyCount = 0;
        byte        typeCount = 0;
        byte        index = 0;
        string      assemblyName = null;
        string[]    assemblyNames = null;
        string      typeName = null;
        string      namespaceName = null;
        string[]    namespaceNames = null;
        byte        namespaceCount = 0;
        
        //  Determine if the stream contains a name table        
        if ((reader.BaseStream.Length - oldPosition) < HeaderLength)
        {
            return;
        }

        endPosition = reader.BaseStream.Seek(-HeaderLength, System.IO.SeekOrigin.End);
        size = reader.ReadInt16();
        signature = reader.ReadInt16();
        
        if (signature == Signature)
        {
            reader.BaseStream.Seek(endPosition - size, System.IO.SeekOrigin.Begin);
            
            //  Load assembly names
            assemblyCount = reader.ReadByte();
            assemblyNames = new string[assemblyCount];
            for (index = 0; index < assemblyCount; index++)
            {
                assemblyNames[index] = reader.ReadString();
            }
            
            //  Load namespace names
            namespaceCount = reader.ReadByte();
            namespaceNames = new string[namespaceCount];
            for (index = 0; index < namespaceCount; index++)
            {
                namespaceNames[index] = reader.ReadString();
            }
            
            //  Load types
            typeCount = reader.ReadByte();
            this.resolvedTypeTable = new TypeInfo[typeCount];
            for (index = 0; index < typeCount; index++)
            {
                TypeInfo rti = new TypeInfo();
                
                if ((header != null) && (header.HeaderVersion >= 1))
                {
                    rti.Version = reader.ReadByte();
                }
                
                assemblyName = assemblyNames[reader.ReadByte()];
                namespaceName = namespaceNames[reader.ReadByte()];
                typeName = reader.ReadString();
                rti.TypeName = string.Format("{1}.{0},{2}", typeName, namespaceName, assemblyName);
                                                            
                this.resolvedTypeTable[index] = rti;
            }
        }
        
        //  Reset stream position
        reader.BaseStream.Seek(oldPosition, System.IO.SeekOrigin.Begin);
    }
    
    public void GetSerializationInfo(SerializationInfo info, int version)
    {
        List<TypeInfo>  types = new List<TypeInfo>();
        
        if (this.resolvedTypeTable != null)
        {
            foreach (TypeInfo rti in this.resolvedTypeTable)
            {
                if (rti.Version > version)
                    types.Add(rti);
            }
        }
        
        if (types.Count > 0)
        {
            info.UnhandledTypeNames = types;
        }
    }

    public override string ToString()
    {
        return base.ToString();
    }
    
    #endregion //Methods
}
}
