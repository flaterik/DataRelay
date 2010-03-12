using System.Collections.Generic;
using MySpace.DataRelay.Common.Schemas;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils
{
    internal class LegacySerializationUtil
    {
        private const string MOOD_STATUS_2_TYPE_NAME = "MySpace.Friends.Domain.AccountStatusMood.Index";
        private static List<short> legacySerializationTypes;

        /// <summary>
        /// Initializes a new instance of the <see cref="LegacySerializationUtil"/> class.
        /// </summary>
        private LegacySerializationUtil(){}

        private static readonly LegacySerializationUtil instance = new LegacySerializationUtil();

        /// <summary>
        /// Gets the instance.
        /// </summary>
        /// <value>The instance.</value>
        internal static LegacySerializationUtil Instance
        {
            get
            {
                return instance;
            }
        }

        /// <summary>
        /// Initializes the legacy serializtion types.
        /// </summary>
        /// <param name="typeSettings">The type settings.</param>
        /// <param name="isLegacySerializationSupported">if set to <c>true</c> indicates that legacy serialization supported.</param>
        internal void InitializeLegacySerializtionTypes(TypeSettings typeSettings, bool isLegacySerializationSupported)
        {
            legacySerializationTypes = new List<short>();

            if (isLegacySerializationSupported)
            {
                try
                {
                    legacySerializationTypes.Add(typeSettings.TypeSettingCollection[MOOD_STATUS_2_TYPE_NAME].TypeId);
                }
                catch
                {
                    // no need to do anything if MOOD_STATUS_2_TYPE_NAME is not found in TypeSettingCollection                
                }                
            }
        }

        /// <summary>
        /// Determines whether the specified type id is supported.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        /// <returns>
        /// 	<c>true</c> if the specified type id is supported; otherwise, <c>false</c>.
        /// </returns>
        internal bool IsSupported(short typeId)
        {
            return legacySerializationTypes.Contains(typeId);
        }
    }
}