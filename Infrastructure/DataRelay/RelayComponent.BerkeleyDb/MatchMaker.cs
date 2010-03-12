using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using MySpace.Logging;
using MySpace.ResourcePool;
using MySpace.Common.HelperObjects;

namespace MySpace.DataRelay.RelayComponent.BerkeleyDb
{
    class MatchMaker : IDisposable
    {
        private static readonly LogWrapper Log = new LogWrapper();

        private bool isDisposed = false;
        private Dictionary<short,ReplyBucket> replies;	//keyed by messageId	
        //private ReplyBucket[] replies;
		private MsReaderWriterLock replyLock; //controls access to Replies object
		private short currentMessageId = 0; //
		internal ResourcePool<short> idPool;

        [ThreadStatic]
        private static EventWaitHandle replyEvent; //only use one per thread, because one thread only cares about one reply at a time

        private static EventWaitHandle ReplyEvent //has to be generated as needed to preserve thread staticness
        {
            get
            {
                if (replyEvent == null)
                {
                    replyEvent = new EventWaitHandle(false, EventResetMode.AutoReset);
                }
                return replyEvent;
            }
        }


        public MatchMaker(int maxPoolItemReuse)
        {
            replies = new Dictionary<short, ReplyBucket>(32);
            idPool = new ResourcePool<short>(new ResourcePool<short>.BuildItemDelegate(GetNextMessageId), maxPoolItemReuse);
            replyLock = new MsReaderWriterLock(System.Threading.LockRecursionPolicy.NoRecursion);		
        }

                // Use C# destructor syntax for finalization code.
		// This destructor will run only if the Dispose method 
		// does not get called.
		// It gives your base class the opportunity to finalize.
		// Do not provide destructors in types derived from this class.
        ~MatchMaker()
		{
			// Do not re-create Dispose clean-up code here.
			// Calling Dispose(false) is optimal in terms of
			// readability and maintainability.
			Dispose(false);
		}

        // Dispose(bool disposing) executes in two distinct scenarios.
        // If disposing equals true, the method has been called directly
        // or indirectly by a user's code. Managed and unmanaged resources
        // can be disposed.
        // If disposing equals false, the method has been called by the 
        // runtime from inside the finalizer and you should not reference 
        // other objects. Only unmanaged resources can be disposed.
        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    if (replyLock != null)
                    {
                        replyLock.Dispose();
                    }
                    if (Log.IsInfoEnabled)
                    {
                        Log.InfoFormat("Dispose() MatchMaker is Disposed");
                    }
                }
                //Any unmanaged code should be released here

                // Note that this is not thread safe.
                // Another thread could start disposing the object
                // after the managed resources are disposed,
                // but before the disposed flag is set to true.
                // If thread safety is necessary, it must be
                // implemented by the client.
                isDisposed = true;
            }
        }

        private short GetNextMessageId()
        {
            short current;
            //always called from within a ReplyLock.WaitToWrite; no need for seperate lock
            if (currentMessageId == -1)
            {
                current = currentMessageId = 1; //skip 0 and avoid negatives
            }
            else
            {
                current = ++currentMessageId;
            }

            return current;
        }

        internal short SetWaitHandle(int waitTimeout)
        {
            //this uses GetNextMessageId to generate new ids, but also reuses ids as much as possible. This VASTLY reduces the number of replybuckets needed.
            ResourcePoolItem<short> idItem = idPool.GetItem();
            short waitId = idItem.Item;
            ReplyBucket bucket = null;

            if (!replies.ContainsKey(waitId)) //we haven't used this waitId yet
            {
                replyLock.Write(() =>
                                	{
										if (!replies.ContainsKey(waitId))
										{
											if (Log.IsDebugEnabled)
											{
												Log.DebugFormat("SetWaitHandle() Creates new ReplyBucket.");
											}
											replies.Add(waitId, new ReplyBucket());
										}            		
                                	});
            }
			
			replyLock.Read(() => { bucket = replies[waitId]; });
			
            bucket.SetValues(waitTimeout, ReplyEvent, idItem);

            return waitId;
        }

        internal void ReleaseWait(short waitId)//, int replyStream)
        {
            ReplyBucket bucket = null;
        	replyLock.Read(() =>
        	          	{
							if (!replies.ContainsKey(waitId))
							{
								//LoggingWrapper.Write("Got reply for message " + messageId.ToString() + ", but no reply bucket exists for that id! Adding bucket and reply.", "Socket Client");
								if (Log.IsDebugEnabled)
								{
									Log.DebugFormat("ReleaseWait() Got reply for message {0}, but no reply bucket exists for that id! Adding bucket and reply."
										, waitId);
								}
							}
							else
							{
								if (Log.IsDebugEnabled)
								{
									Log.DebugFormat("ReleaseWait() Got reply for message {0}"
										, waitId);
								}
								bucket = replies[waitId];
							}
        	          	});

            if (bucket != null)
            {
                bucket.ReleaseWait();
            }
        }

        internal void WaitForReply(short waitId)
        {
            ReplyBucket replyBucket = null;

        	replyLock.Read(() =>
        	          	{
							if (Log.IsDebugEnabled)
							{
								Log.DebugFormat("WaitForReply() Gets reply for waitId {0}", waitId);
							}
							replyBucket = replies[waitId];      		
        	          	});
            //don't want to hold the lock while we wait, so just get the reference then wait
            replyBucket.WaitForReply(waitId);
        }

        #region IDisposable Implementation Methods
        // Implement IDisposable.
        // Do not make this method virtual.
        // A derived class should not be able to override this method.
        public void Dispose()
        {
            Dispose(true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue 
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }
        #endregion
    }

    internal class ReplyBucket
    {
        private static LogWrapper Log = new LogWrapper();
        
        private EventWaitHandle waitHandle;
        private int timeOut; //how long to wait for a reply before throwing a timeout exception		
        private ResourcePoolItem<short> idItem;

        internal ReplyBucket()
        {
        }

        internal ReplyBucket(int timeout, EventWaitHandle waitHandle, ResourcePoolItem<short> idItem)
        {
            SetValues(timeout, waitHandle, idItem);
        }

        internal void ReleaseWait()
        {
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("[{0}] {1}: {2}:ReleaseWait() Reply Recieved");
            }
            waitHandle.Set();
        }

        internal void SetValues(int timeout, EventWaitHandle waitHandle, ResourcePoolItem<short> idItem)
        {
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("SetValues() Sets WaitHandle");
            }
            this.timeOut = timeout;
            this.waitHandle = waitHandle;
            this.idItem = idItem;
        }

        internal void WaitForReply(short waitId)
        {
            ResourcePoolItem<short> idItem = this.idItem; //need a reference to this so we can release it LAST and avoid a race condition
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("WaitForReply() Waits for waitId {0} to be released.", waitId);
            }
            if (this.waitHandle.WaitOne(timeOut, false))
            {
                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("WaitForReply() waitId {0} is released.", waitId);
                }
                this.idItem = null;
                idItem.Release();
                this.waitHandle = null;
            }
            else
            {
                this.idItem = null;
                idItem.Release();
                this.waitHandle = null;
                //ApplicationException ex = new ApplicationException("TimeOut exception.");
                if (Log.IsErrorEnabled)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendFormat("WaitForReply() WaitHandle is timed out for waitId {0}", waitId);
                    Log.Error(sb.ToString());
                }
                //throw ex;
            }
        }
    }
}
