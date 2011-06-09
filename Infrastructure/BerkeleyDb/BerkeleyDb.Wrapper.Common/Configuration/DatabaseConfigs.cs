using System.Collections.Generic;
using System.Collections.ObjectModel;
using BerkeleyDbWrapper;

namespace MySpace.BerkeleyDb.Configuration
{
	public class DatabaseConfigs : XmlSerializableKeyedCollection<int, DatabaseConfig>
	{
		private const int adminId = -1;   //the id for the admin db
		private const int defaultId = 0;  //the setting used for dbs that are not explicitly configured
		
		private static readonly DatabaseConfig defaultAdminDb;
		private static readonly DatabaseConfig defaultDefaultDb;        
		
		static DatabaseConfigs()
		{
			defaultAdminDb = new DatabaseConfig(-1) {Flags = DbFlags.None};
			//Can't use txnnotdurable in admin db because it's used outside the environmnet so we need to make sure it's not set

			defaultDefaultDb = new DatabaseConfig(defaultId);
		}

		protected override int GetKeyForItem(DatabaseConfig item)
		{
			return item.Id;
		}

		/// <summary>
		/// 	<para>Overriden. Gets the namespace of the collection</para>
		/// </summary>
		/// <returns>
		/// 	<para>The <see cref="string"/> namespace.</para>
		/// </returns>
		protected override string GetNamespace()
		{
			return "http://myspace.com/BerkeleyDbConfig.xsd";
		}

		public DatabaseConfig GetConfigFor(int id)
		{
			if (!Contains(id))             //use one of the defaults
			{
				if (id == adminId)
					//it wasn't in the db, so it wasn't explicity configured. return the hard default admin
				{
					return defaultAdminDb;
				}
				if (!Contains(defaultId))
					//neither the requested ID nor 0 was in the config, so return the hard default.
				{
					return defaultDefaultDb;
				}


				return this[defaultId];
			}

			return this[id];
		}
		
		private DatabaseConfig GetClonedConfigFor(int id)
		{
			return GetConfigFor(id).Clone(id);
		}

		public DatabaseConfig GetConfigFor(int typeId, int objectId)
		{
			DatabaseConfig dbConfig = GetClonedConfigFor(typeId);
			dbConfig.SetFederationIndex(objectId);
			return dbConfig;
		}

		public DatabaseConfig GetConfigForFederated(int typeId, int federationIndex)
		{
			DatabaseConfig dbConfig = GetClonedConfigFor(typeId);
			dbConfig.FederationIndex = federationIndex;
			return dbConfig;
		}

		public int GetFederationSize(int id)
		{
			DatabaseConfig dbConfig = GetConfigFor(id);
			int federationSize = dbConfig.FederationSize;
			if (federationSize <= 0)
			{
				federationSize = 1;
			}
			return federationSize;
		}
	}
}