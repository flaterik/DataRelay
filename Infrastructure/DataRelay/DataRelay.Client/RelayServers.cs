using System.Collections.Generic;
using System.Threading;
using MySpace.DataRelay.Configuration;

namespace MySpace.DataRelay.Client
{
    /// <summary>
    /// A Relay Forwarder based on the Future pattern.
    /// </summary>
    internal class RelayServers
    {
        private Dictionary<string, ModuloHashMap<RelayEndPoint>> _hashMaps = new Dictionary<string, ModuloHashMap<RelayEndPoint>>();

        /// <summary>
        /// Called when a configuration object needs to be applied.
        /// </summary>
        /// <param name="updatedConfig">The updated config.</param>
        public void UpdateConfig(RelayNodeConfig updatedConfig)
        {
            var newMaps = new Dictionary<string, ModuloHashMap<RelayEndPoint>>();
            var relayNodeMapping = updatedConfig.RelayNodeMapping;
            if (relayNodeMapping != null)
            {
                if (relayNodeMapping.Validate())
                {
                    var groups = relayNodeMapping.RelayNodeGroups;
                    foreach (var group in groups)
                    {
                        newMaps.Add(group.Name, new ModuloHashMap<RelayEndPoint>());
                        for(int clusterIndex = 0; clusterIndex < group.RelayNodeClusters.Length; ++clusterIndex)
                        {
                            foreach (var node in group.RelayNodeClusters[clusterIndex].RelayNodes)
                            {
                                newMaps[group.Name].Add(clusterIndex, new RelayEndPoint(node, group));
                            }
                        }
                        newMaps[group.Name].Modulo = group.RelayNodeClusters.Length;
                        newMaps[group.Name].Shuffle();
                    }
                }
            }

            Thread.MemoryBarrier();
            _hashMaps = newMaps;
        }

        private static readonly List<RelayEndPoint> _emptyList = new List<RelayEndPoint>(0);

        /// <summary>
        /// Gets a list of EndPoints appropriate from a data relay group for the primary ID specified.
        /// </summary>
        /// <param name="groupName">Name of the data relay group.</param>
        /// <param name="primaryId">The primary id.</param>
        /// <returns>A list of pipeline client futures.</returns>
        public IEnumerable<RelayEndPoint> GetEndPoints(string groupName, int primaryId)
        {
            ModuloHashMap<RelayEndPoint> hashMap;
            if(_hashMaps.TryGetValue(groupName, out hashMap))
            {
                return hashMap.Select(primaryId);
            }
            return _emptyList;
        }
    }
}