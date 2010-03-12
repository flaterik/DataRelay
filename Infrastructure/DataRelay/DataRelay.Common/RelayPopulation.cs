using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay
{
	[Serializable()]
	public class RelayPopulation : TypePopulation//, IVersionSerializable
	{
		public RelayPopulation()
		{
			TypePopulations = new List<TypePopulation>();
		}
		
		public string ServiceLocation;
		public string ServiceGroup;
		public List<TypePopulation> TypePopulations = null;

		[NonSerialized()]
		public new readonly int TypeID;

		#region IVersionSerializable Members

		public new void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			base.Serialize(writer);
			writer.Write(ServiceGroup);
			writer.Write(ServiceLocation);
			writer.WriteList<TypePopulation>(TypePopulations);
		}

		public new void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
		{
			base.Deserialize(reader,version);
			this.ServiceGroup = reader.ReadString();
			this.ServiceLocation = reader.ReadString();
			TypePopulations = reader.ReadList<TypePopulation>();
		}
				
		public new int CurrentVersion
		{
			get { return 1; }
		}

		#endregion
	}

	[Serializable()]
	public class TypePopulation : IVersionSerializable
	{

		public Int32 TypeID;
		public Int64 CurrentPopulation;
		public Int64 ScavengeMax;
		public Int64 MaxPopulation;

		public TypePopulation()
		{

		}

		public TypePopulation(int typeID)
		{
			TypeID = typeID;
		}

		#region IVersionSerializable Members

		public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			writer.Write(TypeID);
			writer.Write(CurrentPopulation);
			writer.Write(ScavengeMax);
			writer.Write(MaxPopulation);
		}
		
		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
		{
			this.TypeID = reader.ReadInt32();
			this.CurrentPopulation = reader.ReadInt64();
			this.ScavengeMax = reader.ReadInt64();
			this.MaxPopulation = reader.ReadInt64();
		}

		public void Deserialize(IPrimitiveReader reader)
		{
			Deserialize(reader, CurrentVersion);
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
	}
}
