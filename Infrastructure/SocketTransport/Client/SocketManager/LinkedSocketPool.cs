using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using Wintellect.PowerCollections;

namespace MySpace.SocketTransport
{

   internal class LinkedManagedSocket : ManagedSocket
   {
      internal LinkedManagedSocket Next;

      internal LinkedManagedSocket(SocketSettings settings, SocketPool pool)
         : base(settings, pool)
      {
      }
   }

   internal class LinkedSocketPortOnlyComparer : IEqualityComparer<LinkedManagedSocket>
   {
      #region IEqualityComparer<LinkedManagedSocket> Members

      public bool Equals(LinkedManagedSocket x, LinkedManagedSocket y)
      {
         return ((IPEndPoint)x.LocalEndPoint).Port == ((IPEndPoint)y.LocalEndPoint).Port;
      }

      public int GetHashCode(LinkedManagedSocket obj)
      {
         return ((IPEndPoint)obj.LocalEndPoint).Port;
      }

      #endregion
   }

   internal class LinkedManagedSocketPool : SocketPool
   {
      private bool useLimiter;
      private Semaphore socketLimiter;
      private int socketLimiterValue;

      private Set<LinkedManagedSocket> sockets;
      private int connectTimeout = 5000;

      private readonly object setLock = new object();
      private readonly object padLock = new object();

      private LinkedManagedSocket nextSocket;
      Thread t = null;
      private int spareSockets = 0;
      private const int minWaitingSockets = 3;
      private static int endpoints = 0;
      private static object endLock = new object();


      internal LinkedManagedSocketPool(IPEndPoint destination, SocketSettings settings)
         : base(destination, settings)
      {
         sockets = new Set<LinkedManagedSocket>(new LinkedSocketPortOnlyComparer());
         lock (endLock)
         {
            endpoints++;
         }

         if (settings.PoolSize > 0)
         {
            useLimiter = true;
            socketLimiter = new Semaphore(settings.PoolSize, settings.PoolSize);
            socketLimiterValue = settings.PoolSize;
         }
         else
         {
            //t = new Thread(new ThreadStart(PoolFiller));
            //t.Start();
         }
         connectTimeout = settings.ConnectTimeout;
      }

      public void PoolFiller()
      {
         do
         {
            Thread.Sleep(10000);
            try
            {
               List<LinkedManagedSocket> disposeList = null;
               lock (padLock)
               {
                  //// count the number left
                  //LinkedManagedSocket socketPointer = nextSocket;
                  //int counter = 0;
                  //while (socketPointer != null)
                  //{
                  //   socketPointer = socketPointer.Next;
                  //   counter++;
                  //}
                  //log.InfoFormat("{0} has {1} spares at start", destination, counter);

                  LinkedManagedSocket socketPointer = nextSocket;
                  while (socketPointer != null)
                  {
                     if (SocketAgedOut(socketPointer))
                     {
                        //log.InfoFormat("removing aged out socket to destination {0}, socket {1}", destination, socketPointer.Handle);
                        LinkedManagedSocket expiredSocket = socketPointer;
                        socketPointer = socketPointer.Next;
                        //if (expiredSocket == null)
                        //   log.InfoFormat("expiredSocket was null");
                        PullSocketFromRotation(expiredSocket);
                        spareSockets--;
                        if (disposeList == null) disposeList = new List<LinkedManagedSocket>();
                        disposeList.Add(expiredSocket);
                     }
                     else
                        socketPointer = socketPointer.Next;
                  }

                  //// count the number left
                  //socketPointer = nextSocket;
                  //counter = 0;
                  //while (socketPointer != null)
                  //{
                  //   socketPointer = socketPointer.Next;
                  //   counter++;
                  //}
                  //log.InfoFormat("{0} has {1} spares after", destination, counter);
               }
               if (disposeList != null) disposeList.ForEach(expiredSocket => DisposeSocket(expiredSocket, false));
            }
            catch (Exception ex)
            {
               log.InfoFormat("poolfiller exception {0}", ex.Message);
            }
         } while (true);
      }

      //public void PoolFiller()
      //{
      //   do
      //   {
      //      Thread.Sleep(60000);
      //      try
      //      {
      //         int spares = 0;
      //         lock (padLock)
      //         {
      //            spares = spareSockets;
      //            //if ((spares = spareSockets) < 0)
      //            //   spares = spareSockets = 0;
      //         }

