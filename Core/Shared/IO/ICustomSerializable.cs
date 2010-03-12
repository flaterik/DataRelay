using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using MySpace.Common.IO;

namespace MySpace.Common
{
    public interface ICustomSerializable
    {
        /// <summary>
        /// Serialize data to a stream
        /// </summary>
        /// <param name="stream">Stream to serialize data to...</param>
        void Serialize(IPrimitiveWriter writer);

        /// <summary>
        /// Deserialize data from a stream
        /// </summary>
        /// <param name="stream">Stream to deserialize data from...</param>
        void Deserialize(IPrimitiveReader reader);
    }
}
