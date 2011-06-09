using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Xml;
using MySpace.Common.IO;

namespace MySpace.Common.Barf
{
	/// <summary>
	/// Encapsulates arguments needed by <see cref="IBarfSerializer{T}.Deserialize"/> and <see cref="IBarfSerializer.Deserialize"/>.
	/// </summary>
	public class BarfDeserializationArgs
	{
		private BinaryFormatter _binaryFormatter;
		private int _objectDepth;
		private long _streamEnd;

		internal BarfDeserializationArgs(IPrimitiveReader reader)
		{
			Reader = reader;
		}

		public T DeserializeLegacyAutoSerializable<T>()
		{
			var args = new TypeSerializationArgs { Reader = Reader, Flags = SerializerFlags.Default };
			object instance = null;
			Serializer.Deserialize<T>(ref instance, args);
			return (T)instance;
		}

		/// <summary>
		/// Begins a region of code that deserializes a barf type.
		/// </summary>
		public BarfObjectHeader BeginObject<T>()
		{
			var def = BarfTypeDefinition.Get<T>(true);

			bool deserializeTypeTable = false;
			if (StreamHeader == null)
			{
				StreamHeader = BarfStreamHeader.ReadFrom(Reader);
				if (StreamHeader.FrameworkVersion > BarfFormatter.MaxFrameworkVersion)
				{
					string message = string.Format(
						"Encountered a BARF formatted stream with FrameworkVersion=\"{0}\" but MaxFrameworkVersion=\"{1}\".",
						StreamHeader.FrameworkVersion,
						BarfFormatter.MaxFrameworkVersion);
					throw new UnhandledVersionException(message);
				}

				deserializeTypeTable = StreamHeader.Flags.IsSet(HeaderFlags.HasNameTable);
			}

			var objectHeader = BarfObjectHeader.ReadFrom(Reader);

			if (!objectHeader.IsNull)
			{
				if (objectHeader.Version < def.MinVersion)
				{
					var message = string.Format(
						"Binary data was encoded with Version=\"{0}\" but the current MinVersion=\"{1}\".",
						objectHeader.Version,
						def.MinVersion);
					throw new UnhandledVersionException(message);
				}

				if (def.CurrentVersion < objectHeader.MinVersion)
				{
					var message = string.Format(
						"Binary data was encoded with a MinVersion=\"{0}\" but CurrentVersion=\"{1}\" is less than that.",
						objectHeader.MinVersion,
						def.CurrentVersion);
					throw new UnhandledVersionException(message);
				}

				if (objectHeader.Version < def.MinDeserializeVersion)
				{
					var message = string.Format(
						"Binary data is Version=\"{0}\" but MinDeserializeVersion=\"{1}\".",
						objectHeader.Version,
						def.MinDeserializeVersion);
					throw new UnhandledVersionException(message);
				}
			}

			if (deserializeTypeTable)
			{
				var currentPosition = Reader.BaseStream.Position;

				Reader.BaseStream.Seek(objectHeader.Length, SeekOrigin.Current);

				TypeTable = BarfTypeTable.ReadFrom(Reader);

				_streamEnd = Reader.BaseStream.Position;

				Reader.BaseStream.Seek(currentPosition, SeekOrigin.Begin);
			}

			++_objectDepth;
			return objectHeader;
		}

		/// <summary>
		/// Captures the future data.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="header">The header.</param>
		/// <param name="instance">The instance.</param>
		public void CaptureFutureData<T>(BarfObjectHeader header, ref T instance)
			where T : ISerializationInfo
		{
			if (header.EndPosition < Reader.BaseStream.Position)
			{
				RaiseInvalidData<T>(header, "Can't capture future data when the current position is past the current position.");
			}

			var remainingLength = header.EndPosition - Reader.BaseStream.Position;
			var futureData = new byte[remainingLength];
			Reader.BaseStream.Read(futureData, 0, futureData.Length);

			var typeInfo = BarfSerializationTypeInfo.Create(header, futureData);

			var info = (BarfSerializationInfo)instance.SerializationInfo ?? new BarfSerializationInfo();

			info.Add(typeof(T), typeInfo);

			instance.SerializationInfo = info;
		}

