using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using HttpContext = System.Web.HttpContext;
using HostingEnvironment = System.Web.Hosting.HostingEnvironment;
using System.Threading;
using LogWrapper = MySpace.Logging.LogWrapper;


namespace MySpace.Common.ChangeNotification
{
    /// <summary>
    /// The FileStalker class is used to monitor specific files for 
    /// modifications by monitoring them closely. This class does not use
    /// any win32 apis and will work against a UNC share. This method was 
    /// devised due to issues with notifications when hosting IIS off of a
    /// NetApp filer. 
    /// </summary>
    public sealed class FileStalker:IDisposable
    {
        public const int MAX_NOTIFICATION_FREQUENCY = (1000 * 60);//Only check every minute
        private static readonly LogWrapper log = new LogWrapper();
        private string _FileToWatch;
        internal readonly int ID = Environment.TickCount;
        
        /// <summary>
        /// Event that is fired when a file is modified.
        /// </summary>
        public event EventHandler<FileModifiedEventArgs> FileModified;
        /// <summary>
        /// Path the FileStalker is currently configured to watch. This path will 
        /// be the fully qualified path to the file. 
        /// </summary>
        public string FileToWatch
        {
            get { return _FileToWatch; }
        }

        /// <summary>
        /// Creates a new instance of the FileStalker class.
        /// </summary>
        /// <param name="FileToWatch">Path to the file to watch. If the FilePath specified is not
        /// a full rooted file the file will be pathed based of the currently running application domain.
        /// <example>ConfigurationFile\Test.config = F:\websites\MySpace.Web.2.5\ConfigurationFile\Test.config (Assuming the web is running in F:\websites\MySpace.Web.2.5)</example>
        /// </param>
        public FileStalker(string fileToWatch)
        {
            if (string.IsNullOrEmpty(fileToWatch)) throw new ArgumentNullException("FileToWatch", "FileToWatch cannot be null.");
            if (Path.IsPathRooted(fileToWatch))
                _FileToWatch = fileToWatch;
            else
            {
                if (null != HttpContext.Current)
                {
                    _FileToWatch = Path.Combine(HostingEnvironment.ApplicationPhysicalPath, fileToWatch);
                }
                else
                {
                    _FileToWatch = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileToWatch);
                }
            }

            addFileStalker(this);
        }


        /// <summary>
        /// Class is used to store the state of a file between notifications 
        /// </summary>
        class FileState : IEquatable<FileInfo>
        {
            /// <summary>
            /// The last modified date of the file
            /// </summary>
            public DateTime FileDate;
            /// <summary>
            /// The Size of the file on disk.
            /// </summary>
            public long FileSize;
            /// <summary>
            /// The last time a notification was sent for the file. 
            /// </summary>
            public DateTime LastNotification;
            /// <summary>
            /// FileStalkers registered to the file.  
            /// </summary>
            public FileStalker[] FileStalkers;

            /// <summary>
            /// Creates a new instance of the FileState class
            /// </summary>
            /// <param name="fileDate">LastModified date of the file.</param>
            /// <param name="fileSize">Size of the file.</param>
            /// <param name="stalker">FileStalker to register the notification to</param>
            public FileState(DateTime fileDate, long fileSize, FileStalker stalker)
            {
                this.FileDate = fileDate;
                this.FileSize = fileSize;
                this.LastNotification = DateTime.UtcNow;
                this.FileStalkers = new FileStalker[] { stalker };
            }

            public bool Equals(FileInfo other)
            {
                if (!other.Exists)
                    return true;
                if (other.LastWriteTimeUtc == this.FileDate &&
                    other.Length == this.FileSize)
                    return true;
                TimeSpan ts = DateTime.UtcNow - LastNotification;
                if (ts.TotalMilliseconds < MAX_NOTIFICATION_FREQUENCY)
                    return true;
                return false;
            }

            public void Update(FileInfo info)
            {
                this.FileDate = info.LastWriteTimeUtc;
                this.FileSize = info.Length;
                this.LastNotification = DateTime.UtcNow;
            }

