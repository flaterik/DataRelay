using System;
using System.Runtime.InteropServices;
using MySpace.Logging;

namespace MySpace.DataRelay.Interfaces.Messages
{
    [Flags]
    public enum RelayMessagePipelineFlags : byte
    {
        None                    = 0x00,
        IsReplication           = 0x01,
        IsInterClusterMessage   = 0x02,
        WasRedirected           = 0x04,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RelayMessagePipelineHeader
    {
        public byte Version;
        public RelayMessagePipelineFlags Flags;
        public byte ExtendedIdOffset; // header has to be < 255 bytes long, including address history
        public byte AddressHistoryOffset;
        public int PayloadOffset;
        public MessageType MessageType;
        public short TypeId;
        public int PrimaryId;
        public int ExtendedIdLength;
        public int AddressHistoryEntries;
        public byte SourceZone;
        public byte TTL;
    }

    public class RelayMessagePipelineSerializer : IDisposable
    {
        private readonly int _headerSize;
        private IntPtr _pinnedBuffer;
        private object _syncRoot = new object();
        private LogWrapper _log = new LogWrapper();

        public int HeaderSize { get { return _headerSize;  } }

        public RelayMessagePipelineSerializer()
        {
            _headerSize = Marshal.SizeOf(typeof(RelayMessagePipelineHeader));
            _pinnedBuffer = Marshal.AllocHGlobal(_headerSize);
        }

        public void Dispose()
        {
            if (_pinnedBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_pinnedBuffer);
                _pinnedBuffer = IntPtr.Zero;
            }
        }

        public byte[] Serialize(RelayMessagePipelineHeader header)
        {
            if (_pinnedBuffer == IntPtr.Zero) throw new ObjectDisposedException("RelayMessagePipelineHeader", "The RelayMessagePipelineHeader has been disposed and then used, probably on an asynchronous IO completion thread.");
            var ret = new byte[_headerSize];
            lock (_syncRoot)
            {
                Marshal.StructureToPtr(header, _pinnedBuffer, false);
                Marshal.Copy(_pinnedBuffer, ret, 0, _headerSize);
                return ret;
            }
        }

        public RelayMessagePipelineHeader Deserialize(byte[] data)
        {
            if (_pinnedBuffer == IntPtr.Zero) throw new ObjectDisposedException("RelayMessagePipelineHeader", "The ChunkHeaderSerializer has been disposed and then used, probably on an asynchronous IO completion thread.");
            lock (_syncRoot)
            {
                Marshal.Copy(data, 0, _pinnedBuffer, _headerSize);
                var header =
                    (RelayMessagePipelineHeader)
                    Marshal.PtrToStructure(_pinnedBuffer, typeof(RelayMessagePipelineHeader));
                return header;
            }
        }
    }
}
