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
            this.IndexId = indexId;
            this.TargetIndexName = targetIndexName;
            this.DeleteFilter = deleteFilter;
        }
        #endregion

        #region Data Members

        public byte[] IndexId { get; set; }

        public string TargetIndexName { get; set; }

        public Filter DeleteFilter { get; set; }

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
                return primaryId > 0 ? primaryId : IndexCacheUtils.GeneratePrimaryId(IndexId);
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
                return IndexId;
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
                    IndexId = reader.ReadBytes(len);
                }

                //TargetIndexName
                TargetIndexName = reader.ReadString();

                //DeleteFilter
                byte b = reader.ReadByte();
                if (b != 0)
                {
                    FilterType filterType = (FilterType)b;
                    DeleteFilter = FilterFactory.CreateFilter(reader, filterType);
                }
            }
        }

        public override void Serialize(IPrimitiveWriter writer)
        {
            using (writer.CreateRegion())
            {
                //IndexId
                if (IndexId == null || IndexId.Length == 0)
                {
                    writer.Write((ushort)0);
                }
                else
                {
                    writer.Write((ushort)IndexId.Length);
                    writer.Write(IndexId);
                }

                //TargetIndexName
                writer.Write(TargetIndexName);

                //DeleteFilter
                if (DeleteFilter == null)
                {
                    writer.Write((byte)0);
                }
                else
                {
                    writer.Write((byte)DeleteFilter.FilterType);
                    Serializer.Serialize(writer.BaseStream, DeleteFilter);
                }
            }
        }
    }
}
