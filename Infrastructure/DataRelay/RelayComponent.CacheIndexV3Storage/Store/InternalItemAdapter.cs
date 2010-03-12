using System.Collections.Generic;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store
{
    internal static class InternalItemAdapter
    {
        /// <summary>
        /// Converts to tag dictionary.
        /// </summary>
        /// <param name="tagList">The tag list.</param>
        /// <param name="inDeserializationContext">The InDeserializationContext.</param>
        /// <returns></returns>
        internal static Dictionary<string /*TagName*/, byte[] /* TagValue*/> ConvertToTagDictionary(
            List<KeyValuePair<int /*TagHashCode*/, byte[] /*TagName*/>> tagList,
            InDeserializationContext inDeserializationContext)
        {
            Dictionary<string /*TagName*/, byte[] /*TagValue*/> tagsDictionary = null;

            if (tagList != null && tagList.Count > 0)
            {
                tagsDictionary = new Dictionary<string, byte[]>(tagList.Count);
                foreach (KeyValuePair<int /*TagName*/, byte[] /*TagValue*/> kvp in tagList)
                {
                    string tagName = inDeserializationContext.TagHashCollection.GetTagName(inDeserializationContext.TypeId, kvp.Key);
                    if (!tagsDictionary.ContainsKey(tagName))
                    {
                        tagsDictionary.Add(tagName, kvp.Value);
                    }
                }
            }
            return tagsDictionary;
        }

        /// <summary>
        /// Converts to tag list.
        /// </summary>
        /// <param name="tagsDictionary">The tags dictionary.</param>
        /// <returns></returns>
        internal static List<KeyValuePair<int /*TagHashCode*/, byte[] /*TagName*/>> ConvertToTagList(
            Dictionary<string /*TagName*/, byte[] /* TagValue*/> tagsDictionary)
        {
            List<KeyValuePair<int, byte[]>> tagsList = null;

            if (tagsDictionary != null && tagsDictionary.Count > 0)
            {
                tagsList = new List<KeyValuePair<int, byte[]>>(tagsDictionary.Count);
                foreach (KeyValuePair<string, byte[]> kvp in tagsDictionary)
                {
                    tagsList.Add(new KeyValuePair<int, byte[]>(TagHashCollection.GetTagHashCode(kvp.Key), kvp.Value));
                }
            }
            return tagsList;
        }

        /// <summary>
        /// Converts IndexItem to an InternalItem.
        /// </summary>
        /// <param name="indexItem">The index item.</param>
        /// <returns></returns>
        internal static InternalItem ConvertToInternalItem(IndexItem indexItem)
        {
            return new InternalItem {ItemId = indexItem.ItemId, TagList = ConvertToTagList(indexItem.Tags)};
        }

        /// <summary>
        /// Converts an InternalItem to an IndexItem.
        /// </summary>
        /// <param name="internalItem">The internal item.</param>
        /// <param name="inDeserializationContext">The in deserialization context.</param>
        /// <returns></returns>
        internal static IndexItem ConvertToIndexItem(InternalItem internalItem, InDeserializationContext inDeserializationContext)
        {
            return new IndexItem(internalItem.ItemId, ConvertToTagDictionary(internalItem.TagList, inDeserializationContext));
        }

    }
}
