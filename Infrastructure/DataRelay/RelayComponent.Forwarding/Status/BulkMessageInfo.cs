using System.Threading;
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
		/// Gets or sets number of bulk messages.
		/// </summary>
		public int MessageCount
		{
			set
			{
				_messageCount = value;
			}
			get
			{
				return _messageCount;
			}
		}
		[XmlElement("MessageCount")]
		private int _messageCount;

		/// <summary>
		/// Gets or sets average message time.
		/// </summary>
		public double AverageMessageTime
		{
			set
			{
				_averageMessageTime = value;
			}
			get
			{
				return _averageMessageTime;
			}
		}
		[XmlElement("AverageMessageTime")]
		private double _averageMessageTime;

		/// <summary>
		/// Gets or sets time of the last message.
		/// </summary>
		public double LastMessageTime
		{
			set
			{
				_lastMessageTime = value;
			}
			get
			{
				return _lastMessageTime;
			}
		}
		[XmlElement("LastMessageTime")]
		private double _lastMessageTime;

		/// <summary>
		/// Gets or sets number of items in the last bulk message.
		/// </summary>
		public double LastMessageLength
		{
			set
			{
				_lastMessageLength = value;
			}
			get
			{
				return _lastMessageLength;
			}
		}
		[XmlElement("LastMessageLength")]
		private double _lastMessageLength;

		/// <summary>
		/// Gets or sets average number of items in bulk messages.
		/// </summary>
		public double AverageMessageLength
		{
			set
			{
				_averageMessageLength = value;
			}
			get
			{
				return _averageMessageLength;
			}
		}
		[XmlElement("AverageMessageLength")]
		private double _averageMessageLength;

		/// <summary>
		/// Creates a clone of this <see cref="BulkMessageInfo"/>.
		/// </summary>
		/// <returns>
		/// <para>A cloned <see cref="BulkMessageInfo"/> object that shares no object
		///		references as this instance; never <see langword="null"/>.
		/// </para>
		/// </returns>
		internal BulkMessageInfo Clone()
		{
			BulkMessageInfo messageInfo = new BulkMessageInfo();
			messageInfo._messageCount = _messageCount;
			messageInfo._lastMessageLength = _lastMessageLength;
			messageInfo._averageMessageLength = _averageMessageLength;
			messageInfo._lastMessageTime = _lastMessageTime;
			messageInfo._averageMessageTime = _averageMessageTime;

			return messageInfo;
		}
		/// <summary>
		/// Returns a copy of the <see cref="BulkMessageInfo"/> or null if no statistics were calculated.
		/// </summary>
		/// <returns>A copy of the <see cref="BulkMessageInfo"/> or null if no statistics were calculated.</returns>
		internal BulkMessageInfo GetStatus()
		{
			BulkMessageInfo messageInfo = null;
			if(this.MessageCount > 0)
			{
				messageInfo = this.Clone();
			}
			return messageInfo;
		}
		/// <summary>
		/// Calculates statistics for bulk messages.
		/// </summary>
		/// <param name="messageLength">The number of messages sent in the bulk message.</param>
		/// <param name="milliseconds">The time it takes to send the message.</param>
		internal void CaculateStatisics(int messageLength, long milliseconds)
		{
			Interlocked.Increment(ref _messageCount);
			Interlocked.Exchange(ref _lastMessageLength, messageLength);
			Interlocked.Exchange(ref _averageMessageLength, CalculateAverage(AverageMessageLength, LastMessageLength, MessageCount));
			Interlocked.Exchange(ref _lastMessageTime, milliseconds);
			Interlocked.Exchange(ref _averageMessageTime, CalculateAverage(AverageMessageTime, LastMessageTime, MessageCount));
		}
		
		private static double CalculateAverage(double baseLine, double newSample, double iterations)
		{
			return ((baseLine * (iterations - 1)) + newSample) / iterations;
		}
	}
}