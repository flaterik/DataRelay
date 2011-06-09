using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using System.Collections.Generic;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Config;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.DomainSpecificConfigs;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context
{
    internal class InDeserializationContext
    {
        internal InDeserializationContext(int maxItemsPerIndex,
            string indexName,
            byte[] indexId,
            short typeId,
            Filter filter,
            bool inclusiveFilter,
            TagHashCollection tagHashCollection,
            bool deserializeHeaderOnly,
            bool collectFilteredItems,
            PrimarySortInfo primarySortInfo,
            List<string> localIdentityTagNames,
            StringHashCollection stringHashCollection,
            Dictionary<int, bool> stringHashCodeDictionary,
            IndexCondition indexCondition,
            CapCondition capCondition,
            bool isMetadataPropertyCollection,
            MetadataPropertyCollection metadataPropertyCollection,
            DomainSpecificProcessingType domainSpecificProcessingType,
            DomainSpecificConfig domainSpecificConfig,
            string getDistinctValuesFieldName,
            GroupBy groupBy)
        {
            MaxItemsPerIndex = maxItemsPerIndex;
            IndexName = indexName;
            IndexId = indexId;
            TypeId = typeId;
            Filter = filter;
            InclusiveFilter = inclusiveFilter;
            TagHashCollection = tagHashCollection;
            DeserializeHeaderOnly = deserializeHeaderOnly;
            CollectFilteredItems = collectFilteredItems;
            PrimarySortInfo = primarySortInfo;
            LocalIdentityTagNames = localIdentityTagNames;
            StringHashCollection = stringHashCollection;
            StringHashCodeDictionary = stringHashCodeDictionary;
            IndexCondition = indexCondition;
            CapCondition = capCondition;
            IsMetadataPropertyCollection = isMetadataPropertyCollection;
            MetadataPropertyCollection = metadataPropertyCollection;
            DomainSpecificProcessingType = domainSpecificProcessingType;
            DomainSpecificConfig = domainSpecificConfig;
            GetDistinctValuesFieldName = getDistinctValuesFieldName;
            GroupBy = groupBy;

            SetEnterExitCondition();
        }

        #region Data members
        
        /// <summary>
        /// If MaxItemsPerIndex equals zero it indicates extract all items.
        /// If MaxItemsPerIndex > 0 indicates max number of items to deserialize
        /// </summary>
        internal int MaxItemsPerIndex
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the name of the index.
        /// </summary>
        /// <value>The name of the index.</value>
        internal string IndexName
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the index id.
        /// </summary>
        /// <value>The index id.</value>
        internal byte[] IndexId
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the type id.
        /// </summary>
        /// <value>The type id.</value>
        internal short TypeId
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the filter.
        /// </summary>
        /// <value>The filter.</value>
        internal Filter Filter
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether items that pass filter are included in CacheIndexInternal.
        /// </summary>
        /// <value><c>true</c> if items that pass filter are to be included in CacheIndexInternal; otherwise, <c>false</c>.</value>
        internal bool InclusiveFilter
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the tag hash collection.
        /// </summary>
        /// <value>The tag hash collection.</value>
        internal TagHashCollection TagHashCollection
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to just deserialize CacheIndexInternal header or not.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if just CacheIndexInternal header is to be deserialized; otherwise, <c>false</c>.
        /// </value>
        internal bool DeserializeHeaderOnly
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to collect filtered items or not.
        /// </summary>
        /// <value>
        /// 	<c>true</c> to collect filtered items; otherwise, <c>false</c>.
        /// </value>
        internal bool CollectFilteredItems
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the primary sort info.
        /// </summary>
        /// <value>The primary sort info.</value>
        internal PrimarySortInfo PrimarySortInfo
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the local identity tag names.
        /// </summary>
        /// <value>The local identity tag names.</value>
        internal List<string> LocalIdentityTagNames
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the string hash collection.
        /// </summary>
        /// <value>The string hash collection.</value>
        internal StringHashCollection StringHashCollection
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the string hash code dictionary.
        /// </summary>
        /// <value>The string hash code dictionary.</value>
        internal Dictionary<int, bool> StringHashCodeDictionary
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the index condition.
        /// </summary>
        /// <value>The index condition.</value>
        internal IndexCondition IndexCondition
        {
            get; set;
        }

        private Condition enterCondition;
        /// <summary>
        /// Gets the enter condition.
        /// </summary>
        /// <value>The enter condition.</value>
        internal Condition EnterCondition
        {
            get
            {
                return enterCondition;
            }
        }

        private Condition exitCondition;
        /// <summary>
        /// Gets the exit condition.
        /// </summary>
        /// <value>The exit condition.</value>
        internal Condition ExitCondition
        {
            get
            {
                return exitCondition;
            }
        }

        /// <summary>
        /// Gets or sets the cap condition.
        /// </summary>
        /// <value>The cap condition.</value>
        internal CapCondition CapCondition
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether metadata represents an index property collection.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if metadata represents an index property collection; otherwise, <c>false</c>.
        /// </value>
        internal bool IsMetadataPropertyCollection
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the metadata property collection.
        /// </summary>
        /// <value>The metadata property collection.</value>
        internal MetadataPropertyCollection MetadataPropertyCollection
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the type of the domain specific processing.
        /// </summary>
        /// <value>The type of the domain specific processing.</value>
        internal DomainSpecificProcessingType DomainSpecificProcessingType
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the domain specific config.
        /// </summary>
        /// <value>The domain specific config.</value>
        internal DomainSpecificConfig DomainSpecificConfig
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the name of the distinct values field.
        /// </summary>
        /// <value>The name of the distinct values field.</value>
        internal string GetDistinctValuesFieldName 
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the GroupBy clause.
        /// </summary>
        /// <value>The GroupBy clause.</value>
        internal GroupBy GroupBy
        {
            get; set;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Sets the enter exit condition.
        /// </summary>
        private void SetEnterExitCondition()
        {
            if (IndexCondition != null)
            {
                IndexCondition.CreateConditions(PrimarySortInfo.FieldName, 
                    PrimarySortInfo.IsTag,
                    PrimarySortInfo.SortOrderList[0],
                    out enterCondition,
                    out exitCondition);

                if (enterCondition != null)
                {
                    IndexCacheUtils.ProcessMetadataPropertyCondition(enterCondition, MetadataPropertyCollection);
                }

                if (exitCondition != null)
                {
                    IndexCacheUtils.ProcessMetadataPropertyCondition(exitCondition, MetadataPropertyCollection);
                }
            }
        }

        #endregion
    }
}