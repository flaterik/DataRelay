using System.Collections.Generic;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class VirtualCacheIndex: CacheIndex, IVirtualCacheType
	{
		#region Ctors
		public VirtualCacheIndex()
		{
			Init(null);
		}

		//With targetIndexName and with NO indexTagMapping
		public VirtualCacheIndex(byte[] indexId, string targetIndexName, List<IndexDataItem> addList, string cacheTypeName)
			: base(indexId, targetIndexName, addList)
		{
			Init(cacheTypeName);
		}
		
		public VirtualCacheIndex(byte[] indexId, string targetIndexName, List<IndexDataItem> addList, List<IndexItem> deleteList, string cacheTypeName)
			: base(indexId, targetIndexName, addList, deleteList)
		{
			Init(cacheTypeName);
		}

		public VirtualCacheIndex(byte[] indexId, string targetIndexName, List<IndexDataItem> addList, List<IndexItem> deleteList, byte[] metadata, bool updateMetadata, bool replaceFullIndex, string cacheTypeName)
			: base(indexId, targetIndexName, addList, deleteList, metadata, updateMetadata, replaceFullIndex)
		{
			Init(cacheTypeName);
		}

        public VirtualCacheIndex(byte[] indexId, string targetIndexName, List<IndexDataItem> addList, List<IndexItem> deleteList, byte[] metadata, bool updateMetadata, bool replaceFullIndex, bool preserveData, string cacheTypeName)
            : base(indexId, targetIndexName, addList, deleteList, metadata, updateMetadata, replaceFullIndex, preserveData)
        {
            Init(cacheTypeName);
        }

		//With indexTagMapping and with NO targetIndexName
		public VirtualCacheIndex(byte[] indexId, Dictionary<string, List<string>> indexTagMapping, List<IndexDataItem> addList, string cacheTypeName)
			: base(indexId, indexTagMapping, addList)
		{
			Init(cacheTypeName);
		}

		public VirtualCacheIndex(byte[] indexId, Dictionary<string, List<string>> indexTagMapping, List<IndexDataItem> addList, List<IndexItem> deleteList, string cacheTypeName)
			: base(indexId, indexTagMapping, addList, deleteList)
		{
			Init(cacheTypeName);
		}

		public VirtualCacheIndex(byte[] indexId, Dictionary<string, List<string>> indexTagMapping, List<IndexDataItem> addList, List<IndexItem> deleteList, byte[] metadata, bool updateMetadata, bool replaceFullIndex, string cacheTypeName)
			: base(indexId, indexTagMapping, addList, deleteList, metadata, updateMetadata, replaceFullIndex)
		{
			Init(cacheTypeName);
		}

        public VirtualCacheIndex(byte[] indexId, Dictionary<string, List<string>> indexTagMapping, List<IndexDataItem> addList, List<IndexItem> deleteList, byte[] metadata, bool updateMetadata, bool replaceFullIndex, bool preserveData, string cacheTypeName)
            : base(indexId, indexTagMapping, addList, deleteList, metadata, updateMetadata, replaceFullIndex, preserveData)
        {
            Init(cacheTypeName);
        }

        //VirtualCount Update
        public VirtualCacheIndex(byte[] indexId, Dictionary<string, int> indexVirtualCountMapping, string cacheTypeName)
            : base(indexId, indexVirtualCountMapping)
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