      //         while (spares < minFreeSocket)
      //         {
      //            log.InfoFormat("only {0} spares for pool {1}.  activeSocketCount:{2}, total endpoint: {3}", spares, destination, activeSocketCount, endpoints);

      //            var socket = BuildSocket();
      //            socket.Idle = true;

      //            lock (padLock)
      //            {
      //               if (nextSocket == null)
      //               {
      //                  nextSocket = (LinkedManagedSocket)socket;
      //               }
      //               else
      //               {
      //                  LinkedManagedSocket newNextSocket = (LinkedManagedSocket)socket;
      //                  LinkedManagedSocket currentNextSocket = nextSocket;
      //                  newNextSocket.Next = currentNextSocket;
      //                  nextSocket = newNextSocket;
      //               }
      //               ++spareSockets;
      //               log.InfoFormat("Created spare socket {0}", spares);
      //            }
      //         }
      //      }
      //      catch (Exception ex)
      //      {
      //         log.InfoFormat("poolfiller exception {0}", ex.Message);
      //      }
      //   } while (true);
      //}

      private LinkedManagedSocket BuildSocket()
      {
         LinkedManagedSocket socket = new LinkedManagedSocket(this.Settings, this);
         socket.Connect(this.destination, this.Settings.ConnectTimeout);
         Interlocked.Increment(ref socketCount);
         return socket;
      }

      internal override ManagedSocket GetSocket()
      {
         if (!EnterLimiter()) throw new SocketException((int)SocketError.TooManyOpenSockets);

         List<LinkedManagedSocket> disposeList = null;
         try
         {
            lock (padLock)
            {
               while (nextSocket != null)
               {
                  // async receive could set an error at any time.
                  if (nextSocket.LastError == SocketError.Success)
                  {
                     var foundSocket = nextSocket;

                     // here we the first non-errored socket off of the linked list. 
                     foundSocket.Idle = false;
                     nextSocket = foundSocket.Next;
                     spareSockets--;
                     foundSocket.Next = null;
                     Interlocked.Increment(ref activeSocketCount);
                     return foundSocket;
                  }
                  else
                  {
                     // not null, has error
                     LinkedManagedSocket badSocket = nextSocket;
                     log.DebugFormat("Socket not used in state {0}", badSocket.LastError);

                     nextSocket = nextSocket.Next; // look at the next one
                     spareSockets--;
                     badSocket.Next = null;
                     if (disposeList == null) disposeList = new List<LinkedManagedSocket>();
                     disposeList.Add(badSocket);
                  }
               }
            }
            if (disposeList != null)
            {
               try
               {
                  disposeList.ForEach(badSocket => DisposeSocket(badSocket, false));
               }
               catch (Exception ex)
               {
                  log.InfoFormat("exception disposing sockets {0}", ex.Message);
               }
               disposeList = null;
            }

            // else, make a new one. which.. probably should not be able to happen.
            var socket = BuildSocket();
            lock (setLock)
            {
               sockets.Add(socket);
            }
            Interlocked.Increment(ref activeSocketCount);
            return socket;
         }
         catch
         {
            ExitLimiter();
            throw;
         }
         finally
         {
            if (disposeList != null) disposeList.ForEach(badSocket => DisposeSocket(badSocket, false));
         }
      }

      private bool EnterLimiter()
      {
         if (useLimiter)
         {
            //enter the semaphore. It starts at MaxValue, and is incremented when a socket is released or disposed.
            if (socketLimiter.WaitOne(connectTimeout, false))
            {
               Interlocked.Decrement(ref socketLimiterValue);
               return true;
            }
            return false;
         }

         return true;
      }

      private void ExitLimiter()
      {
         if (useLimiter)
         {
            try
            {
               socketLimiter.Release();
               Interlocked.Increment(ref socketLimiterValue);
            }
            catch (SemaphoreFullException)
            {
               if (log.IsErrorEnabled)
                  log.ErrorFormat("Socket pool for {0} released a socket too many times.", destination);
            }
         }
      }

