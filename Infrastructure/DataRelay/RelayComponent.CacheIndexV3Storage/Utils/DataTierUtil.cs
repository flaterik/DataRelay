using System;
using System.Collections.Generic;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Enums;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils
{
    /// <summary>
    /// Provides utility methods related to getting and saving data to Data Tier
    /// </summary>
    internal static class DataTierUtil
    {
        /// <summary>
        /// Gets the data into resultItemList items.
        /// </summary>
        /// <param name="resultItemList">The result item list.</param>
        /// <param name="storeContext">The store context.</param>
        /// <param name="messageContext">The message context.</param>
        /// <param name="fullDataIdFieldList">The full data id field list.</param>
        /// <param name="fullDataIdInfo">The full data id info.</param>
        internal static void GetData(List<ResultItem> resultItemList,
            IndexStoreContext storeContext,
            MessageContext messageContext,
            FullDataIdFieldList fullDataIdFieldList,
            FullDataIdInfo fullDataIdInfo)
        {
            byte[] extendedId;
            List<RelayMessage> dataStoreMessages = new List<RelayMessage>();
            short relatedTypeId;
            if (fullDataIdInfo != null && fullDataIdInfo.RelatedTypeName != null)
            {
                if (!storeContext.TryGetTypeId(fullDataIdInfo.RelatedTypeName, out relatedTypeId))
                {
                    LoggingUtil.Log.ErrorFormat("Invalid RelatedCacheTypeName - {0}", fullDataIdInfo.RelatedTypeName);
                    throw new Exception("Invalid RelatedTypeId for TypeId - " + fullDataIdInfo.RelatedTypeName);
                }
            }
            else if (!storeContext.TryGetRelatedIndexTypeId(messageContext.TypeId, out relatedTypeId))
            {
                LoggingUtil.Log.ErrorFormat("Invalid RelatedTypeId for TypeId - {0}", messageContext.TypeId);
                throw new Exception("Invalid RelatedTypeId for TypeId - " + messageContext.TypeId);
            }

            if (resultItemList != null && resultItemList.Count > 0)
            {
                foreach (ResultItem resultItem in resultItemList)
                {
                    extendedId = GetFullDataId(resultItem.IndexId,
                        resultItem,
                        fullDataIdInfo != null && fullDataIdInfo.RelatedTypeName != null ? fullDataIdInfo.FullDataIdFieldList : fullDataIdFieldList);
                    dataStoreMessages.Add(new RelayMessage(relatedTypeId,
                                              IndexCacheUtils.GeneratePrimaryId(extendedId),
                                              extendedId,
                                              MessageType.Get));
                }

                storeContext.ForwarderComponent.HandleMessages(dataStoreMessages);

                int i = 0;
                foreach (ResultItem resultItem in resultItemList)
                {
                    if (dataStoreMessages[i].Payload != null)
                    {
                        resultItem.Data = dataStoreMessages[i].Payload.ByteArray;
                    }
                    else
                    {
                        LoggingUtil.Log.DebugFormat("Fetched Null Data for TypeId: {0}, IndexId: {1}, ItemId: {2}, FullDataId: {3}, PrimaryId: {4}",
                            relatedTypeId,
                            IndexCacheUtils.GetReadableByteArray(resultItem.IndexId),
                            IndexCacheUtils.GetReadableByteArray(resultItem.ItemId),
                            IndexCacheUtils.GetReadableByteArray(dataStoreMessages[i].ExtendedId),
                            IndexCacheUtils.GeneratePrimaryId(dataStoreMessages[i].ExtendedId)
                            );
                    }
                    i++;
                }
            }
        }

        /// <summary>
        /// Gets the data into indexDataItemList items.
        /// </summary>
        /// <param name="indexDataItemList">The index data item list.</param>
        /// <param name="storeContext">The store context.</param>
        /// <param name="messageContext">The message context.</param>
        /// <param name="fullDataIdFieldList">The full data id field list.</param>
        /// <param name="fullDataIdInfo">The full data id info.</param>
        internal static void GetData(List<IndexDataItem> indexDataItemList,
            IndexStoreContext storeContext,
            MessageContext messageContext,
            FullDataIdFieldList fullDataIdFieldList,
            FullDataIdInfo fullDataIdInfo)
        {
            byte[] extendedId;
            List<RelayMessage> dataStoreMessages = new List<RelayMessage>();
            short relatedTypeId;
            if (fullDataIdInfo != null && fullDataIdInfo.RelatedTypeName != null)
            {
                if (!storeContext.TryGetTypeId(fullDataIdInfo.RelatedTypeName, out relatedTypeId))
                {
                    LoggingUtil.Log.ErrorFormat("Invalid RelatedCacheTypeName - {0}", fullDataIdInfo.RelatedTypeName);
                    throw new Exception("Invalid RelatedTypeId for TypeId - " + fullDataIdInfo.RelatedTypeName);
                }
            }
            else if (!storeContext.TryGetRelatedIndexTypeId(messageContext.TypeId, out relatedTypeId))
            {
                LoggingUtil.Log.ErrorFormat("Invalid RelatedTypeId for TypeId - {0}", messageContext.TypeId);
                throw new Exception("Invalid RelatedTypeId for TypeId - " + messageContext.TypeId);
            }

            if (indexDataItemList != null && indexDataItemList.Count > 0)
            {
                foreach (IndexDataItem resultItem in indexDataItemList)
                {
                    extendedId = GetFullDataId(null,
                        resultItem,
                        fullDataIdInfo != null && fullDataIdInfo.RelatedTypeName != null ? fullDataIdInfo.FullDataIdFieldList : fullDataIdFieldList);
                    dataStoreMessages.Add(new RelayMessage(relatedTypeId,
                                              IndexCacheUtils.GeneratePrimaryId(extendedId),
                                              extendedId,
                                              MessageType.Get));
                }

                storeContext.ForwarderComponent.HandleMessages(dataStoreMessages);

                int i = 0;
                foreach (IndexDataItem resultItem in indexDataItemList)
                {
                    if (dataStoreMessages[i].Payload != null)
                    {
                        resultItem.Data = dataStoreMessages[i].Payload.ByteArray;
                    }
                    i++;
                }
            }
        }

        /// <summary>
        /// Determines whether messages are needed to be forwarded to data tier.
        /// </summary>
        /// <param name="relayTTL">The relay TTL.</param>
        /// <param name="sourceZone">The source zone.</param>
        /// <param name="serverZone">The server zone.</param>
        /// <param name="serverMode">The server mode.</param>
        /// <returns></returns>
        internal static bool ShouldForwardToDataTier(short relayTTL, ushort sourceZone, ushort serverZone, IndexServerMode serverMode)
        {
            return (relayTTL > 0 && sourceZone == serverZone && serverMode == IndexServerMode.Databound);
        }

        /// <summary>
        /// Gets the full data ids.
        /// </summary>
        /// <param name="indexId">The index id.</param>
        /// <param name="itemList">The item list.</param>
        /// <param name="fullDataIdFieldList">The full data id field list.</param>
        /// <returns></returns>
        internal static List<byte[]> GetFullDataIds(byte[] indexId, InternalItemList itemList, FullDataIdFieldList fullDataIdFieldList)
        {
            var fullDataIdList = new List<byte[]>(itemList.Count);
            foreach (IItem item in itemList)
            {
                fullDataIdList.Add(GetFullDataId(indexId, item, fullDataIdFieldList));
            }
            return fullDataIdList;
        }

        /// <summary>
        /// Gets the full data id.
        /// </summary>
        /// <param name="indexId">The index id.</param>
        /// <param name="item">The item.</param>
        /// <param name="fullDataIdFieldList">The full data id field list.</param>
        /// <returns>Byte Array representing FullDataId</returns>
        internal static byte[] GetFullDataId(byte[] indexId, IItem item, FullDataIdFieldList fullDataIdFieldList)
        {
            try
            {
                return GetFullDataIdList(indexId, item, fullDataIdFieldList);
            }
            catch
            {
                LoggingUtil.Log.Info("Error generating FullDataId");
                return null;
            }
        }

        private static byte[] GetFullDataIdList(byte[] indexId, IItem item, FullDataIdFieldList fullDataIdFieldList)
        {
            var retValList = new List<byte[]>();

            switch (fullDataIdFieldList[0].FullDataIdPartFormat)
            {
                case FullDataIdPartFormat.Sequential:
                    for (int i = 0; i < fullDataIdFieldList.Count; i++)
                    {
                        retValList.Add(GetFullDataIdField(indexId, item, fullDataIdFieldList[i]));
                    }
                    break;

                case FullDataIdPartFormat.MinMax:
                    retValList.Add(GetMinMaxField(indexId, item, fullDataIdFieldList));
                    break;
            }

            // Compute length of the byte array to return
            int len = 0;
            for (int i = 0; i < retValList.Count; i++)
            {
                len += retValList[i].Length;
            }

            //Copy full data id parts to return value
            var retVal = new byte[len];
            int pos = 0;
            for (int i = 0; i < retValList.Count; i++)
            {
                Array.Copy(retValList[i], 0, retVal, pos, retValList[i].Length);
                pos += retValList[i].Length;
            }

            return retVal;
        }

        private static byte[] GetFullDataIdField(byte[] indexId, IItem item, FullDataIdField fullDataIdField)
        {
            byte[] retVal = null;

            // If FullDataIdFieldList present on the FullDataIdField process that
            if (fullDataIdField.FullDataIdFieldList != null && fullDataIdField.FullDataIdFieldList.Count > 0)
            {
                retVal = GetFullDataIdList(indexId, item, fullDataIdField.FullDataIdFieldList);
            }
            else // Process rest of the fullDataIdField
            {
                switch (fullDataIdField.FullDataIdType)
                {
                    case FullDataIdType.IndexId:
                        retVal = indexId;
                        break;

                    case FullDataIdType.ItemId:
                        retVal = item.ItemId;
                        break;

                    case FullDataIdType.Tag:
                        if (!item.TryGetTagValue(fullDataIdField.TagName, out retVal))
                            throw new Exception("Tag missing required to generate FullDataId");
                        break;
                }
            }
            return retVal;
        }

        private static byte[] GetMinMaxField(byte[] indexId, IItem item, FullDataIdFieldList fullDataIdFieldList)
        {
            // Note: Always place min value first followed by the max value in the return value

            int offset1 = fullDataIdFieldList[0].Offset;
            int offset2 = fullDataIdFieldList[1].Offset;

            byte[] fullDataIdField1 = GetFullDataIdField(indexId, item, fullDataIdFieldList[0]);
            byte[] fullDataIdField2 = GetFullDataIdField(indexId, item, fullDataIdFieldList[1]);

            byte[] retVal = new byte[fullDataIdFieldList[0].Count + fullDataIdFieldList[1].Count];

            if (ByteArrayComparerUtil.CompareByteArrayBasedOnDataType(fullDataIdField1, fullDataIdField2, ref offset1,
                                                                  ref offset2, fullDataIdFieldList[0].Count,
                                                                  fullDataIdFieldList[1].Count,
                                                                  fullDataIdFieldList[0].DataType) < 0)
            {
                // FullDataIdField1 < FullDataIdField2 so FullDataIdField1 followed by FullDataIdField2
                Array.Copy(fullDataIdField1, fullDataIdFieldList[0].Offset, retVal, 0, fullDataIdFieldList[0].Count);
                Array.Copy(fullDataIdField2, fullDataIdFieldList[1].Offset, retVal, fullDataIdFieldList[0].Count, fullDataIdFieldList[1].Count);
            }
            else
            {
                // FullDataIdField1 >= FullDataIdField2 so FullDataIdField2 followed by FullDataIdField1
                Array.Copy(fullDataIdField2, fullDataIdFieldList[1].Offset, retVal, 0, fullDataIdFieldList[1].Count);
                Array.Copy(fullDataIdField1, fullDataIdFieldList[0].Offset, retVal, fullDataIdFieldList[1].Count, fullDataIdFieldList[0].Count);
            }

            return retVal;
        }
    }
}