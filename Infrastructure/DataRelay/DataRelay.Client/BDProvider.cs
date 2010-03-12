using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common.HelperObjects;

namespace MySpace.DataRelay.Client.BlueDragon
{
	public class Provider
	{
		public string exceptionInfo = "";//used to output errors to BlueDragon

		public Provider()
		{
		}

		public void Delete(int objectId, string typeName)
		{
			try
			{
				RelayClient.Instance.DeleteObject(objectId, typeName);				
			}
			catch (Exception e)
			{
				ReportException(e);
			}
		}

		public void DeleteExtended(string extendedId, string typeName)
		{
			try
			{
                if (extendedId != null)
                {
                    
                    RelayClient.Instance.DeleteObject(StringUtility.GetStringHash(extendedId), extendedId, typeName);
                }                
			}
			catch (Exception e)
			{
				ReportException(e);
			}
		}

		public void DeleteExtended(int primaryId, string extendedId, string typeName)
		{
			try
			{
				RelayClient.Instance.DeleteObject(primaryId, extendedId, typeName);
			}
			catch (Exception e)
			{
				ReportException(e);
			}
		}

		public void DeleteInAllTypes(int objectId)
		{
			try
			{
				RelayClient.Instance.DeleteObjectInAllTypes(objectId);				
			}
			catch (Exception e)
			{
				ReportException(e);
			}
		}

		public void DeleteExtendedInAllTypes(string extendedId)
		{
			try
			{
                if (extendedId != null)
                {
                    RelayClient.Instance.DeleteObjectInAllTypes(StringUtility.GetStringHash(extendedId), extendedId);
                }
			}
			catch (Exception e)
			{
				ReportException(e);
			}
		}

		public void DeleteExtendedInAllTypes(int primaryId, string extendedId)
		{
			try
			{
				RelayClient.Instance.DeleteObjectInAllTypes(primaryId, extendedId);
			}
			catch (Exception e)
			{
				ReportException(e);
			}
		}

		protected void ReportException(Exception e)
		{
			if (e != null)
			{
				exceptionInfo = exceptionInfo + e.Message + e.StackTrace;
			}
            if (RelayClient.log.IsErrorEnabled)
                RelayClient.log.Error("Exception in BD Provider: {0}", e);
		}
	}
}
