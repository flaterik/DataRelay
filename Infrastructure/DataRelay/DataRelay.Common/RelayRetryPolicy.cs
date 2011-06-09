
namespace MySpace.DataRelay
{
    /// <summary>
    /// The retry policy for when a message cannot be handled by Data Relay
    /// </summary>
    public enum RelayRetryPolicy
    {
        /// <summary>
        /// Allows relay messages to be retried (to a different node) when the request was Node Unreachable.  Timeouts will still be a miss (and usually indicate that
        /// a fallback to a database is required to fetch the item.)
        /// /// </summary>
        UnreachableNodesOnly = 0,  // 0 will be the default value if not specified
        /// <summary>
        /// Allows relay messages to be retried (to a different node) when the request was Node Unreachable or timed out.
        /// </summary>
        UnreachableNodesOrTimeout
    }
}
