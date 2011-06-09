using System;
using System.IO;
using System.Runtime.Serialization;
using MySpace.Common;
using MySpace.Common.CompactSerialization.IO;
using MySpace.Common.IO;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;

namespace MySpace.DataRelay.Formatters
{
	/// <summary>
	/// Helper class that translates between a <see cref="Stream"/> and a <see cref="RelayMessage"/>,
	/// and vice-versa.
	/// </summary>
	public static class RelayMessageFormatter
	{
		/// <summary>
		/// Translates the given <see cref="Stream"/> into a <see cref="RelayMessage"/>.
		/// </summary>
		/// <param name="stream">The given stream.</param>
		/// <returns>The <see cref="RelayMessage"/> contained in the given stream..</returns>
		public static RelayMessage ReadRelayMessage(Stream stream)
		{
			RelayMessage op = RelayMessage.GetInstanceFromStream(stream);

			return op;
		}

		/// <summary>
		///	<para>Converts a <see cref="RelayMessage"/> into a <see cref="MemoryStream"/>.</para>
		/// </summary>
		/// <param name="message">The <see cref="RelayMessage"/>.</param>
		/// <returns>A <see cref="MemoryStream"/> version of the given <see cref="RelayMessage"/>.</returns>		
		public static MemoryStream WriteRelayMessage(RelayMessage message)
		{
			MemoryStream ms = new MemoryStream();

			WriteRelayMessage(message, ms);
			ms.Seek(0, SeekOrigin.Begin);

			return ms;
		}

		/// <summary>
		///	<para>Writes <paramref name="message"/> into <paramref name="stream"/>.
		///	The position of <paramref name="stream"/> will not be reset to the
		///	beginning following the write unlike in
		///	<see cref="WriteRelayMessage(RelayMessage)" />.</para>
		/// </summary>
		/// <param name="message">The <see cref="RelayMessage"/>.</param>
		/// <param name="stream">The stream to write <paramref name="message"/> into.</param>
		public static void WriteRelayMessage(RelayMessage message, Stream stream)
		{
			Serializer.Serialize<RelayMessage>(stream, message);
		}

		/// <summary>
		/// Converts a list of <see cref="RelayMessage"/> into a <see cref="MemoryStream"/>
		/// </summary>
		/// <param name="messageList">The list to convert.</param>
		/// <returns>The <see cref="MemoryStream"/> that represents the list.</returns>
		public static MemoryStream WriteRelayMessageList(IList<RelayMessage> messageList)
		{
			MemoryStream ms = new MemoryStream();
			BinaryWriter writeStream = new BinaryWriter(ms);
			CompactBinaryWriter writer = new CompactBinaryWriter(writeStream);
			writer.Write(messageList.Count);
			for (int i = 0; i < messageList.Count; i++)
			{
				writer.Write<RelayMessage>(messageList[i], false);
			}

			ms.Seek(0, SeekOrigin.Begin);

			return ms;
		}

		/// <summary>
		/// Converts a list of <see cref="RelayMessage"/> into a <see cref="MemoryStream"/>
		/// </summary>
		/// <param name="messageList">The list to convert.</param>
		/// <param name="stream">The stream to write <paramref name="messageList"/> into.</param>
		public static void WriteRelayMessageList(IList<RelayMessage> messageList, Stream stream)
		{
			BinaryWriter writeStream = new BinaryWriter(stream);
			CompactBinaryWriter writer = new CompactBinaryWriter(writeStream);
			writer.Write(messageList.Count);
			for (int i = 0; i < messageList.Count; i++)
			{
				writer.Write<RelayMessage>(messageList[i], false);
			}
			
			//this seek can't happen on methods passed to async socket client, because everything breaks and then you're confused and sad
			//TODO make async socket client not reliant on stream position and/or roll async functionality into regular socket client.
			//stream.Seek(0, SeekOrigin.Begin); 
			
		}

