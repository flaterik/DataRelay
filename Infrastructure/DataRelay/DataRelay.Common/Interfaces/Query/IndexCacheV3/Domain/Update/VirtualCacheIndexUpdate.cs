using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class VirtualCacheIndexUpdate: CacheIndexUpdate, IVirtualCacheType
	{
		#region Ctors
		public VirtualCacheIndexUpdate()
		{
			Init(null);
		}

        public VirtualCacheIndexUpdate(Command command, string cacheTypeName)
            :base (command)
        {
           Init(cacheTypeName);
        }

		private void Init(string cacheTypeName)
		{
			this.cacheTypeName = cacheTypeName;
		}
		#endregion

		#region IVirtualCacheType Members
		protected string cacheTypeName;

		public string CacheTypeName
		{
			get
			{
				return cacheTypeName;
			}
			set
			{
				cacheTypeName = value;
			}
		}
		#endregion
	}
}
