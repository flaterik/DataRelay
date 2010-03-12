using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Microsoft.Ccr.Core;

using MySpace.Logging;

namespace MySpace.DataRelay.RelayComponent.BerkeleyDb
{
    delegate void PostMessageDelegate(QueueItem queueItem);

    class ThrottledQueue : IDisposable
    {
        private static LogWrapper Log = new LogWrapper();
        
        private bool isDisposed = false;
        private Dispatcher dispatcher = null;
        private DispatcherQueue dispatcherQueue = null;
        private Port<QueueItem> messagePort = null;
        private MatchMaker matchMaker = null;

        public ThrottledQueue(string threadPoolName, string dispatcherQueueName, 
            PostMessageDelegate postMessage, int maxPoolItemReuse)
        {
            matchMaker = new MatchMaker(maxPoolItemReuse);
            dispatcher = new Dispatcher(1, threadPoolName);
            dispatcherQueue = new DispatcherQueue(dispatcherQueueName, dispatcher);
            messagePort = new Port<QueueItem>();
            Handler<QueueItem> handler = new Handler<QueueItem>(postMessage);
            Arbiter.Activate(dispatcherQueue, Arbiter.Receive(true, messagePort, handler));
        }

        // Use C# destructor syntax for finalization code.
		// This destructor will run only if the Dispose method 
		// does not get called.
		// It gives your base class the opportunity to finalize.
		// Do not provide destructors in types derived from this class.
        ~ThrottledQueue()
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
                    if (dispatcher != null)
                    {
                        dispatcher.Dispose();
                    }
                    if (matchMaker != null)
                    {
                        matchMaker.Dispose();
                    }
                    if (Log.IsInfoEnabled)
                    {
                        Log.InfoFormat("Dispose() ThrottledQueue is Disposed");
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

        public void ReleaseWait(short waitId)
        {
            //int reply = 0;
            matchMaker.ReleaseWait(waitId);//, reply);
        }

        public short SetWaitHandle(int waitTimeout)
        {
            return matchMaker.SetWaitHandle(waitTimeout);
        }

        public void Post(RelayMessage message)
        {
            Post(message, 0);
        }

        public void Post(RelayMessage message, short waitId)
        {
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("Post() Port posts message to DispatcherQueue '{0}' (TypeId={1}, MessageId={2})"
                    , dispatcherQueue.Name, message.TypeId, message.Id);
            }
            QueueItem queueItem = new QueueItem();
            queueItem.WaitId = waitId;
            queueItem.Message = message;
            messagePort.Post(queueItem);
        }

        public void WaitForReply(short waitId)
        {
            matchMaker.WaitForReply(waitId);
        }

        public int Count
        {
            get
            {
                return dispatcherQueue.Count;
            }
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

    class QueueItem
    {
        public short WaitId;
        public RelayMessage Message;
    }
}
