using System;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.DomainSpecificConfigs;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.Common.Util;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils
{
    internal static class DomainSpecificProcssorUtil
    {
        private const string RecencyTagName = "Recency";

        internal static void Process(InternalItem internalItem,
            TagHashCollection tagHashCollection,
            short typeId,
            DomainSpecificProcessingType domainSpecificProcessingType,
            DomainSpecificConfig domainSpecificConfig)
        {
            switch (domainSpecificProcessingType)
            {
                case DomainSpecificProcessingType.StreamRecency:
                    tagHashCollection.AddTag(typeId, RecencyTagName);
                    internalItem.TagList.Add(new System.Collections.Generic.KeyValuePair<int, byte[]>(TagHashCollection.GetTagHashCode(RecencyTagName),
                        GetRecency(internalItem, domainSpecificConfig.StreamRecencyConfig)));
                    break;
            }
        }

        private static byte[] GetRecency(InternalItem internalItem, StreamRecencyConfig streamRecencyConfig)
        {
            //     Nt = N0 * e^(-decay constant * t)
            //i.e. Recency = InitialRecency * e^(-decay constant * timeElapsedSinceActivityCreation)

            byte[] activityTypeBytes;
            double halfLife;

            if (String.Compare("ItemId", streamRecencyConfig.TypeTagName, StringComparison.OrdinalIgnoreCase) == 0)
            {
                activityTypeBytes = internalItem.ItemId;
            }
            else
            {
                internalItem.TryGetTagValue(streamRecencyConfig.TypeTagName, out activityTypeBytes);
            }

            if(activityTypeBytes != null)
            {
                int activityType = activityTypeBytes.Length == sizeof(Int32) ? 
                    BitConverter.ToInt32(activityTypeBytes, 0) : 
                    activityTypeBytes[0];

                halfLife = streamRecencyConfig.TypeDecayMappingCollection.Contains(activityType) ? 
                    streamRecencyConfig.TypeDecayMappingCollection[activityType].HalfLife : 
                    streamRecencyConfig.DefaultHalfLife;
            }
            else
            {
                halfLife = streamRecencyConfig.DefaultHalfLife;
            }

            // decay constant limited to 3 decimal places
            double decayConstant = Math.Round(Math.Log(2) / halfLife, 3);

            byte[] timestampBytes;
            if (String.Compare("ItemId", streamRecencyConfig.TimeStampTagName, StringComparison.OrdinalIgnoreCase) == 0)
            {
                timestampBytes = internalItem.ItemId;
            }
            else
            {
                internalItem.TryGetTagValue(streamRecencyConfig.TimeStampTagName, out timestampBytes);
            }
            double t =  DateTime.Now.Subtract(new SmallDateTime(BitConverter.ToInt32(timestampBytes, 0)).FullDateTime).TotalDays;
            return BitConverter.GetBytes(streamRecencyConfig.InitialRecencyValue * Math.Pow(Math.E, -decayConstant * t));
        }
    }
}
