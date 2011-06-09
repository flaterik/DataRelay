using System.Collections.Generic;
using MySpace.Common;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	internal class ErrorQueueState : IVersionSerializable
	{
		internal Dictionary<string, Dictionary<string, ErrorQueue>> ErrorQueues;

		#region IVersionSerializable Members

		public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			if (ErrorQueues != null)
			{
				writer.Write(true);
				writer.Write(ErrorQueues.Keys.Count);				
				foreach (string key in ErrorQueues.Keys)
				{
					writer.Write(key);
					Dictionary<string, ErrorQueue> groupQueue = ErrorQueues[key];
					if (groupQueue != null)
					{
						writer.Write(true);
						writer.Write(groupQueue.Keys.Count);
						if (groupQueue.Keys.Count > 0)
						{
							foreach (string serviceName in groupQueue.Keys)
							{
								writer.Write(serviceName);								
								writer.Write(groupQueue[serviceName],false);
							}
						}
					}
					else
					{
						writer.Write(false);
					}
				}				
			}
			else
			{
				writer.Write(false);
			}
		}

		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
		{
			if (reader.ReadBoolean())
			{
				int groupsCount = reader.ReadInt32();
				ErrorQueues = new Dictionary<string, Dictionary<string, ErrorQueue>>(groupsCount);
				for (int i = 0; i < groupsCount; i++)
				{
					string group = reader.ReadString();
					if(reader.ReadBoolean())
					{
						int serviceCount = reader.ReadInt32();
						Dictionary<string, ErrorQueue> groupQueues = new Dictionary<string, ErrorQueue>(serviceCount);
						for (int j = 0; j < serviceCount; j++)
						{
							string serviceName = reader.ReadString();
							groupQueues.Add(serviceName, reader.Read<ErrorQueue>());
						}
						ErrorQueues.Add(group, groupQueues);
					}					
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


		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
		{
			Deserialize(reader, CurrentVersion);
		}

		#endregion
	}
}
