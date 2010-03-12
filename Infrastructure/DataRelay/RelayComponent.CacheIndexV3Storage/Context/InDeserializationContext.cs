using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using System.Collections.Generic;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Config;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context
{
    internal class InDeserializationContext
    {
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

        /// <summary>
        /// Flag to indicate whether Enter and Exit conditions are set or not
        /// </summary>
        private bool isEnterExitConditionSet;

        private Condition enterCondition;
        /// <summary>
        /// Gets the enter condition.
        /// </summary>
        /// <value>The enter condition.</value>
        internal Condition EnterCondition
        {
            get
            {
                if (!isEnterExitConditionSet)
                {
                    SetEnterExitCondition();
                }
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
                if (!isEnterExitConditionSet)
                {
                    SetEnterExitCondition();
                }
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

        #endregion

        #region Methods

        /// <summary>
        /// Sets the enter exit condition.
        /// </summary>
        private void SetEnterExitCondition()
        {
            isEnterExitConditionSet = true;
            if (IndexCondition != null)
            {
                IndexCondition.CreateConditions(PrimarySortInfo.FieldName, 
                    PrimarySortInfo.IsTag,
                    PrimarySortInfo.SortOrderList[0],
                    out enterCondition,
                    out exitCondition);
            }
        }

        #endregion
    }
}