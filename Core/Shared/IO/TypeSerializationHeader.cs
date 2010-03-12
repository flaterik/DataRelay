using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.Common.IO
{

/// <summary>
/// Internal use only
/// </summary>
[Flags]
public enum TypeSerializationHeaderFlags : byte
{
    Default                 = 0,
    
    KnownFlags              = Default
}

/// <summary>
/// Internal use only
/// </summary>
public class TypeSerializationHeader
{
    const int CurrentHeaderVersion = 1;
    
    byte    headerVersion = CurrentHeaderVersion;
    short   headerLength = 0;
    long    headerPosition = -1;
    byte    dataVersion = 0;
    byte    dataMinVersion = 0;
    int     dataLength = 0;
    long    dataPosition = -1;
    TypeSerializationHeaderFlags flags = TypeSerializationHeaderFlags.Default;
    
    /// <summary>
    /// The version of this header
    /// </summary>
    public byte HeaderVersion
    {
        get { return this.headerVersion; }
    }

    /// <summary>
    /// The absolute position of the header in the data stream
    /// </summary>
    public long HeaderPosition
    {
        get { return this.headerPosition; }
    }
    
    /// <summary>
    /// Gets or sets the current version of the serialized object.
    /// </summary>
    public byte DataVersion
    {
        get { return this.dataVersion; }
        set { this.dataVersion = value; }
    }
    
	/// <summary>
	/// Gets the Minimum Version of the serialized object.
	/// </summary>
    public byte DataMinVersion
    {
        get { return this.dataMinVersion; }
        internal set { this.dataMinVersion = value; }
    }

    /// <summary>
    /// The length in bytes of the serialized object
    /// </summary>
    public int DataLength
    {
        get { return this.dataLength; }
        set { this.dataLength = value; }
    }
    
    /// <summary>
    /// The absolute position of the serialized object in the data stream
    /// </summary>
    public long DataPosition
    {
        get { return this.dataPosition; }
    }
    
    /// <summary>
    /// Writes or updates the serialized header
    /// </summary>
    /// <param name="writer"></param>
    public void Write(IPrimitiveWriter writer)
    {
        long    oldPosition = writer.BaseStream.Position;
        bool    firstWrite = this.headerPosition < 0;
        
        //  Assumes the first time this header is written that
        //  we're adding it to the stream with some data missing
        //  Subsequent writes are intended to update the header
        //  with new info such as the actual data length
        if (firstWrite)
        {
            this.headerPosition = oldPosition;
        }
        else
        {
            writer.BaseStream.Seek(this.headerPosition, System.IO.SeekOrigin.Begin);
        }
        
        writer.Write(this.headerVersion);
        writer.Write(this.headerLength);
        writer.Write((byte)this.flags);
        writer.Write(this.dataVersion);
        writer.Write(this.dataMinVersion);
        writer.Write(this.dataLength);
        
        if (firstWrite)
        {
            this.dataPosition = writer.BaseStream.Position;
            this.headerLength = (short)(this.dataPosition - this.headerPosition);
        }
        else
        {
            writer.BaseStream.Seek(oldPosition, System.IO.SeekOrigin.Begin);
        }
    }
    
    /// <summary>
    /// Reads the serialized header
    /// </summary>
    /// <param name="reader"></param>
    public void Read(IPrimitiveReader reader)
    {
        this.headerPosition = reader.BaseStream.Position;
        this.headerVersion = reader.ReadByte();
        this.headerLength = reader.ReadInt16();
        this.flags = (TypeSerializationHeaderFlags)reader.ReadByte();
        this.dataVersion = reader.ReadByte();
        this.dataMinVersion = reader.ReadByte();
        this.dataLength = reader.ReadInt32();
        this.dataPosition = this.headerPosition + this.headerLength;
        
        if (this.headerVersion > CurrentHeaderVersion)
        {
            throw new ApplicationException("This object was serialized with a newer version of the serialization framework");
        }
        if ((this.flags & ~TypeSerializationHeaderFlags.KnownFlags) != 0)
        {
            throw new ApplicationException("This object was serialized with features that are not supported in this version of the serialization framework");
        }
        
        reader.BaseStream.Seek(this.dataPosition, System.IO.SeekOrigin.Begin);
    }
    
    /// <summary>
    /// Set the data length based on the data start position and
    /// current stream position
    /// </summary>
    /// <param name="writer"></param>
    public void UpdateDataLength(IPrimitiveWriter writer)
    {
        this.dataLength = (int)(writer.BaseStream.Position - this.dataPosition);
    }
}
}
