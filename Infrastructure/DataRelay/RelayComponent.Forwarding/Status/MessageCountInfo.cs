using System.Xml.Serialization;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// Information about messages being processed by the data relay server.
	/// </summary>
	[XmlRoot("MessageCountInfo")]
	public class MessageCountInfo
	{
		/// <summary>
		/// Type of message data is being collected for.
		/// </summary>
		[XmlElement("MessageType")]
		public string MessageType;

		/// <summary>
		/// Number of messages.
		/// </summary>
		[XmlElement("MessageCount")]
		public int MessageCount;

		/// <summary>
		/// Average message time.
		/// </summary>
		[XmlElement("AverageMessageTime")]
		public double AverageMessageTime;

		/// <summary>
		/// Time of the last message.
		/// </summary>
		[XmlElement("LastMessageTime")]
		public double LastMessageTime;
	}
}
