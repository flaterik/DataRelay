using MySpace.Common;
using MySpace.Common.HelperObjects;

namespace MySpace.DataRelay
{
	[SerializableClass(Inline = true)]
	public class GenericStorage<T> : IExtendedCacheParameter, IVirtualCacheType
	{
		private readonly string _payloadTypeName;
		protected GenericStorage()
		{
			_payloadTypeName = typeof(T).FullName;
		}

		public GenericStorage(string key)
			: this()
		{
			ExtendedId = key;
		}

		public GenericStorage(string key, T value)
			: this()
		{
			ExtendedId = key;
			Payload = value;
		}

		[SerializableProperty(1)]
		private T _payload;

		public T Payload
		{
			get
			{
				return _payload;
			}
			set
			{
				_payload = value;
			}
		}

		public MySpace.Common.Framework.DataSource DataSource { get; set; }

		public bool IsEmpty { get; set; }

		private int _primaryId;
		public int PrimaryId
		{
			get
			{
				return _primaryId;
			}
			set { }
		}

		private string _extendedId;
		public string ExtendedId
		{
			get
			{
				return _extendedId;
			}
			set
			{
				_extendedId = _payloadTypeName + value;
				_primaryId = StringUtility.GetStringHashFast(_extendedId);
			}
		}

		public System.DateTime? LastUpdatedDate { get; set; }


		//This is because the framework will report the name with full generic
		//type information, and we want all objects to be stored under the same type name.
		//Alternately, if you wanted to store different object payload types in different
		//type ids, you could return a different type name for each.
		protected string reportedTypeName = "MySpace.DataRelay.GenericStorage";
		public string CacheTypeName
		{
			get
			{
				return reportedTypeName;
			}
			set { }
		}


	}
}
