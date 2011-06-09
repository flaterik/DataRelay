using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class MetadataPropertyQuery : IRelayMessageQuery, IPrimaryQueryId
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

        #endregion

        #region IRelayMessageQuery Members

        /// <summary>
        /// Gets the query id.
        /// </summary>
        /// <value>The query id.</value>
        public byte QueryId
        {
            get
            {
                return (byte)QueryTypes.MetadataPropertyQuery;
            }
        }

        #endregion

        #region IPrimaryQueryId Members

        private int primaryId = IndexCacheUtils.MULTIINDEXQUERY_DEFAULT_PRIMARYID;
        /// <summary>
        /// Gets or sets the primary id.
        /// </summary>
        /// <value>The primary id.</value>
        public int PrimaryId
        {
            get
            {
                if (primaryId == IndexCacheUtils.MULTIINDEXQUERY_DEFAULT_PRIMARYID)
                {
                    return IndexCacheUtils.GeneratePrimaryId(IndexId);
                }
                return primaryId;
            }
            set
            {
                primaryId = value;
            }
        }

        #endregion

        #region IVersionSerializable Members

        private const int CURRENT_VERSION = 1;
        /// <summary>
        /// Gets the current serialization data version of your object.  The <see cref="M:MySpace.Common.IVersionSerializable.Serialize(MySpace.Common.IO.IPrimitiveWriter)"/> method
        /// will write to the stream the correct format for this version.
        /// </summary>
        /// <value></value>
        public int CurrentVersion
        {
            get
            {
                return CURRENT_VERSION;
            }
        }

        /// <summary>
        /// Serialize the class data to a stream.
        /// </summary>
        /// <param name="writer">The <see cref="T:MySpace.Common.IO.IPrimitiveWriter"/> that writes to the stream.</param>
        public void Serialize(IPrimitiveWriter writer)
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
        }

        /// <summary>
        /// Deserialize the class data from a stream.
        /// </summary>
        /// <param name="reader">The <see cref="T:MySpace.Common.IO.IPrimitiveReader"/> that extracts used to extra data from a stream.</param>
        /// <param name="version">The value of <see cref="P:MySpace.Common.IVersionSerializable.CurrentVersion"/> that was written to the stream when it was originally serialized to a stream;
        /// the version of the <paramref name="reader"/> data.</param>
        public void Deserialize(IPrimitiveReader reader, int version)
        {
            //IndexId
            ushort len = reader.ReadUInt16();
            if (len > 0)
            {
                IndexId = reader.ReadBytes(len);
            }

            //TargetIndexName
            TargetIndexName = reader.ReadString();
        }

        /// <summary>
        /// Deprecated. Has no effect.
        /// </summary>
        /// <value></value>
        public bool Volatile
        {
            get
            {
                return false;
            }
        }

        #endregion

        #region ICustomSerializable Members

        /// <summary>
        /// Deserialize data from a stream
        /// </summary>
        /// <param name="reader"></param>
        public void Deserialize(IPrimitiveReader reader)
        {
            reader.Response = SerializationResponse.Unhandled;
        }

        #endregion
    }
}