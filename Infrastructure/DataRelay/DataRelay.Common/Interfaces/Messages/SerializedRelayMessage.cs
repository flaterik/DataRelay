using System;
using System.IO;
using MySpace.Common;
using MySpace.Common.IO;
using System.Collections.Generic;
using System.Text;
using System.Net;
using MySpace.DataRelay.Formatters;
using MySpace.DataRelay.Common.Interfaces.Query;
using System.Diagnostics;

namespace MySpace.DataRelay
{
	public class SerializedRelayMessage : IVersionSerializable
	{
		public MessageType MessageType;
		public MemoryStream MessageStream;
		public int PayloadLength;

		/// <summary>
		/// Used to capture a performance metric of when the message arrived on the 
		/// current machine.  Do not serialize.
		/// </summary>
		[NonSerialized]
		public long EnteredCurrentSystemAt = 0;

		public SerializedRelayMessage() { }

		/// <summary>
		/// Gets a value indicating if this <see cref="RelayMessage"/> requires a response from
		/// the server.  If <see langword="true"/>, indicates this is an "OUT" message; otherwise,
		/// this is an "IN" message.
		/// </summary>
		public bool IsTwoWayMessage
		{
			get
			{
				return RelayMessage.GetIsTwoWayMessage(MessageType);
			}
		}

		public SerializedRelayMessage(RelayMessage message)
		{
			MessageStream = new MemoryStream();
			RelayMessageFormatter.WriteRelayMessage(message, MessageStream);
			MessageStream.Seek(0, SeekOrigin.Begin);
			MessageType = message.MessageType;
			EnteredCurrentSystemAt = message.EnteredCurrentSystemAt;

			if (message.Payload != null && message.Payload.ByteArray != null)
			{
				PayloadLength = message.Payload.ByteArray.Length;
			}
		}

		public SerializedRelayMessage(MessageType messageType, MemoryStream messageStream)
		{
			EnteredCurrentSystemAt = Stopwatch.GetTimestamp();
			MessageType = messageType;
			MessageStream = messageStream;
		}

		public SerializedRelayMessage(MessageType messageType, MemoryStream messageStream, int payloadLength)
		{
			EnteredCurrentSystemAt = Stopwatch.GetTimestamp();
			MessageType = messageType;
			MessageStream = messageStream;
			PayloadLength = payloadLength;
		}

		public override string ToString()
		{
			StringBuilder desc = new StringBuilder();
			desc.Append("SerializedRelayMessage ");
			desc.Append(MessageType.ToString());

			if (PayloadLength > 0)
			{
				desc.Append(" Payload ");
				desc.Append(PayloadLength);
				desc.Append(" Bytes.");
			}
			return desc.ToString();
		}

		#region IVersionSerializable Members

		public void Serialize(IPrimitiveWriter writer)
		{
			writer.Write((int)MessageType);
			writer.Write(PayloadLength);
			if (MessageStream != null)
			{
				byte[] streamBytes = MessageStream.ToArray();
				writer.Write(streamBytes.Length);
				writer.Write(streamBytes);
			}
			else
			{
				writer.Write(-1);
			}
		}

		public void Deserialize(IPrimitiveReader reader, int version)
		{
			if (version == CurrentVersion)
			{
				MessageType = (MessageType)reader.ReadInt32();
				PayloadLength = reader.ReadInt32();
				int bytesLength = reader.ReadInt32();
				if (bytesLength > -1)
				{
					byte[] bytes = reader.ReadBytes(bytesLength);
					MessageStream = new MemoryStream(bytes);
				}
			}
		}

		public int CurrentVersion
		{
			get { return 1; }
		}

		public bool Volatile
		{
			get { return false; }
		}

		#endregion

		#region ICustomSerializable Members


		public void Deserialize(IPrimitiveReader reader)
		{
			Deserialize(reader, CurrentVersion);
		}

		#endregion
	}

}