		/// <summary>
		/// Writes a list of <see cref="RelayMessage"/> to a <see cref="MemoryStream"/> constrained by 
		/// a start <paramref name="startPosition"/> and <paramref name="count"/>.
		/// </summary>
		/// <param name="messages">The list of messages to write.</param>
		/// <param name="startPosition">The index of the <paramref name="messages"/> to start writing from.</param>
		/// <param name="count">The number of items to translate.</param>
		/// <param name="ms">The <see cref="MemoryStream"/> to write to.</param>
		/// <returns>Returns the number of bytes written.</returns>
		public static int WriteRelayMessageList(IList<RelayMessage> messages, int startPosition, int count, MemoryStream ms)
		{
			BinaryWriter writeStream = new BinaryWriter(ms);
			CompactBinaryWriter writer = new CompactBinaryWriter(writeStream);
			int written = 0;
			int realCount = count;
			if ((count + startPosition) > messages.Count)
			{
				realCount = messages.Count - startPosition;
			}
			writer.Write(realCount);
			for (int i = startPosition; i < messages.Count && ((i - startPosition) < realCount); i++)
			{
				writer.Write<RelayMessage>(messages[i], false);
				written++;
			}

			ms.Seek(0, SeekOrigin.Begin);
			return written;

		}

		/// <summary>
		/// Converts the given <see cref="Stream"/> into a list of <see cref="RelayMessage"/>.
		/// </summary>
		/// <param name="stream">The given <see cref="Stream"/></param>
		/// <returns>A list of <see cref="RelayMessage"/>.</returns>
		public static List<RelayMessage> ReadRelayMessageList(Stream stream)
		{
			return ReadRelayMessageList(stream, null);
		}

		/// <summary>
		/// Converts the given <see cref="Stream"/> into a list of <see cref="RelayMessage"/>.
		/// </summary>
		/// <param name="stream">The given <see cref="Stream"/></param>
		/// <param name="evaluateMethod">A method to evaluate each <see cref="RelayMessage"/> as it's deserialized.</param>
		/// <returns>A list of <see cref="RelayMessage"/>.</returns>
		public static List<RelayMessage> ReadRelayMessageList(Stream stream, Action<RelayMessage> evaluateMethod)
		{
			BinaryReader bread = new BinaryReader(stream);
			CompactBinaryReader br = new CompactBinaryReader(bread);
			int objectCount = br.ReadInt32();
			List<RelayMessage> messages = new List<RelayMessage>(objectCount);
			for (int i = 0; i < objectCount; i++)
			{
				RelayMessage nextMessage = new RelayMessage();
				try
				{
					br.Read<RelayMessage>(nextMessage, false);
				}
				catch (SerializationException exc)
				{
					//try and add some context to this object
					//Id and TypeId most likely got correctly deserialized so we're providing that much
					string message = string.Format("Deserialization failed for RelayMessage of Id='{0}', ExtendedId='{1}', TypeId='{2}' and StreamLength='{3}'",
						nextMessage.Id, Algorithm.ToHex(nextMessage.ExtendedId), nextMessage.TypeId, stream.Length);
					SerializationException newException = new SerializationException(message, exc);
					throw newException;
				}
				messages.Add(nextMessage);
				if (evaluateMethod != null) evaluateMethod(nextMessage);
			}
			return messages;
		}

		/// <summary>
		/// Writes the given <see cref="ComponentRuntimeInfo"/> list to a <see cref="Stream"/>.
		/// </summary>
		/// <param name="runtimeInfo">The given <see cref="ComponentRuntimeInfo"/>.</param>
		/// <returns>Returns a <see cref="Stream"/> containing the <see cref="ComponentRuntimeInfo"/> list.</returns>
		public static Stream WriteRuntimeInfo(ComponentRuntimeInfo[] runtimeInfo)
		{
			MemoryStream stream = new MemoryStream();
			BinaryFormatter bf = new BinaryFormatter();
			bf.Serialize(stream, runtimeInfo);
			stream.Seek(0, SeekOrigin.Begin);
			return stream;
		}


	}
}
