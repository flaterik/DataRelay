using System;
using System.IO;
using MySpace.Common.Storage;
using MySpace.Storage;
using MySpace.ResourcePool;

namespace MySpace.BinaryStorage
{
    public class SmartStream : Stream
    {
        #region fields
        private readonly IBinaryStorage binaryStore;

        private byte[] buffer;

        private int bufferBeginOffset;

        private int currentOffset;

        private readonly short typeIdInternal;

        private readonly StorageKey key;

        private readonly int sizeOfChunk; 

        private bool isLastBuffer = false;

        private int bytesLeftInBuffer;

        private int bufferBytesLength;


        private bool firstLengthRequest = true;

        private long entryLength = -1;

        private int dbAccesstime = 0;

        private MemoryStream currentStream;

        #endregion

        #region ctor
        public SmartStream(IBinaryStorage store, short typeId, int objectId, byte[] extendedId, int getSize, MemoryStream stream)
        {
            if (stream == null)
            {
                throw new Exception("The memory stream passed in is null");
            }

            // initialize the berkeley db store
            this.binaryStore = store;
            this.sizeOfChunk = getSize;

            this.key = new StorageKey(extendedId, objectId);

            this.currentStream = stream;
            this.currentStream.Capacity = sizeOfChunk;
            byte[] result = this.currentStream.GetBuffer();

            int getBytes = this.binaryStore.Get(typeId, this.key, 0, result);

            if (getBytes > 0)
            {
                this.buffer = result;
            }

            isLastBuffer = getBytes < sizeOfChunk ? true : false;

            this.dbAccesstime++;
            this.bufferBytesLength = getBytes;
            this.bytesLeftInBuffer = getBytes;
            this.typeIdInternal = typeId;
        }

        #endregion

        #region Stream methods
        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override void Flush()
        {
            return;
        }

        /// <summary>
        /// How many bytes left for reading for the current buffer
        /// need to check, if there is something to get the whole length from the 
        /// dbd for that entry, not just current buffer length.
        /// </summary>
        public override long Length
        {
            get
            {
                if (firstLengthRequest)
                {
                    firstLengthRequest = false;
                    return this.bytesLeftInBuffer;
                }

                if (this.isLastBuffer)
                {
                    return this.Position + this.bytesLeftInBuffer;
                }

                if (this.entryLength == -1)
                {
                    this.entryLength = this.binaryStore.GetLength(this.typeIdInternal, this.key);
                    this.dbAccesstime++;
                }

                return this.entryLength;
            }
        }

        public override long Position
        {
            get
            {
                return this.currentOffset;
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public int DatabaseAccessTime
        {
            get
            {
                return this.dbAccesstime;
            }
        }

        private void UpdateRead(int readCount)
        {
            this.bytesLeftInBuffer -= readCount;
            this.currentOffset += readCount;
        }

        public override int Read(byte[] readBuffer, int offset, int count)
        {
            if (this.currentOffset <= (this.bufferBeginOffset + this.bufferBytesLength - 1))
            {
                if (this.bytesLeftInBuffer >= count)
                {
                    Buffer.BlockCopy(this.buffer, this.currentOffset - this.bufferBeginOffset, readBuffer, 0, count);

                    this.currentOffset += count;
                    this.bytesLeftInBuffer -= count;

                    return count;
                }
                else   // if count greater than the remaining bytes in the buffer
                {
                    int totalBytesCopied = 0; 

                    if (!this.isLastBuffer)      
                    {
                        // Need to retrieve another buffer and combine the result
                        Buffer.BlockCopy(
                                this.buffer,
                                this.buffer.Length - this.bytesLeftInBuffer,
                                readBuffer,
                                0,
                                this.bytesLeftInBuffer);

                        totalBytesCopied += this.bytesLeftInBuffer;
                        int numberOfBytesToGet = count - totalBytesCopied;
                        this.bufferBeginOffset += this.buffer.Length;

                        currentStream.Capacity = numberOfBytesToGet > this.sizeOfChunk ? numberOfBytesToGet : this.sizeOfChunk;

                        byte[] result = currentStream.GetBuffer();

                        int actualBytesGet = this.binaryStore.Get(this.typeIdInternal, this.key, this.bufferBeginOffset, result);

                        this.dbAccesstime++;

                        if (actualBytesGet == 0)    // if there is no new bytes
                        {
                            return totalBytesCopied;
                        }
                        else if (actualBytesGet < result.Length)
                        {
                            this.isLastBuffer = true;
                        }

                        this.bufferBytesLength = actualBytesGet;
                        this.bytesLeftInBuffer = actualBytesGet;
                        this.buffer = result;

                        int actualBytesToCopy = actualBytesGet > count - totalBytesCopied ? count - totalBytesCopied : actualBytesGet;

                        Buffer.BlockCopy(
                                this.buffer,
                                0,
                                readBuffer,
                                totalBytesCopied,   // use as offset position here
                                actualBytesToCopy);

                        totalBytesCopied += actualBytesToCopy;
                        this.currentOffset += totalBytesCopied;
                        this.bytesLeftInBuffer -= actualBytesToCopy;

                        return totalBytesCopied;
                    }

                    Buffer.BlockCopy(this.buffer, this.currentOffset - this.bufferBeginOffset, readBuffer,
                                         0, this.bytesLeftInBuffer);

                    totalBytesCopied = this.bytesLeftInBuffer;
                    this.currentOffset += totalBytesCopied;
                    this.bytesLeftInBuffer = 0;
                    

                    return totalBytesCopied;
                }
            }
            else
            {
                //release pooled stream if there is one
                currentStream.Seek(0, SeekOrigin.Begin);

                // need to get a new buffer as a current buffer
                // get the offset and count to retrieve the item and copy the bytes
                if (this.isLastBuffer)
                {
                    this.bytesLeftInBuffer = 0;
                    return 0;
                }

                int numOfBytesToGet = sizeOfChunk >= count ? sizeOfChunk : count;

                currentStream.Capacity = numOfBytesToGet;

                byte[] result = currentStream.GetBuffer();

                int actualBytesCount = this.binaryStore.Get(this.typeIdInternal, this.key, this.currentOffset, result);

                this.dbAccesstime++;

                if (actualBytesCount < numOfBytesToGet)
                {
                    this.isLastBuffer = true;
                }

                this.buffer = result;
                this.bufferBytesLength = actualBytesCount;
                this.bytesLeftInBuffer = actualBytesCount;
                this.bufferBeginOffset = this.currentOffset;

                int bytesToCopy = actualBytesCount > count ? count : actualBytesCount;
                Buffer.BlockCopy(this.buffer, 0, readBuffer, 0, bytesToCopy);

                // update the offset
                this.currentOffset += bytesToCopy;
                this.bytesLeftInBuffer -= bytesToCopy;

                return bytesToCopy;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] writeBuffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}