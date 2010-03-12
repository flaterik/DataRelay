using MySpace.Logging;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils
{
    internal static class LoggingUtil
    {
        #region Data Member

        static LoggingUtil()
        {
            Log = new LogWrapper();
        }

        /// <summary>
        /// Gets or sets the log.
        /// </summary>
        /// <value>The log.</value>
        internal static LogWrapper Log
        {
            get; private set;
        }

        #endregion
    }
}