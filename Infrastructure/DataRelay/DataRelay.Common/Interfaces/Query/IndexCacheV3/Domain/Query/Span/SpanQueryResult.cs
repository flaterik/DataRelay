using System.Collections.Generic;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class SpanQueryResult : BaseMultiIndexIdQueryResult
    {
        #region IVersionSerializable Members

        public override void Serialize(IPrimitiveWriter writer)
        {
            //ResultItemList
            if (ResultItemList == null || ResultItemList.Count == 0)
            {
                writer.Write(0);
            }
            else
            {
                writer.Write(ResultItemList.Count);
                foreach (ResultItem resultItem in ResultItemList)
                {
                    resultItem.Serialize(writer);
                }
            }

            //IndexIdIndexHeaderMapping
            if (IndexIdIndexHeaderMapping == null || IndexIdIndexHeaderMapping.Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)IndexIdIndexHeaderMapping.Count);
                foreach (KeyValuePair<byte[] /*IndexId*/, IndexHeader /*IndexHeader*/> kvp in IndexIdIndexHeaderMapping)
                {
                    //IndexId
                    if (kvp.Key == null || kvp.Key.Length == 0)
                    {
                        writer.Write((ushort)0);
                    }
                    else
                    {
                        writer.Write((ushort)kvp.Key.Length);
                        writer.Write(kvp.Key);
                    }

                    //IndexHeader
                    Serializer.Serialize(writer, kvp.Value);
                }
            }

            //TotalCount
            writer.Write(TotalCount);

            //IsTagPrimarySort
            writer.Write(IsTagPrimarySort);

            //SortFieldName
            // Note : this field will only be needed if PrimarySort field is tag
            if (IsTagPrimarySort)
            {
                writer.Write(SortFieldName);
            }
            else
            {
                writer.Write("");
            }

            //SortOrderList
            if (SortOrderList == null || SortOrderList.Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)SortOrderList.Count);
                foreach (SortOrder sortOrder in SortOrderList)
                {
                    sortOrder.Serialize(writer);
                }
            }

            //ExceptionInfo
            writer.Write(ExceptionInfo);

            //AdditionalAvailableItemCount
            writer.Write(AdditionalAvailableItemCount);
        }

        public override void Deserialize(IPrimitiveReader reader, int version)
        {
            //ResultItemList
            int listCount = reader.ReadInt32();
            ResultItemList = new List<ResultItem>(listCount);
            if (listCount > 0)
            {
                ResultItem resultItem;
                for (int i = 0; i < listCount; i++)
                {
                    resultItem = new ResultItem();
                    resultItem.Deserialize(reader);
                    ResultItemList.Add(resultItem);
                }
            }

            //IndexIdIndexHeaderMapping
            ushort count = reader.ReadUInt16();
            if (count > 0)
            {
                IndexIdIndexHeaderMapping = new Dictionary<byte[], IndexHeader>(count, new ByteArrayEqualityComparer());
                byte[] indexId;
                IndexHeader indexHeader;
                ushort len;

                for (ushort i = 0; i < count; i++)
                {
                    len = reader.ReadUInt16();
                    indexId = null;
                    if (len > 0)
                    {
                        indexId = reader.ReadBytes(len);
                    }
                    indexHeader = new IndexHeader();
                    Serializer.Deserialize(reader.BaseStream, indexHeader);

                    IndexIdIndexHeaderMapping.Add(indexId, indexHeader);
                }
            }

            //TotalCount
            TotalCount = reader.ReadInt32();

            //IsTagPrimarySort
            IsTagPrimarySort = reader.ReadBoolean();

            //SortFieldName
            SortFieldName = reader.ReadString();

            //SortOrderList
            count = reader.ReadUInt16();
            SortOrderList = new List<SortOrder>(count);
            SortOrder sortOrder;
            for (int i = 0; i < count; i++)
            {
                sortOrder = new SortOrder();
                sortOrder.Deserialize(reader);
                SortOrderList.Add(sortOrder);
            }

            //ExceptionInfo
            ExceptionInfo = reader.ReadString();

            //AdditionalAvailableItemCount
            AdditionalAvailableItemCount = reader.ReadInt32();
        }

        private const int CURRENT_VERSION = 1;
        public override int CurrentVersion
        {
            get
            {
                return CURRENT_VERSION;
            }
        }

        #endregion
    }
}
