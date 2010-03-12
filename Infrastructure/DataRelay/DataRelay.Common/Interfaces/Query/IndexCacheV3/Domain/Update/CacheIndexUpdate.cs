using System;
using MySpace.Common;
using MySpace.Common.Framework;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class CacheIndexUpdate : IVersionSerializable, IExtendedRawCacheParameter
    {
        #region Ctors
        public CacheIndexUpdate()
        {
            Init(null);
        }

        public CacheIndexUpdate(Command command)
        {
            Init(command);
        }

        private void Init(Command command)
        {
            this.command = command;
        }
        #endregion

        #region Data Members
        private Command command;
        public Command Command
        {
            get
            {
                return command;
            }
            set
            {
                command = value;
            }
        }

        #endregion

        #region IExtendedRawCacheParameter Members
        public byte[] ExtendedId
        {
            get
            {
                return command.ExtendedId;
            }
            set
            {
                throw new Exception("Setter for 'CacheIndexUpdate.ExtendedId' is not implemented and should not be invoked!");
            }
        }

        private DateTime? lastUpdatedDate;
        public DateTime? LastUpdatedDate
        {
            get
            {
                return lastUpdatedDate;
            }
            set
            {
                lastUpdatedDate = value;
            }
        }
        #endregion

        #region ICacheParameter Members
        public int PrimaryId
        {
            get
            {
                return command.PrimaryId;
            }
            set
            {
                command.PrimaryId = value;
            }
        }

        private DataSource dataSource = DataSource.Unknown;
        public DataSource DataSource
        {
            get
            {
                return dataSource;
            }
            set
            {
                dataSource = value;
            }
        }

		public bool IsEmpty
		{
			get
			{
				return false;
			}
			set
			{
				return;
			}
		}

		public bool IsValid
		{
			get
			{
				return true;
			}
		}

		public bool EditMode
		{
			get
			{
				return false;
			}
			set
			{
				return;
			}
		}
        #endregion

        #region IVersionSerializable Members
        public void Serialize(IPrimitiveWriter writer)
        {
            using (writer.CreateRegion())
            {
                writer.Write((byte)command.CommandType);
                Serializer.Serialize(writer.BaseStream, command);
            }
        }

        public void Deserialize(IPrimitiveReader reader, int version)
        {
            using (reader.CreateRegion())
            {
                CommandType commandType = (CommandType)reader.ReadByte();
                command = CommandFactory.CreateCommand(reader, commandType);
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
