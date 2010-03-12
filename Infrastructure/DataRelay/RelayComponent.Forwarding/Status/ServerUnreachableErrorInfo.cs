using System;
using System.Xml.Serialization;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// Information for servers that were unreachable.
	/// </summary>
	[XmlRoot("ServerUnreachableErrorInfo")]
	public class ServerUnreachableErrorInfo
	{
		/// <summary>
		/// Number of server unreachable errors in the last 30 seconds.
		/// </summary>
		[XmlElement("Errors")]
		public int Errors { set; get; }
		/// <summary>
		/// Period of time to wait for reporting number of server unreachable errors.
		/// </summary>
		[XmlElement("WaitPeriodSeconds")]
		public int WaitPeriodSeconds { set; get; }
		/// <summary>
		/// Number of server unreachable errors in the last two <see cref="WaitPeriodSeconds"/>.
		/// </summary>
		[XmlElement("ErrorsLast2WaitPeriods")]
		public int ErrorsLast2WaitPeriods { set; get; }

		/// <summary>
		/// The last time the server was unreachable.
		/// </summary>
		[XmlElement("LastTime")]
		public DateTime LastTime { set; get; }

		/// <summary>
		/// Description of the last time the server was unreachable.
		/// </summary>
		/// <example>
		/// (20 minutes ago)
		/// </example>
		[XmlElement("LastTimeDescription")]
		public String LastTimeDescription { set; get; }
	}
}
