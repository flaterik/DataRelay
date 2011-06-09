using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.DataRelay.Common.Interfaces.Query
{
	public class ListQuery : IRelayMessageQuery
	{
		private byte[] contiansNodeId;

	

		#region IRelayMessageQuery Members

		public byte QueryId
		{
			get { throw new Exception("The method or operation is not implemented."); }
		}

		#endregion

		#region IVersionSerializable Members

		public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			throw new Exception("The method or operation is not implemented.");
		}

		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
		{
			throw new Exception("The method or operation is not implemented.");
		}

		public int CurrentVersion
		{
			get { throw new Exception("The method or operation is not implemented."); }
		}

		public bool Volatile
		{
			get { throw new Exception("The method or operation is not implemented."); }
		}

		#endregion

		#region ICustomSerializable Members


		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
		{
			throw new Exception("The method or operation is not implemented.");
		}

		#endregion
	}
}
