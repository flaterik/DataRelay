using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// Information for servers that were down.
	/// </summary>
	[XmlRoot("ServerDownErrorInfo")]
	public class ServerDownErrorInfo
	{
		/// <summary>
		/// Number of server downtime errors.
		/// </summary>
		[XmlElement("Errors")]
		public int Errors { set; get; }

		/// <summary>
		/// Number of server downtime errors in the last 30 seconds.
		/// </summary>
		[XmlElement("ErrorsLast30Seconds")]
		public int ErrorsLast30Seconds { set; get; }

		/// <summary>
		/// The last time the server was down.
		/// </summary>
		[XmlElement("LastTime")]
		public DateTime LastTime { set; get; }

		/// <summary>
		/// Description of the last time the server was down.
		/// </summary>
		/// <example>
		/// (20 minutes ago)
		/// </example>
		[XmlElement("LastTimeDescription")]
		public String LastTimeDescription { set; get; }
	}
}
