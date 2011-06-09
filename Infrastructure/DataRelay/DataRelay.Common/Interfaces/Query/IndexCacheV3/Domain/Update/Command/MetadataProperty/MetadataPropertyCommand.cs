using System;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class MetadataPropertyCommand : Command
    {
        #region Data Members

        /// <summary>
        /// Gets or sets the index id.
        /// </summary>
        /// <value>The index id.</value>
        public byte[] IndexId
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the name of the target index.
        /// </summary>
        /// <value>The name of the target index.</value>
        public string TargetIndexName
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the MetadataPropertyCollectionUpdate.
        /// </summary>
        /// <value>The MetadataPropertyCollectionUpdate.</value>
        public MetadataPropertyCollectionUpdate MetadataPropertyCollectionUpdate
        {
            get; set;
        }

        /// <summary>
        /// Gets the type of the command.
        /// </summary>
        /// <value>The type of the command.</value>
        internal override CommandType CommandType
        {
            get
            {
                return CommandType.MetadataProperty;
            }
        }

        private int primaryId;
        /// <summary>
        /// Gets or sets the primary id.
        /// </summary>
        /// <value>The primary id.</value>
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

        /// <summary>
        /// Gets or sets the extended id.
        /// </summary>
        /// <value>The extended id.</value>
        public override byte[] ExtendedId
        {
            get
            {
                return IndexId;
            }
            set
            {
                throw new Exception("Setter for 'MetadataDictionaryCommand.ExtendedId' is not implemented and should not be invoked!");
            }
        }

        #endregion

        #region Methods

        private const int CURRENT_VERSION = 1;
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
        /// Serializes the specified writer.
        /// </summary>
        /// <param name="writer">The writer.</param>
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

                //MetadataPropertyCollectionUpdate
                if(MetadataPropertyCollectionUpdate == null)
                {
                    writer.Write(false);
                }
                else
                {
                    writer.Write(true);
                    Serializer.Serialize(writer.BaseStream, MetadataPropertyCollectionUpdate);
                }
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
                //IndexId
                ushort len = reader.ReadUInt16();
                if (len > 0)
                {
                    IndexId = reader.ReadBytes(len);
                }

                //TargetIndexName
                TargetIndexName = reader.ReadString();

                //MetadataPropertyCollectionUpdate
                if(reader.ReadBoolean())
                {
                    MetadataPropertyCollectionUpdate = new MetadataPropertyCollectionUpdate();
                    Serializer.Deserialize(reader.BaseStream, MetadataPropertyCollectionUpdate);
                }
            }
        }

        #endregion
    }
}