            public void Notify()
            {
                foreach (FileStalker stalker in FileStalkers)
                {
                    /* Give the update thread a dedicated thread since the thread pool could get starved
                     * or we could just sit in the queue for a while 
                     * */
                    Thread updateThread = new Thread(new ThreadStart(stalker.Notify));
                    updateThread.IsBackground = true;
                    updateThread.Start();
                }
            }
        }

        internal void Notify()
        {
            try
            {
                if (null != FileModified)
                    FileModified(this, new FileModifiedEventArgs(this));
            }
            catch (Exception ex)
            {
                log.Error("Exception thrown calling FileModified", ex);
            }
        }

        private static Thread monitorThread;

        private static Dictionary<string, FileState> _State;
        private static object _StateLock=new object();

        static FileStalker()
        {
            _State = new Dictionary<string, FileState>(StringComparer.OrdinalIgnoreCase);
            monitorThread = new Thread(new ThreadStart(monitorChanges));
            monitorThread.IsBackground = true;
            monitorThread.Start();
        }

        /// <summary>
        /// Method is used to walk the file system and check files for against
        /// </summary>
        private static void monitorChanges()
        {
            while (true)
            {
                string[] stalkedFiles = null;

                lock (_StateLock)
                {
                    stalkedFiles = new string[_State.Count];
                    _State.Keys.CopyTo(stalkedFiles, 0);
                }

                try
                {
                    foreach (string StalkedFile in stalkedFiles)
                    {
                        FileState state = null;// _State[StalkedFile];
                        bool found = false;
                        lock (_StateLock)
                        {
                            found = _State.TryGetValue(StalkedFile, out state);
                        }

                        if (!found)
                        {
                            if (log.IsWarnEnabled) log.WarnFormat("\"{0}\" was marked as a stalked file but was not found in _state??? Skipping...", StalkedFile);
                            continue;
                        }

                        try
                        {
                            FileInfo file = new FileInfo(StalkedFile);

                            if (!state.Equals(file))
                            {
                                state.Update(file);
                                state.Notify();
                            }
                        }
                        catch (Exception ex0)
                        {
                            log.Warn("Exception thrown", ex0); 
                        }

                        //Sleep between each iteration to spread the operations over a couple seconds.
                        Thread.Sleep(1000);
                    }

                    Thread.Sleep(MAX_NOTIFICATION_FREQUENCY);
                }
                catch (Exception ex)
                {
                    log.Warn("Exception thrown", ex);
                }
            }
        }

        private static void addFileStalker(FileStalker stalker)
        {
            lock (_StateLock)
            {
                FileState state = null;
                FileInfo info = new FileInfo(stalker.FileToWatch);

                if (!_State.TryGetValue(stalker.FileToWatch, out state))
                {
                    if (!info.Exists)
                    {
                        state = new FileState(DateTime.MinValue, -1L, stalker);
                    }
                    else
                    {
                        state = new FileState(info.LastWriteTimeUtc, info.Length, stalker);
                    }
                    _State.Add(stalker.FileToWatch, state);
                    return;
                }

                FileStalker[] newStalkers = new FileStalker[state.FileStalkers.Length + 1];
                Array.Copy(state.FileStalkers, newStalkers, state.FileStalkers.Length);
                newStalkers[newStalkers.Length - 1] = stalker;
                state.FileStalkers = newStalkers;
            }
        }
        private static void removeFileStalker(FileStalker disposedStalker)
        {
            FileState state = null;
            lock (_StateLock)
            {
                if (!_State.TryGetValue(disposedStalker.FileToWatch, out state))
                    return;

                List<FileStalker> stalkers = new List<FileStalker>();
                
                foreach (FileStalker stalker in state.FileStalkers)
                {
                    if (disposedStalker.ID == stalker.ID)
                        continue;
                    stalkers.Add(stalker);
                }
                state.FileStalkers = stalkers.ToArray();
            }
        }
        public void Dispose()
        {
            removeFileStalker(this);
        }
    }
}
