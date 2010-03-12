using System;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    [Obsolete("This class is obsolete; use Condition class instead", true)]
    public class Criterion : IVersionSerializable
    {
        #region Data Members
        private string fieldName;
        public string FieldName
        {
            get
            {
                return fieldName;
            }
            set
            {
                fieldName = value;
            }
        }

        private bool isTag;
        public bool IsTag
        {
            get
            {
                return isTag;
            }
            set
            {
                isTag = value;
            }
        }

        private Operation operation;
        public Operation Operation
        {
            get
            {
                return operation;
            }
            set
            {
                operation = value;
            }
        }

        private byte[] value;
        public byte[] Value
        {
            get
            {
                return this.value;
            }
            set
            {
                this.value = value;
            }
        }

        private DataType dataType;
        public DataType DataType
        {
            get
            {
                return dataType;
            }
            set
            {
                dataType = value;
            }
        }
        #endregion

        #region Ctors
        public Criterion()
        {
            Init(null, true, Operation.Equals, null, DataType.Int32);
        }

        public Criterion(string fieldName, bool isTag, Operation operation, byte[] value, DataType dataType)
        {
            Init(fieldName, isTag, operation, value, dataType);
        }

        private void Init(string fieldName, bool isTag, Operation operation, byte[] value, DataType dataType)
        {
            this.fieldName = fieldName;
            this.isTag = isTag;
            this.operation = operation;
            this.value = value;
            this.dataType = dataType;
        }
        #endregion

        #region IVersionSerializable Members
        public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
        {
            //FieldName
            writer.Write(fieldName);

            //IsTag
            writer.Write(isTag);

            //Operation
            writer.Write((byte)operation);

            //Value
            if (value == null || value.Length == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)value.Length);
                writer.Write(value);
            }

            //DataType
            writer.Write((byte)dataType);
        }
        
        public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
        {
            Deserialize(reader);
        }

        public int CurrentVersion
        {
            get
            {
                return 1;
            }
        }

        public bool Volatile
        {
            get
            {
                return false;
            }
        }
        #endregion

        #region ICustomSerializable Members

        public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
        {
            //FieldName
            fieldName = reader.ReadString();

            //IsTag
            isTag = reader.ReadBoolean();

            //Operation
            operation = (Operation)reader.ReadByte();

            //Value
            ushort len = reader.ReadUInt16();
            if (len > 0)
            {
                value = reader.ReadBytes(len);
            }

            //DataType
            dataType = (DataType)reader.ReadByte();
        }

        #endregion
    }
}