using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.Common.ChangeNotification
{
    /// <summary>
    /// Event that is passed when a File has been modified. 
    /// </summary>
    public sealed class FileModifiedEventArgs : EventArgs
    {
        internal FileModifiedEventArgs(FileStalker stalker)
        {
            _Stalker = stalker;
        }

        private FileStalker _Stalker;

        /// <summary>
        /// FileStalker that caused the notification.
        /// </summary>
        public FileStalker Stalker
        {
            get { return _Stalker; }
        }
        /// <summary>
        /// Path to the file that was modified. 
        /// </summary>
        public string FilePath { get { return _Stalker.FileToWatch; } }
    }
}
