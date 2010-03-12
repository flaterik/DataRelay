using System.Collections.Generic;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store
{
    internal class InternalItem : IItem
    {
        #region Data members

        /// <summary>
        /// Gets or sets the item id.
        /// </summary>
        /// <value>The item id.</value>
        public byte[] ItemId
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the tag list.
        /// </summary>
        /// <value>The tag list.</value>
        internal List<KeyValuePair<int, byte[]>> TagList
        {
            get; set;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Updates the tag.
        /// </summary>
        /// <param name="tagHashCode">The tag hash code.</param>
        /// <param name="tagValue">The tag value.</param>
        internal void UpdateTag(int tagHashCode, byte[] tagValue)
        {
            if(TagList == null)
            {
                TagList = new List<KeyValuePair<int, byte[]>>();
            }
            else
            {
                for (int i = TagList.Count - 1; i > -1; i--)
                {
                    if (TagList[i].Key == tagHashCode)
                    {
                        TagList.RemoveAt(i);
                        TagList.Insert(i, new KeyValuePair<int, byte[]>(tagHashCode, tagValue));
                        return;
                    }
                }
            }
            TagList.Add(new KeyValuePair<int, byte[]>(tagHashCode, tagValue));
        }

        #endregion

        #region IItem Members

        /// <summary>
        /// Tries the get tag value.
        /// </summary>
        /// <param name="tagName">Name of the tag.</param>
        /// <param name="tagValue">The tag value.</param>
        /// <returns></returns>
        public bool TryGetTagValue(string tagName, out byte[] tagValue)
        {
            tagValue = null;
            if (TagList != null && TagList.Count > 0 && tagName != null)
            {
                int tagHashCode = TagHashCollection.GetTagHashCode(tagName);
                for (int i = 0; i < TagList.Count; i++)
                {
                    if (TagList[i].Key == tagHashCode)
                    {
                        tagValue = TagList[i].Value;
                        return true;
                    }
                }
            }
            return false;
        }

        #endregion
    }
}
