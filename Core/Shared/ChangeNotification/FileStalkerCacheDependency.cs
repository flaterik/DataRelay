using System;
using System.Collections.Generic;
using System.Text;
using CacheDependency = System.Web.Caching.CacheDependency;

namespace MySpace.Common.ChangeNotification
{
    /// <summary>
    /// Class is used as a cache dependecy for Asp.Net
    /// </summary>
    public sealed class FileStalkerCacheDependency : CacheDependency
    {
        FileStalker _Stalker;

        /// <summary>
        /// Generates a unique id for the content.
        /// </summary>
        /// <returns>Unique ID</returns>
        public override string GetUniqueID()
        {
            return string.Format("FileStalker({0})", _Stalker.FileToWatch.ToLower());        
        }

        /// <summary>
        /// Creates a new instance of the FileStalkerCacheDependency.
        /// </summary>
        /// <param name="FilePath">Path to the file to monitor.</param>
        public FileStalkerCacheDependency(string FilePath)
        {
            _Stalker = new FileStalker(FilePath);
            _Stalker.FileModified += new EventHandler<FileModifiedEventArgs>(NotifyDependencyChanged);
            SetUtcLastModified(DateTime.MaxValue);
        }
        /// <summary>
        /// Dispose
        /// </summary>
        protected override void DependencyDispose()
        {
            _Stalker.Dispose();
            base.DependencyDispose();
        }
    }
}
