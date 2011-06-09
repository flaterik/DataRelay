using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using MySpace.Common;
using System.Xml;
using System.Xml.Serialization;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage
{
	public class CacheIndexV3StoreState : IVersionSerializable
	{
		
		//public DateTime SaveTimestamp  = DateTime.MinValue;
		public string HomeDirectory;
		public CacheIndexV3StorageConfiguration StorageConfiguration;

		public CacheIndexV3StoreState()
		{
			// parameter less constructor required for MySpace.Common.IO.Serializer
		}
		public CacheIndexV3StoreState(CacheIndexV3Store store)
		{
            HomeDirectory = store.storageConfiguration.BerkeleyDbConfig.EnvironmentConfig.HomeDirectory;
			StorageConfiguration = store.storageConfiguration;
		}

		#region IVersionSerializable Members
		public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			writer.Write(HomeDirectory);

			//XmlSerializer xmlSerialzer = new XmlSerializer(typeof(CacheIndexV3StorageConfiguration));

			//MemoryStream memoryStream = new MemoryStream();
			//XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8);
			//xmlSerialzer.Serialize(xmlTextWriter, StorageConfiguration,);
			//memoryStream = (MemoryStream)xmlTextWriter.BaseStream;
			//Encoding encoding = new UTF8Encoding();
			//string str = encoding.GetString(memoryStream.ToArray());

			//writer.Write(str);
		}
		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
		{
			Deserialize(reader);
		}
		public int CurrentVersion
		{
			get { return 1; }
		}
		public bool Volatile
		{
			get { return true; }
		}
		#endregion

		#region ICustomSerializable Members
		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
		{
			HomeDirectory = reader.ReadString();

			//try
			//{
			//   //string str = reader.ReadString();
			//   //XmlSerializer XmlSerializer = new XmlSerializer(typeof(CacheIndexV3StorageConfiguration));
			//   //UTF8Encoding encoding = new UTF8Encoding();
			//   //byte[] buffer = encoding.GetBytes(str);
			//   //MemoryStream memoryStream = new MemoryStream(buffer);
			//   //XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8);
			//   //StorageConfiguration = (CacheIndexV3StorageConfiguration)XmlSerializer.Deserialize(memoryStream);
			//}
			//catch
			//{
			//   LoggingWrapper.Write("Unable to deserialize CacheIndexV3StorageConfiguration object");
			//   throw new ApplicationException("Unable to deserialize CacheIndexV3StorageConfiguration object");
			//}
		}
		#endregion

		public bool IsUsable()
		{
			bool isUsable = false;
			
			
			return isUsable;
		}
	}
}
