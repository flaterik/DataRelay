using System.Collections.Generic;
using System.Text;
using MySpace.Common.IO;
using System;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class Condition : Filter
    {
        #region Data Members

        /// <summary>
        /// Gets or sets the name of the field (ItemId/IndexId/{TagName}).
        /// </summary>
        /// <value>The name of the field.</value>
        public string FieldName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is tag.
        /// </summary>
        /// <value><c>true</c> if this instance is tag; otherwise, <c>false</c>.</value>
        public bool IsTag
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the operation.
        /// </summary>
        /// <value>The operation.</value>
        public Operation Operation
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the value for the field.
        /// </summary>
        /// <value>The value.</value>
        public byte[] Value
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the bitwise mask. Should be set only for bitwise operators
        /// </summary>
        /// <value>The bitwise mask.</value>
        public byte[] BitwiseMask
        {
            get
            {
                return Value;
            }
            set
            {
                Value = value;
            }
        }

        /// <summary>
        /// Gets or sets the DataType of the field.
        /// </summary>
        /// <value>The DataType.</value>
        public DataType DataType
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the expected bitwise result. Only applicable for a Bitwise operation.
        /// </summary>
        /// <value>The expected bitwise result.</value>
        public byte[] ExpectedBitwiseResult
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the no. of bytes to shift by. Only applicable for a Bitwise operation.
        /// </summary>
        /// <value>The shift by.</value>
        public byte ShiftBy
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the type of the filter.
        /// </summary>
        /// <value>The type of the filter.</value>
        internal override FilterType FilterType
        {
            get
            {
                return FilterType.Condition;
            }
        }
        #endregion

        #region Ctors

        /// <summary>
        /// Initializes a new instance of the <see cref="Condition"/> class.
        /// </summary>
        public Condition()
        {
            Init(null, true, Operation.Equals, null, DataType.Int32);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Condition"/> class.
        /// </summary>
        /// <param name="fieldName">Name of the field.</param>
        /// <param name="isTag">if set to <c>true</c> indicates that the field is a tag.</param>
        /// <param name="operation">The operation.</param>
        /// <param name="value">The value.</param>
        /// <param name="dataType">DataType of the field.</param>
        public Condition(string fieldName, bool isTag, Operation operation, byte[] value, DataType dataType)
        {
            Init(fieldName, isTag, operation, value, dataType);
        }

        /// <summary>
        /// Inits the instance of the <see cref="Condition"/> class.
        /// </summary>
        /// <param name="fieldName">Name of the field.</param>
        /// <param name="isTag">if set to <c>true</c> indicates that the field is a tag.</param>
        /// <param name="operation">The operation.</param>
        /// <param name="value">The value.</param>
        /// <param name="dataType">DataType of the field.</param>
        private void Init(string fieldName, bool isTag, Operation operation, byte[] value, DataType dataType)
        {
            FieldName = fieldName;
            IsTag = isTag;
            Operation = operation;
            Value = value;
            DataType = dataType;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the filter count.
        /// </summary>
        /// <value>The filter count.</value>
        internal override int FilterCount
        {
            get
            {
                return 1;
            }
        }

        /// <summary>
        /// Gets the filter info.
        /// </summary>
        /// <value>The filter info.</value>
        internal override string FilterInfo
        {
            get
            {
                var stb = new StringBuilder();
                stb.Append("(").Append(FieldName).Append(" ");
                stb.Append(Operation);
                int oper = (int)Operation;
                stb.Append(" ");
                if ((oper <= (int)Operation.BitwiseXOR))
                {
                    stb.Append((Value == null)
                                   ? "Null"
                                   : (IndexCacheUtils.GetReadableByteArray(Value) + " [" + DataType + "]"));
                }
                else
                {
                    stb.Append(ShiftBy);
                }

                if (oper > (int)Operation.NotEquals && oper != (int)Operation.BitwiseComplement)
                {
                    stb.Append(" == ").Append((ExpectedBitwiseResult == null) ? "Null" : IndexCacheUtils.GetReadableByteArray(ExpectedBitwiseResult));
                }

                stb.Append(")");
                return stb.ToString();
            }
        }

        /// <summary>
        /// Applies the Condition on the specified item value.
        /// </summary>
        /// <param name="itemValue">The item value.</param>
        /// <returns></returns>
        internal bool Process(byte[] itemValue)
        {
            bool retVal = false;
            BaseComparer itemComparer = new BaseComparer(false, null, new List<SortOrder>(1) { new SortOrder(DataType, SortBy.ASC) });

            switch (Operation)
            {
                case Operation.Equals:
                    retVal = (ByteArrayComparerUtil.CompareByteArrays(Value, itemValue)) ? true : false;
                    break;

                case Operation.NotEquals:
                    retVal = (!ByteArrayComparerUtil.CompareByteArrays(Value, itemValue)) ? true : false;
                    break;

                case Operation.GreaterThan:
                    retVal = (itemComparer.Compare(Value, itemValue) < 0) ? true : false;
                    break;

                case Operation.GreaterThanEquals:
                    retVal = (itemComparer.Compare(Value, itemValue) <= 0) ? true : false;
                    break;

                case Operation.LessThan:
                    retVal = (itemComparer.Compare(Value, itemValue) > 0) ? true : false;
                    break;

                case Operation.LessThanEquals:
                    retVal = (itemComparer.Compare(Value, itemValue) >= 0) ? true : false;
                    break;

                case Operation.BitwiseComplement:
                case Operation.BitwiseAND:
                case Operation.BitwiseOR:
                case Operation.BitwiseXOR:
                case Operation.BitwiseShiftLeft:
                case Operation.BitwiseShiftRight:
                    retVal = CheckBitwiseCondition(itemValue);
                    break;
            }
            return retVal;
        }

        /// <summary>
        /// Checks the bitwise condition.
        /// </summary>
        /// <param name="itemValue">The item value.</param>
        /// <returns></returns>
        private bool CheckBitwiseCondition(byte[] itemValue)
        {
            bool retVal = false;

            switch (DataType)
            {
                case DataType.UInt16:
                    retVal = ProcessBitwiseOperation(BitConverter.ToUInt16(itemValue, 0));
                    break;

                case DataType.Int16:
                    retVal = ProcessBitwiseOperation(BitConverter.ToInt16(itemValue, 0));
                    break;

                case DataType.UInt32:
                    retVal = ProcessBitwiseOperation(BitConverter.ToUInt32(itemValue, 0));
                    break;

                case DataType.Int32:
                case DataType.SmallDateTime:
                    retVal = ProcessBitwiseOperation(BitConverter.ToInt32(itemValue, 0));
                    break;

                case DataType.UInt64:
                    retVal = ProcessBitwiseOperation(BitConverter.ToUInt64(itemValue, 0));
                    break;

                case DataType.Int64:
                case DataType.DateTime:
                    retVal = ProcessBitwiseOperation(BitConverter.ToInt64(itemValue, 0));
                    break;

                case DataType.Byte:
                    retVal = ProcessBitwiseOperation(itemValue[0]);
                    break;
            }

            return retVal;
        }

        /// <summary>
        /// Processes the bitwise operation.
        /// </summary>
        /// <param name="ushortItemValue">The ushort item value.</param>
        /// <returns></returns>
        private bool ProcessBitwiseOperation(ushort ushortItemValue)
        {
            bool retVal = false;
            ushort ushortValue = 0;
            ushort ushortExpectedBitwiseResult = 0;
            if (Value != null)
            {
                ushortValue = BitConverter.ToUInt16(Value, 0);
            }
            if (ExpectedBitwiseResult != null)
            {
                ushortExpectedBitwiseResult = BitConverter.ToUInt16(ExpectedBitwiseResult, 0);
            }

            switch (Operation)
            {
                case Operation.BitwiseComplement:
                    retVal = (~ushortItemValue == ushortValue);
                    break;
                case Operation.BitwiseAND:
                    retVal = ((ushortItemValue & ushortValue) == ushortExpectedBitwiseResult);
                    break;
                case Operation.BitwiseOR:
                    retVal = ((ushortItemValue | ushortValue) == ushortExpectedBitwiseResult);
                    break;
                case Operation.BitwiseXOR:
                    retVal = ((ushortItemValue ^ ushortValue) == ushortExpectedBitwiseResult);
                    break;
                case Operation.BitwiseShiftLeft:
                    retVal = ((ushortItemValue << ShiftBy) == ushortExpectedBitwiseResult);
                    break;
                case Operation.BitwiseShiftRight:
                    retVal = ((ushortItemValue >> ShiftBy) == ushortExpectedBitwiseResult);
                    break;
            }
            return retVal;
        }

        /// <summary>
        /// Processes the bitwise operation.
        /// </summary>
        /// <param name="itemValue">The item value.</param>
        /// <returns></returns>
        private bool ProcessBitwiseOperation(short itemValue)
        {
            bool retVal = false;
            short shortValue = 0;
            short shortExpectedBitwiseResult = 0;
            if (Value != null)
            {
                shortValue = BitConverter.ToInt16(Value, 0);
            }
            if (ExpectedBitwiseResult != null)
            {
                shortExpectedBitwiseResult = BitConverter.ToInt16(ExpectedBitwiseResult, 0);
            }

            switch (Operation)
            {
                case Operation.BitwiseComplement:
                    retVal = (~itemValue == shortValue);
                    break;
                case Operation.BitwiseAND:
                    retVal = ((itemValue & shortValue) == shortExpectedBitwiseResult);
                    break;
                case Operation.BitwiseOR:
                    retVal = ((itemValue | shortValue) == shortExpectedBitwiseResult);
                    break;
                case Operation.BitwiseXOR:
                    retVal = ((itemValue ^ shortValue) == shortExpectedBitwiseResult);
                    break;
                case Operation.BitwiseShiftLeft:
                    retVal = ((itemValue << ShiftBy) == shortExpectedBitwiseResult);
                    break;
                case Operation.BitwiseShiftRight:
                    retVal = ((itemValue >> ShiftBy) == shortExpectedBitwiseResult);
                    break;
            }
            return retVal;
        }

        /// <summary>
        /// Processes the bitwise operation.
        /// </summary>
        /// <param name="itemValue">The item value.</param>
        /// <returns></returns>
        private bool ProcessBitwiseOperation(uint itemValue)
        {
            bool retVal = false;
            uint uintValue = 0;
            uint uintExpectedBitwiseResult = 0;
            if (Value != null)
            {
                uintValue = BitConverter.ToUInt32(Value, 0);
            }
            if (ExpectedBitwiseResult != null)
            {
                uintExpectedBitwiseResult = BitConverter.ToUInt32(ExpectedBitwiseResult, 0);
            }

            switch (Operation)
            {
                case Operation.BitwiseComplement:
                    retVal = (~itemValue == uintValue);
                    break;
                case Operation.BitwiseAND:
                    retVal = ((itemValue & uintValue) == uintExpectedBitwiseResult);
                    break;
                case Operation.BitwiseOR:
                    retVal = ((itemValue | uintValue) == uintExpectedBitwiseResult);
                    break;
                case Operation.BitwiseXOR:
                    retVal = ((itemValue ^ uintValue) == uintExpectedBitwiseResult);
                    break;
                case Operation.BitwiseShiftLeft:
                    retVal = ((itemValue << ShiftBy) == uintExpectedBitwiseResult);
                    break;
                case Operation.BitwiseShiftRight:
                    retVal = ((itemValue >> ShiftBy) == uintExpectedBitwiseResult);
                    break;
            }
            return retVal;
        }

        /// <summary>
        /// Processes the bitwise operation.
        /// </summary>
        /// <param name="itemValue">The item value.</param>
        /// <returns></returns>
        private bool ProcessBitwiseOperation(int itemValue)
        {
            bool retVal = false;
            int intValue = 0;
            int intExpectedBitwiseResult = 0;
            if (Value != null)
            {
                intValue = BitConverter.ToInt32(Value, 0);
            }
            if (ExpectedBitwiseResult != null)
            {
                intExpectedBitwiseResult = BitConverter.ToInt32(ExpectedBitwiseResult, 0);
            }

            switch (Operation)
            {
                case Operation.BitwiseComplement:
                    retVal = (~itemValue == intValue);
                    break;
                case Operation.BitwiseAND:
                    retVal = ((itemValue & intValue) == intExpectedBitwiseResult);
                    break;
                case Operation.BitwiseOR:
                    retVal = ((itemValue | intValue) == intExpectedBitwiseResult);
                    break;
                case Operation.BitwiseXOR:
                    retVal = ((itemValue ^ intValue) == intExpectedBitwiseResult);
                    break;
                case Operation.BitwiseShiftLeft:
                    retVal = ((itemValue << ShiftBy) == intExpectedBitwiseResult);
                    break;
                case Operation.BitwiseShiftRight:
                    retVal = ((itemValue >> ShiftBy) == intExpectedBitwiseResult);
                    break;
            }
            return retVal;
        }

        /// <summary>
        /// Processes the bitwise operation.
        /// </summary>
        /// <param name="itemValue">The item value.</param>
        /// <returns></returns>
        private bool ProcessBitwiseOperation(ulong itemValue)
        {
            bool retVal = false;
            ulong ulongValue = 0;
            ulong ulongExpectedBitwiseResult = 0;
            if (Value != null)
            {
                ulongValue = BitConverter.ToUInt32(Value, 0);
            }
            if (ExpectedBitwiseResult != null)
            {
                ulongExpectedBitwiseResult = BitConverter.ToUInt32(ExpectedBitwiseResult, 0);
            }

            switch (Operation)
            {
                case Operation.BitwiseComplement:
                    retVal = (~itemValue == ulongValue);
                    break;
                case Operation.BitwiseAND:
                    retVal = ((itemValue & ulongValue) == ulongExpectedBitwiseResult);
                    break;
                case Operation.BitwiseOR:
                    retVal = ((itemValue | ulongValue) == ulongExpectedBitwiseResult);
                    break;
                case Operation.BitwiseXOR:
                    retVal = ((itemValue ^ ulongValue) == ulongExpectedBitwiseResult);
                    break;
                case Operation.BitwiseShiftLeft:
                    retVal = ((itemValue << ShiftBy) == ulongExpectedBitwiseResult);
                    break;
                case Operation.BitwiseShiftRight:
                    retVal = ((itemValue >> ShiftBy) == ulongExpectedBitwiseResult);
                    break;
            }
            return retVal;
        }

        /// <summary>
        /// Processes the bitwise operation.
        /// </summary>
        /// <param name="itemValue">The item value.</param>
        /// <returns></returns>
        private bool ProcessBitwiseOperation(long itemValue)
        {
            bool retVal = false;
            long longValue = 0;
            long longExpectedBitwiseResult = 0;
            if (Value != null)
            {
                longValue = BitConverter.ToUInt32(Value, 0);
            }
            if (ExpectedBitwiseResult != null)
            {
                longExpectedBitwiseResult = BitConverter.ToUInt32(ExpectedBitwiseResult, 0);
            }

            switch (Operation)
            {
                case Operation.BitwiseComplement:
                    retVal = (~itemValue == longValue);
                    break;
                case Operation.BitwiseAND:
                    retVal = ((itemValue & longValue) == longExpectedBitwiseResult);
                    break;
                case Operation.BitwiseOR:
                    retVal = ((itemValue | longValue) == longExpectedBitwiseResult);
                    break;
                case Operation.BitwiseXOR:
                    retVal = ((itemValue ^ longValue) == longExpectedBitwiseResult);
                    break;
                case Operation.BitwiseShiftLeft:
                    retVal = ((itemValue << ShiftBy) == longExpectedBitwiseResult);
                    break;
                case Operation.BitwiseShiftRight:
                    retVal = ((itemValue >> ShiftBy) == longExpectedBitwiseResult);
                    break;
            }
            return retVal;
        }

        /// <summary>
        /// Processes the bitwise operation.
        /// </summary>
        /// <param name="itemValue">The item value.</param>
        /// <returns></returns>
        private bool ProcessBitwiseOperation(byte itemValue)
        {
            bool retVal = false;
            byte byteValue = 0;
            byte byteExpectedBitwiseResult = 0;
            if (Value != null)
            {
                byteValue = Value[0];
            }
            if (ExpectedBitwiseResult != null)
            {
                byteExpectedBitwiseResult = ExpectedBitwiseResult[0];
            }

            switch (Operation)
            {
                case Operation.BitwiseComplement:
                    retVal = (~itemValue == byteValue);
                    break;
                case Operation.BitwiseAND:
                    retVal = ((itemValue & byteValue) == byteExpectedBitwiseResult);
                    break;
                case Operation.BitwiseOR:
                    retVal = ((itemValue | byteValue) == byteExpectedBitwiseResult);
                    break;
                case Operation.BitwiseXOR:
                    retVal = ((itemValue ^ byteValue) == byteExpectedBitwiseResult);
                    break;
                case Operation.BitwiseShiftLeft:
                    retVal = ((itemValue << ShiftBy) == byteExpectedBitwiseResult);
                    break;
                case Operation.BitwiseShiftRight:
                    retVal = ((itemValue >> ShiftBy) == byteExpectedBitwiseResult);
                    break;
            }
            return retVal;
        }

        #endregion

        #region IVersionSerializable Members

        /// <summary>
        /// Serializes the specified writer.
        /// </summary>
        /// <param name="writer">The writer.</param>
        public override void Serialize(IPrimitiveWriter writer)
        {
            using (writer.CreateRegion())
            {
                //FieldName
                writer.Write(FieldName);

                //IsTag
                writer.Write(IsTag);

                //Operation
                writer.Write((byte)Operation);

                //Value
                if (Value == null || Value.Length == 0)
                {
                    writer.Write((ushort)0);
                }
                else
                {
                    writer.Write((ushort)Value.Length);
                    writer.Write(Value);
                }

                //DataType
                writer.Write((byte)DataType);

                //MatchValue
                if (ExpectedBitwiseResult == null || ExpectedBitwiseResult.Length == 0)
                {
                    writer.Write((ushort)0);
                }
                else
                {
                    writer.Write((ushort)ExpectedBitwiseResult.Length);
                    writer.Write(ExpectedBitwiseResult);
                }

                //ShiftBy
                writer.Write(ShiftBy);
            }
        }

        /// <summary>
        /// Deserializes the specified reader.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="version">The version.</param>
        public override void Deserialize(IPrimitiveReader reader, int version)
        {
            using (reader.CreateRegion())
            {
                //FieldName
                FieldName = reader.ReadString();

                //IsTag
                IsTag = reader.ReadBoolean();

                //Operation
                Operation = (Operation)reader.ReadByte();

                //Value
                ushort len = reader.ReadUInt16();
                if (len > 0)
                {
                    Value = reader.ReadBytes(len);
                }

                //DataType
                DataType = (DataType)reader.ReadByte();

                if (version >= 2)
                {
                    //MatchValue
                    len = reader.ReadUInt16();
                    if (len > 0)
                    {
                        ExpectedBitwiseResult = reader.ReadBytes(len);
                    }

                    //ShiftBy
                    ShiftBy = reader.ReadByte();
                }
            }
        }

        private const int CURRENT_VERSION = 2;
        /// <summary>
        /// Gets the current version.
        /// </summary>
        /// <value>The current version.</value>
        public override int CurrentVersion
        {
            get
            {
                return CURRENT_VERSION;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Condition"/> is volatile.
        /// </summary>
        /// <value><c>true</c> if volatile; otherwise, <c>false</c>.</value>
        public override bool Volatile
        {
            get
            {
                return false;
            }
        }

        #endregion
    }
}