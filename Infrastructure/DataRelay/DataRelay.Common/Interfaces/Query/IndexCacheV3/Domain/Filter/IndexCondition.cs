using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class IndexCondition : IVersionSerializable
    {
        #region Data Members

        public byte[] InclusiveMaxValue
        { 
            get; set;
        }
        public byte[] InclusiveMinValue
        { 
            get; set;
        }

        #endregion

        #region Methods
        internal void CreateConditions(string fieldName, 
            bool isTag,
            SortOrder indexSortOrder, 
            out Condition enterCondition, 
            out Condition exitCondition)
        {
            // Enter and Exit Conditions are set for DESC sort order which is the common in most use cases
            enterCondition = InclusiveMaxValue != null ?
                new Condition(fieldName, isTag, Operation.LessThanEquals, InclusiveMaxValue, indexSortOrder.DataType) :
                null;

            exitCondition = InclusiveMinValue != null ?
                new Condition(fieldName, isTag, Operation.GreaterThanEquals, InclusiveMinValue, indexSortOrder.DataType) :
                null;

            if (indexSortOrder.SortBy == SortBy.ASC)
            {                
                var temp = enterCondition;
                enterCondition = exitCondition;
                exitCondition = temp;
            }
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
            }
        }

        private const int CURRENT_VERSION = 1;
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