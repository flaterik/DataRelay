using System.Threading;
using System.Xml.Serialization;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// Information about messages being processed by the data relay server.
	/// </summary>
	[XmlRoot("TypeSpecificMessageCountInfo")]
	public class TypeSpecificMessageCountInfo
	{
		/// <summary>
		/// Gets or sets the number of messages.
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
		/// Gets or sets the average message time.
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
		/// Gets or sets the time of the last message.
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
		/// Creates a clone of this <see cref="TypeSpecificMessageCountInfo"/>.
		/// </summary>
		/// <returns>
		/// <para>A cloned <see cref="TypeSpecificMessageCountInfo"/> object that shares no object
		///		references as this instance; never <see langword="null"/>.
		/// </para>
		/// </returns>
		internal TypeSpecificMessageCountInfo Clone()
		{
			TypeSpecificMessageCountInfo messageInfo = new TypeSpecificMessageCountInfo();
			messageInfo._messageCount = _messageCount;
			messageInfo._averageMessageTime = _averageMessageTime;
			messageInfo._lastMessageTime = _lastMessageTime;

			return messageInfo;
		}
		/// <summary>
		/// Returns a copy of the <see cref="TypeSpecificMessageCountInfo"/> or null if no statistics were calculated.
		/// </summary>
		/// <returns>A copy of the <see cref="TypeSpecificMessageCountInfo"/> or null if no statistics were calculated.</returns>
		internal TypeSpecificMessageCountInfo GetStatus()
		{
			TypeSpecificMessageCountInfo info = null;
			if (this.MessageCount > 0)
			{
				info = this.Clone();
			}
			return info;
		}
		/// <summary>
		/// Calculates statistics for messages of a specified type.
		/// </summary>
		/// <param name="milliseconds">The time it takes to send the message.</param>
		internal void CaculateStatisics(long milliseconds)
		{
			Interlocked.Exchange(ref _lastMessageTime, milliseconds);
			Interlocked.Exchange(ref _averageMessageTime, 
				CalculateAverage(_averageMessageTime, milliseconds, Interlocked.Increment(ref _messageCount)));
		}
		private static double CalculateAverage(double baseLine, double newSample, double iterations)
		{
			return ((baseLine * (iterations - 1)) + newSample) / iterations;
		}
	}
}