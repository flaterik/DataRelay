using System.IO;
using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay
{
    /// <summary>
    /// Extensions methods to faciliate Stream operations.
    /// </summary>
    internal static class RelayStreamExtensions
    {
        /// <summary>
        /// Reads and deserializes a new instance of a type object from the Stream object.
        /// </summary>
        /// <typeparam name="T">The type to read from the stream.</typeparam>
        /// <param name="stream">The stream.</param>
        /// <returns>An instance of type T.</returns>
        public static T Read<T>(this Stream stream) where T : IVersionSerializable, new()
        {
            var reader = SerializerFactory.GetReader(stream);
            var resultObject = new T();
            resultObject.Deserialize(reader, resultObject.CurrentVersion);
            return resultObject;
            
        }
    }
}