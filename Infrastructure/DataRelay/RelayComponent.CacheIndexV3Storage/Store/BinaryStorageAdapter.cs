using System;
using System.Runtime.InteropServices;
using MySpace.ResourcePool;
using MySpace.Storage;
using MySpace.Common.Storage;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PayloadStorage
    {
        public bool Compressed;           //1
        public int TTL;                  //5
        public long LastUpdatedTicks;     //13
        public long ExpirationTicks;      //21
        public bool Deactivated;          //22    
    };

    public static class BinaryStorageAdapter
    {
        private static readonly unsafe int BdbHeaderSize = sizeof(PayloadStorage);

        public static unsafe void Save(MemoryStreamPool memPool, IBinaryStorage store, short typeId, int primaryId, byte[] extendedId, PayloadStorage header, byte[] value)
        {
            if (value != null)
            {
                byte[] entryBytes = new byte[BdbHeaderSize + value.Length];

                fixed (byte* pBytes = &entryBytes[0])
                {
                    *((PayloadStorage*)pBytes) = header;
                }

                Buffer.BlockCopy(value, 0, entryBytes, BdbHeaderSize, value.Length);

                store.Put(typeId, new StorageKey(extendedId, primaryId), entryBytes);
            }
        }

        public static byte[] Get(IBinaryStorage store, short typeId, int primaryId, byte[] extendedId)
        {
            byte[] dbEntry = store.GetBuffer(typeId, new StorageKey(extendedId, primaryId));

            byte[] resultBytes = null;

            if (dbEntry != null)
            {
                int bytesToCopy = dbEntry.Length - BdbHeaderSize;

                if (bytesToCopy > 0)
                {
                    resultBytes = new byte[bytesToCopy];
                    Buffer.BlockCopy(dbEntry, BdbHeaderSize, resultBytes, 0, bytesToCopy);
                }
            }

            return resultBytes;
        }

        public static bool Delete(IBinaryStorage store, short typeId, int primaryId, byte[] extendedId)
        {
            return store.Delete(typeId, new StorageKey(extendedId, primaryId));
        }

        public static void Clear(IBinaryStorage store, short typeId)
        {
            store.Clear(typeId);
        }
    }
}
