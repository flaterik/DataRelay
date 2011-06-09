using System.Text;
using MySpace.Common;
using MySpace.Common.IO;
using System;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class IndexCondition : IVersionSerializable
    {
        #region Data Members

        public byte[] InclusiveMaxValue { get; set; }

        public byte[] InclusiveMinValue { get; set; }

        public string InclusiveMaxMetadataProperty { get; set; }

        public DataType InclusiveMaxMetadataPropertyDataType { get; set; }

        public string InclusiveMinMetadataProperty { get; set; }

        public DataType InclusiveMinMetadataPropertyDataType { get; set; }

        #endregion

        #region Methods

        internal void CreateConditions(string fieldName,
            bool isTag,
            SortOrder indexSortOrder,
            out Condition enterCondition,
            out Condition exitCondition)
        {
            // Note: Enter and Exit Conditions are set for DESC sort order which is the common in most use cases

            //EnterCondition
            if (InclusiveMaxValue != null)
            {
                enterCondition = new Condition(fieldName, isTag, Operation.LessThanEquals, InclusiveMaxValue, indexSortOrder.DataType);
            }
            else if (!string.IsNullOrEmpty(InclusiveMaxMetadataProperty))
            {
                enterCondition = new Condition(fieldName, isTag, Operation.LessThanEquals, null, InclusiveMaxMetadataPropertyDataType)
                {
                    MetadataProperty = InclusiveMaxMetadataProperty
                };
            }
            else
            {
                enterCondition = null;
            }

            //ExitCondition
            if (InclusiveMinValue != null)
            {
                exitCondition = new Condition(fieldName, isTag, Operation.GreaterThanEquals, InclusiveMinValue, indexSortOrder.DataType);
            }
            else if (!string.IsNullOrEmpty(InclusiveMinMetadataProperty))
            {
                exitCondition = new Condition(fieldName, isTag, Operation.GreaterThanEquals, null, InclusiveMinMetadataPropertyDataType)
                {
                    MetadataProperty = InclusiveMinMetadataProperty
                };
            }
            else
            {
                exitCondition = null;
            }

            if (indexSortOrder.SortBy == SortBy.ASC)
            {
                var temp = enterCondition;
                enterCondition = exitCondition;
                exitCondition = temp;
            }
        }

        public override string ToString()
        {
            var stb = new StringBuilder();
            stb.Append("(").Append("InclusiveMaxValue: ").Append(IndexCacheUtils.GetReadableByteArray(InclusiveMaxValue)).Append("),");
            stb.Append("(").Append("InclusiveMinValue: ").Append(IndexCacheUtils.GetReadableByteArray(InclusiveMinValue)).Append("),");
            stb.Append("(").Append("InclusiveMaxMetadataProperty: ").Append(InclusiveMaxMetadataProperty).Append("),");
            stb.Append("(").Append("InclusiveMaxMetadataPropertyDataType: ").Append(InclusiveMaxMetadataPropertyDataType.ToString()).Append("),");
            stb.Append("(").Append("InclusiveMinMetadataProperty: ").Append(InclusiveMinMetadataProperty).Append("),");
            stb.Append("(").Append("InclusiveMinMetadataPropertyDataType: ").Append(InclusiveMinMetadataPropertyDataType.ToString()).Append("),");
            return stb.ToString();
        }

        #endregion

        #region IVersionSerializable Members

        public void Serialize(IPrimitiveWriter writer)
        {
            using (writer.CreateRegion())
            {
                //InclusiveMaxValue
                if (InclusiveMaxValue == null || InclusiveMaxValue.Length == 0)
                {
                    writer.Write((ushort)0);
                }
                else
                {
                    writer.Write((ushort)InclusiveMaxValue.Length);
                    writer.Write(InclusiveMaxValue);
                }

                //InclusiveMinvalue
                if (InclusiveMinValue == null || InclusiveMinValue.Length == 0)
                {
                    writer.Write((ushort)0);
                }
                else
                {
                    writer.Write((ushort)InclusiveMinValue.Length);
                    writer.Write(InclusiveMinValue);
                }

                //InclusiveMaxMetadataProperty
                writer.Write(InclusiveMaxMetadataProperty);

                //InclusiveMaxMetadataPropertyDataType
                writer.Write((byte)InclusiveMaxMetadataPropertyDataType);

                //InclusiveMinMetadataProperty
                writer.Write(InclusiveMinMetadataProperty);

                //InclusiveMinMetadataPropertyDataType
                writer.Write((byte)InclusiveMinMetadataPropertyDataType);
            }
        }

        public void Deserialize(IPrimitiveReader reader, int version)
        {
            using (reader.CreateRegion())
            {
                //InclusiveMaxValue
                ushort len = reader.ReadUInt16();
                if (len > 0)
                {
                    InclusiveMaxValue = reader.ReadBytes(len);
                }

                //InclusiveMinvalue
                len = reader.ReadUInt16();
                if (len > 0)
                {
                    InclusiveMinValue = reader.ReadBytes(len);
                }

                if (version >= 2)
                {
                    //InclusiveMaxMetadataProperty
                    InclusiveMaxMetadataProperty = reader.ReadString();

                    //InclusiveMaxMetadataPropertyDataType
                    InclusiveMaxMetadataPropertyDataType = (DataType)reader.ReadByte();

                    //InclusiveMinMetadataProperty
                    InclusiveMinMetadataProperty = reader.ReadString();

                    //InclusiveMinMetadataPropertyDataType
                    InclusiveMinMetadataPropertyDataType = (DataType)reader.ReadByte();
                }
            }
        }

        private const int CURRENT_VERSION = 2;
        public int CurrentVersion
        {
            get
            {
                return CURRENT_VERSION;
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

        public void Deserialize(IPrimitiveReader reader)
        {
            reader.Response = SerializationResponse.Unhandled;
        }

        #endregion
    }
}