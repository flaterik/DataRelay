using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySpace.DataRelay;
using MySpace.DataRelay.Common.Schemas;
using MySpace.DataRelay.RelayComponent.Forwarding;
using MySpace.DataRelay.Transports;

namespace MySpace.RelayComponent.Forwarding.Test
{
	/// <summary>
	/// Distributes relay message among a collection of <see cref="IRelayTransport"/>s.
	/// </summary>
	public class RelayTransportStack : IRelayTransport, IRelayTransportExtended
	{
		private readonly IRelayTransport[] _transports;
		private readonly int _count;
		private readonly IRelayTransportExtended[] _extendedTransports;

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="RelayTransportStack"/> class.</para>
		/// </summary>
		/// <param name="transports">
		/// 	<para>The collection of <see cref="IRelayTransport"/>s.</para>
		/// </param>
		public RelayTransportStack(params IRelayTransport[] transports)
		{
			if (transports == null) throw new ArgumentNullException("transports");
			_transports = transports;
			_count = transports.Length;
			_extendedTransports = new IRelayTransportExtended[_count];
			for (var idx = 0; idx < _count; ++idx)
			{
				_extendedTransports[idx] = transports[idx] as IRelayTransportExtended;
			}
		}

		private void DoAllTransports(Action<IRelayTransport> act)
		{
			for(var idx = 0; idx < _count; ++idx)
			{
				var transport = _transports[idx];
				if (transport == null) continue;
				act(transport);
			}
		}

		private void DoAllExtendedTransports(Action<IRelayTransportExtended> act)
		{
			for (var idx = 0; idx < _count; ++idx)
			{
				var extendedTransport = _extendedTransports[idx];
				if (extendedTransport == null) continue;
				act(extendedTransport);
			}
		}

		#region IRelayTransport Members

		void IRelayTransport.SendMessage(RelayMessage message)
		{
			DoAllTransports(t => t.SendMessage(message));
		}

		void IRelayTransport.SendMessage(SerializedRelayMessage message)
		{
			DoAllTransports(t => t.SendMessage(message));
		}

		void IRelayTransport.SendInMessageList(SerializedRelayMessage[] messages)
		{
			DoAllTransports(t => t.SendInMessageList(messages));
		}

		void IRelayTransport.SendInMessageList(List<SerializedRelayMessage> messages)
		{
			DoAllTransports(t => t.SendInMessageList(messages));
		}

		void IRelayTransport.SendOutMessageList(List<RelayMessage> messages)
		{
			DoAllTransports(t => t.SendOutMessageList(messages));
		}

		void IRelayTransport.GetConnectionStats(out int openConnections, out int activeConnections)
		{
			var osum = 0;
			var asum = 0;
			DoAllTransports(t =>
        	{
        		int o, a;
        		t.GetConnectionStats(out o, out a);
				osum += o;
				asum += a;
        	});
			openConnections = osum;
			activeConnections = asum;
		}

		#endregion

		#region IRelayTransportExtended Members

		void IRelayTransportExtended.SendSyncMessage(RelayMessage message)
		{
			DoAllExtendedTransports(t => t.SendSyncMessage(message));
		}

		void IRelayTransportExtended.SendSyncMessageList(List<RelayMessage> messages)
		{
			DoAllExtendedTransports(t => t.SendSyncMessageList(messages));
		}

		#endregion
	}
}
