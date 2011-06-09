using System;
using System.Collections.Generic;
using MySpace.Common;
using MySpace.DataRelay.Common.Schemas;
using MySpace.DataRelay.Configuration;
using MySpace.Logging;

namespace MySpace.DataRelay.Client
{
    /// <summary>
    /// Sends a RelayMessage to relay nodes using Pipeline Transport or Socket Transport.
    /// </summary>
    public class RelayMessageClient
    {
        private readonly LogWrapper _log = new LogWrapper();
        private readonly RelayServers _servers = new RelayServers();
        private readonly RelayTypeSettings _typeSettings = new RelayTypeSettings();

        internal RelayMessageClient()
        {
            UpdateConfig(RelayNodeConfig.GetRelayNodeConfig(ReloadConfigHandler));
        }

        internal RelayMessageClient(RelayNodeConfig nodeConfig)
        {
            UpdateConfig(nodeConfig);
        }

        /// <summary>
        /// Sends the relay message.
        /// </summary>
        /// <param name="typeSetting">The type setting.</param>
        /// <param name="relayMessage">The relay message.</param>
        /// <returns>A future with the possibly resuling RelayMessage.  Result will be null if it is a miss.</returns>
        public Future<RelayMessage> SendRelayMessage(TypeSetting typeSetting, RelayMessage relayMessage)
        {
            var endPoints = _servers.GetEndPoints(typeSetting.GroupName, relayMessage.Id);

            var endPointEnumerator = endPoints.GetEnumerator();
            if (!endPointEnumerator.MoveNext()) return NoMatchingServer(typeSetting, relayMessage);
            endPointEnumerator.Reset();

            var publisher = new FuturePublisher<RelayMessage>();
            TryNextServer(publisher, relayMessage, null, endPointEnumerator);
            return publisher.Future;
        }

        private static void TryNextServer(FuturePublisher<RelayMessage> publisher, RelayMessage relayMessage, Exception lastException, IEnumerator<RelayEndPoint> servers)
        {
            if (servers.MoveNext())
            {
                if (!publisher.Future.IsCanceled)
                {
                    var serverResponse = servers.Current.RelayMessageSender.Send(relayMessage);
                    serverResponse.OnSuccess(() => publisher.SetResult(serverResponse.Result));
                    serverResponse.OnError(ex => TryNextServer(publisher, relayMessage, ex, servers));
                }
            }
            else
            {
                publisher.SetError(new ApplicationException("Send Message failed for all available matching servers.", lastException));
            }
        }

        private Future<RelayMessage> NoMatchingServer(TypeSetting typeSetting, RelayMessage relayMessage)
        {
            var noMatchingServerMessage = String.Format("No Data Relay server is in the server mapping for group/id {0}/{1}", typeSetting.GroupName, relayMessage.Id);
            _log.ErrorFormat(noMatchingServerMessage);

            var serverNotFoundFuture = new FuturePublisher<RelayMessage>();

            serverNotFoundFuture.SetError(new ApplicationException(noMatchingServerMessage));  // TODO: use a better exception type
            return serverNotFoundFuture.Future;
        }

        private void ReloadConfigHandler(object state, EventArgs args)
        {
            UpdateConfig(state as RelayNodeConfig);
        }

        private void UpdateConfig(RelayNodeConfig config)
        {
            if (config != null)
            {
                _log.Info("Loading RelayNodeConfig.");

                _servers.UpdateConfig(config);
                _typeSettings.UpdateConfig(config);
            }
            else
            {
                _log.Error("Attempt to reload a RelayNode config with null config");
            }
        }

        protected TypeSetting GetTypeSetting<T>()
        {
            return _typeSettings.GetSetting<T>();
        }
    }
}