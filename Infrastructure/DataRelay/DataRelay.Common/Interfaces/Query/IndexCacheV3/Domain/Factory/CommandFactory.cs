using System;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    internal static class CommandFactory
    {        
        internal static Command CreateCommand(IPrimitiveReader reader, CommandType commandType)
        {
            Command command;

            switch (commandType)
            {
                case CommandType.FilteredIndexDelete:
                    command = new FilteredIndexDeleteCommand();                    
                    break;
                    
                default:
                    throw new Exception("Unknown CommandType " + commandType);
            }

            Serializer.Deserialize(reader.BaseStream, command);
            return command;
        }
    }
}

