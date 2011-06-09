using System;
using MySpace.DataRelay.Common.Schemas;
using MySpace.DataRelay.Configuration;

using MySpace.Common;

namespace MySpace.DataRelay.Client
{
    internal class RelayTypeSettings
    {
        private RelayNodeConfig _configuration;

        /// <summary>
        /// Updates the configuration used for type settings.
        /// </summary>
        /// <param name="updatedConfig">The updated config.</param>
        public void UpdateConfig(RelayNodeConfig updatedConfig)
        {
            if (updatedConfig!=null && updatedConfig.TypeSettings != null)
            {
                RelayMessage.SetCompressionImplementation(updatedConfig.TypeSettings.Compressor);
                _configuration = updatedConfig;
            }
        }

        /// <summary>
        /// Gets the type setting for a given type name.
        /// </summary>
        /// <returns>The <see cref="TypeSetting"/> for the type T; null if not found.</returns>
        public TypeSetting GetSetting<T>()
        {
            var typeName = typeof (T).FullName;
            var config = _configuration;
            if (config == null || config.TypeSettings == null || typeName == null || !config.TypeSettings.TypeSettingCollection.Contains(typeName))
            {
                return null;
            }

            return _configuration.TypeSettings.TypeSettingCollection[typeName];
        }

        /// <summary>
        /// A convenience method to create an error Future for when a type is not present in the Data Relay configuration.
        /// </summary>
        /// <typeparam name="T">The type to create an error future for.</typeparam>
        /// <returns>The error future.</returns>
        public static Future<T> InvalidTypeFuture<T>()
        {
            var futureObject = new FuturePublisher<T>();
            futureObject.SetError(new InvalidOperationException(String.Format("The type \"{0}\" is not configured for use with Data Relay.", typeof(T).FullName)));
            return futureObject.Future;
        }
    }
}