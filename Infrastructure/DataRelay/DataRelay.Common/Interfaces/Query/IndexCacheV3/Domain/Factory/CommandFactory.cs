using System;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    internal static class CommandFactory
    {        
        /// <summary>
        /// Creates the command.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="commandType">Type of the command.</param>
        /// <returns>Command object</returns>
        internal static Command CreateCommand(IPrimitiveReader reader, CommandType commandType)
        {
            Command command;

            switch (commandType)
            {
                case CommandType.FilteredIndexDelete:
                    command = new FilteredIndexDeleteCommand();                    
                    break;

                case CommandType.MetadataProperty:
                    command = new MetadataPropertyCommand();
                    break;

                default:
                    throw new Exception("Unknown CommandType " + commandType);
            }

            Serializer.Deserialize(reader.BaseStream, command);
            return command;
        }
    }
}