      internal override void ReleaseSocket(ManagedSocket socket)
      {
         try
         {
            if (socket.LastError != SocketError.Success || SocketAgedOut(socket))
            {
               //log.InfoFormat("releasing socket to {0} with status {1}", destination, socket.LastError);
               DisposeSocket(socket);
            }
            else
            {
               if (!socket.Idle)
               {
                  lock (padLock)
                  {
                     socket.Idle = true;
                     if (nextSocket == null)
                     {
                        nextSocket = (LinkedManagedSocket)socket;
                        spareSockets++;
                     }
                     else
                     {
                        LinkedManagedSocket newNextSocket = (LinkedManagedSocket)socket;
                        LinkedManagedSocket currentNextSocket = nextSocket;
                        newNextSocket.Next = currentNextSocket;
                        nextSocket = newNextSocket;
                        spareSockets++;
                     }
                     ExitLimiter();
                  }
                  Interlocked.Decrement(ref activeSocketCount);
               }
            }
         }
         catch (Exception ex)
         {
            if (log.IsErrorEnabled)
               log.ErrorFormat("Exception releasing socket: {0}", ex);
         }
      }

      private void DisposeSocket(ManagedSocket socket)
      {
         DisposeSocket(socket, true);
      }

      private void DisposeSocket(ManagedSocket socket, bool pullFromRotation)
      {
         bool exitLimiter = false;
         if (socket != null)
         {
            try
            {
               //log.InfoFormat("{0}Checking socket idle for socket {1}", destination, socket.Handle);
               if (!socket.Idle)
               {
                  exitLimiter = true;
                  Interlocked.Decrement(ref activeSocketCount);
               }
               //log.InfoFormat("{0}pull from rotation for socket {1}", destination, socket.Handle);
               if (pullFromRotation)
               {
                  PullSocketFromRotation((LinkedManagedSocket)socket);
               }
               //log.InfoFormat("{0}setting idle for socket {1}", destination, socket.Handle);
               socket.Idle = false;
               Interlocked.Decrement(ref socketCount);

               lock (setLock)
               {
                  //log.InfoFormat("{0}Removing socket {1}", destination, socket.Handle);
                  sockets.Remove((LinkedManagedSocket)socket);
               }

               if (socket.Connected)
               {
                  //log.InfoFormat("{0}Shuttign down socket {1}", destination, socket.Handle);
                  socket.Shutdown(SocketShutdown.Both);
               }
               //log.InfoFormat("{0}Closign socket {1}", destination, socket.Handle);
               socket.Close();
            }
            catch (SocketException)
            { }
            catch (ObjectDisposedException)
            {
               exitLimiter = false;
               if (log.IsErrorEnabled)
                  log.ErrorFormat("Attempt to release and dispose disposed socket by pool for {0}, socket {1}", destination, socket.Handle);
            }
            finally
            {
               if (exitLimiter)
               {
                  ExitLimiter();
               }
            }
         }

      }

      private void PullSocketFromRotation(LinkedManagedSocket socket)
      {
         lock (padLock)
         {
            if (nextSocket != null)
            {
               if (socket == nextSocket)
               {
                  //log.InfoFormat("{0} pulling socket {1} from head", destination, socket.Handle);
                  nextSocket = nextSocket.Next;
                  spareSockets--;
               }
               else
               {
                  LinkedManagedSocket pointer = nextSocket.Next;
                  LinkedManagedSocket prevPointer = null;
                  while (pointer != null && pointer != socket)
                  {
                     prevPointer = pointer;
                     pointer = pointer.Next;
                  }
                  if (pointer == socket && prevPointer != null && pointer != null)
                  {
                     //log.InfoFormat("{0} pulling socket {1} from middle", destination, pointer.Handle);
                     prevPointer.Next = pointer.Next; //skip over it!
                     spareSockets--;
                  }
               }
            }
         }

      }

      internal override void ReleaseAndDisposeAll()
      {
         lock (padLock)
         {
            foreach (LinkedManagedSocket socket in sockets)
            {
               try
               {
                  if (socket.Connected)
                  {
                     socket.Shutdown(SocketShutdown.Both);
                  }
                  socket.Close();
                  Interlocked.Decrement(ref activeSocketCount);
                  Interlocked.Decrement(ref socketCount);
               }
               catch (SocketException)
               {
               }
               catch (ObjectDisposedException)
               {
               }
            }
            nextSocket = null;
         }
      }

   }

}