		/// <summary>
		/// Ends a region of code that deserializes a barf type.
		/// </summary>
		public void EndObject<T>(BarfObjectHeader header)
		{
			--_objectDepth;

			if (!header.IsNull && Reader.BaseStream.Position < header.EndPosition)
			{
				RaiseInvalidData<T>(header, "There is data remaining but there shouldn't be.");
			}

			if (_objectDepth == 0)
			{
				StreamHeader = null;
				TypeTable = null;
				if (_streamEnd > 0L)
				{
					Reader.BaseStream.Seek(_streamEnd, SeekOrigin.Begin);
					_streamEnd = 0L;
				}
			}
		}

		public void RaiseInvalidData<T>(BarfObjectHeader header, string message)
		{
			var builder = new StringBuilder();
			var w = XmlWriter.Create(builder, new XmlWriterSettings
			{
				Indent = true,
				IndentChars = "\t",
				OmitXmlDeclaration = true,
				ConformanceLevel = ConformanceLevel.Fragment,
				NewLineChars = Environment.NewLine,
				NewLineHandling = NewLineHandling.Entitize,
				NewLineOnAttributes = false
			});
			w.WriteStartElement("Details");
			{
				if (!string.IsNullOrEmpty(message))
				{
					w.WriteElementString("Message", message);
				}
				w.WriteElementString("Type", typeof(T).ToString());
				w.WriteElementString("Position", Reader.BaseStream.Position.ToString());
				w.WriteElementString("ObjectDepth", _objectDepth.ToString());
				if (header != null)
				{
					w.WriteStartElement("ObjectHeader");
					{

						w.WriteElementString("IsNull", header.IsNull.ToString());
						if (!header.IsNull)
						{
							w.WriteElementString("Version", header.Version.ToString());
							w.WriteElementString("MinVersion", header.MinVersion.ToString());
							w.WriteElementString("Length", header.Length.ToString());
							w.WriteElementString("StartPosition", header.StartPosition.ToString());
							w.WriteElementString("EndPosition", header.EndPosition.ToString());
						}
					}
					w.WriteEndElement();
				}
				w.WriteStartElement("CurrentDefinition");
				{
					var def = BarfTypeDefinition.Get<T>(false);
					if (def == null || !def.IsValid)
					{
						w.WriteElementString("Error", "Couldn't load definition for type - " + typeof(T));
					}
					else
					{
						w.WriteElementString("CurrentVersion", def.CurrentVersion.ToString());
						w.WriteElementString("MinVersion", def.MinVersion.ToString());
						w.WriteElementString("MinDeserializeVersion", def.MinDeserializeVersion.ToString());
					}
				}
				w.WriteEndElement();
				w.WriteStartElement("StreamHeader");
				{
					w.WriteElementString("FrameworkVersion", StreamHeader.FrameworkVersion.ToString());
					w.WriteElementString("Flags", StreamHeader.Flags.ToString());
					w.WriteElementString("Length", StreamHeader.Length.ToString());
				}
				w.WriteEndElement();
			}
			w.WriteEndElement();
			w.Flush();

			throw new InvalidDataException(builder.ToString());
		}

		public void RaiseInvalidData<T>(BarfObjectHeader header)
		{
			RaiseInvalidData<T>(header, null);
		}

		/// <summary>
		/// Gets the stream header.
		/// </summary>
		/// <value>The stream header.</value>
		public BarfStreamHeader StreamHeader { get; private set; }

		/// <summary>
		/// Gets the primitive reader that the object can be deserialized from.
		/// </summary>
		/// <value>The primitive reader that the object can be deserialized from.</value>
		public IPrimitiveReader Reader { get; private set; }

		/// <summary>
		/// Gets a type table containing type information that can be serialized.
		/// </summary>
		/// <value>The type table containing type information that can be serialized.</value>
		public BarfTypeTable TypeTable { get; private set; }

		/// <summary>
		/// Gets the binary formatter to use on objects that are otherwise un-supported by the barf framework.
		/// </summary>
		/// <value>The binary formatter.</value>
		public BinaryFormatter BinaryFormatter
		{
			get
			{
				if (_binaryFormatter == null)
				{
					_binaryFormatter = new BinaryFormatter();
				}

				return _binaryFormatter;
			}
		}
	}
}
