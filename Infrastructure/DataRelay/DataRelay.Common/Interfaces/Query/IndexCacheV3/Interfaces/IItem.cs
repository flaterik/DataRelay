namespace MySpace.DataRelay.Interfaces.Query.IndexCacheV3
{
    public interface IItem
    {
        /// <summary>
        /// Tries the get tag value.
        /// </summary>
        /// <param name="tagName">Name of the tag.</param>
        /// <param name="tagValue">The tag value.</param>
        /// <returns></returns>
        bool TryGetTagValue(string tagName, out byte[] tagValue);

        /// <summary>
        /// Gets the item id.
        /// </summary>
        /// <value>The item id.</value>
        byte[] ItemId { get; }
    }
}
