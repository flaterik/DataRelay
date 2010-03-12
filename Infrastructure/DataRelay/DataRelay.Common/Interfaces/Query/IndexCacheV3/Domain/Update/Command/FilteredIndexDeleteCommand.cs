using System;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class FilteredIndexDeleteCommand : Command
    {
        #region Ctors
        public FilteredIndexDeleteCommand()
        {
            Init(null, null, null);
        }

        public FilteredIndexDeleteCommand(byte[] indexId, string targetIndexName, Filter deleteFilter)
        {
            Init(indexId, targetIndexName, deleteFilter);
        }

        private void Init(byte[] indexId, string targetIndexName, Filter deleteFilter)
        {
            this.indexId = indexId;
            this.targetIndexName = targetIndexName;
            this.deleteFilter = deleteFilter;
        }
        #endregion

        #region Data Members
        private byte[] indexId;
        public byte[] IndexId
        {
            get
            {
                return indexId;
            }
            set
            {
                indexId = value;
            }
        }

        private string targetIndexName;
        public string TargetIndexName
        {
            get
            {
                return targetIndexName;
            }
            set
            {
                targetIndexName = value;
            }
        }

        private Filter deleteFilter;
        public Filter DeleteFilter
        {
            get
            {
                return deleteFilter;
            }
            set
            {
                deleteFilter = value;
            }
        }

        internal override CommandType CommandType
        {
            get
            {
                return CommandType.FilteredIndexDelete;
            }
        }

        private int primaryId;
        public override int PrimaryId
        {
            get
            {
                return primaryId > 0 ? primaryId : IndexCacheUtils.GeneratePrimaryId(indexId);
            }
            set
            {
                primaryId = value;
            }
        }

        #endregion

        public override byte[] ExtendedId
        {
            get
            {
                return indexId;
            }
            set
            {
                throw new Exception("Setter for 'FilteredIndexDeleteCommand.ExtendedId' is not implemented and should not be invoked!");
            }
        }

        private const int CURRENT_VERSION = 1;
        public override int CurrentVersion
        {
            get
            {
                return CURRENT_VERSION;
            }
        }

        public override void Deserialize(IPrimitiveReader reader, int version)
        {
            using (reader.CreateRegion())
            {
                //IndexId
                ushort len = reader.ReadUInt16();
                if (len > 0)
                {
                    indexId = reader.ReadBytes(len);
                }

                //TargetIndexName
                targetIndexName = reader.ReadString();

                //DeleteFilter
                byte b = reader.ReadByte();
                if (b != 0)
                {
                    FilterType filterType = (FilterType)b;
                    deleteFilter = FilterFactory.CreateFilter(reader, filterType);
                }
            }
        }

        public override void Serialize(IPrimitiveWriter writer)
        {
            using (writer.CreateRegion())
            {
                //IndexId
                if (indexId == null || indexId.Length == 0)
                {
                    writer.Write((ushort)0);
                }
                else
                {
                    writer.Write((ushort)indexId.Length);
                    writer.Write(indexId);
                }

                //TargetIndexName
                writer.Write(targetIndexName);

                //DeleteFilter
                if (deleteFilter == null)
                {
                    writer.Write((byte)0);
                }
                else
                {
                    writer.Write((byte)deleteFilter.FilterType);
                    Serializer.Serialize(writer.BaseStream, deleteFilter);
                }
            }
        }
    }
}
