using System.Xml.Serialization;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// Information about messages sent as lists against the relay server.
	/// </summary>
	[XmlRoot("BulkMessageInfo")]
	public class BulkMessageInfo
	{
		/// <summary>
		/// Number of bulk messages.
		/// </summary>
		[XmlElement("MessageCount")]
		public int MessageCount { set; get; }

		/// <summary>
		/// Average message time.
		/// </summary>
		[XmlElement("AverageMessageTime")]
		public double AverageMessageTime { set; get; }

		/// <summary>
		/// Time of the last message.
		/// </summary>
		[XmlElement("LastMessageTime")]
		public double LastMessageTime { set; get; }

		/// <summary>
		/// Number of items in the last bulk message.
		/// </summary>
		[XmlElement("LastMessageLength")]
		public double LastMessageLength { set; get; }

		/// <summary>
		/// Average number of items in bulk messages.
		/// </summary>
		[XmlElement("AverageMessageLength")]
		public double AverageMessageLength { set; get; }
	}
}
