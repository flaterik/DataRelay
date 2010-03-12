using System;
using System.Collections.Generic;
using System.Text;

using System.Reflection;
using System.Threading;

namespace MySpace.DataRelay
{
    /// <summary>
    /// Provides Runtime loading of Classes.
    /// </summary>
	public class Factory : MarshalByRefObject
	{

        private static readonly MySpace.Logging.LogWrapper log = new MySpace.Logging.LogWrapper();
        
        /// <summary>
        /// 	<para>Overriden.  Obtains a lifetime service object to control the lifetime policy for this instance.</para>
        /// </summary>
        /// <returns>
        /// 	<para>An object of type <see cref="System.Runtime.Remoting.Lifetime.ILease"></see> used to control the lifetime policy for this instance. This is the current lifetime service object for this instance if one exists; otherwise, a new lifetime service object initialized to the value of the <see cref="System.Runtime.Remoting.Lifetime.LifetimeServices.LeaseManagerPollTime"></see> property.</para>
        /// </returns>
        /// <exception cref="System.Security.SecurityException">
        /// 	<para>The immediate caller does not have infrastructure permission.</para>
        /// </exception>
        /// <filterpriority>
        /// 	<para>2</para>
        /// </filterpriority>
        /// <PermissionSet>
        /// 	<IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="RemotingConfiguration, Infrastructure"/>
        /// </PermissionSet>
		public override object InitializeLifetimeService()
		{
			return null;
		}		
		
        /// <summary>
        /// Loads the named class from the given named assembly.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly.</param>
        /// <param name="className">The namd of the class.</param>
        /// <param name="fileName">Out.  The filename that was loaded.</param>
        /// <returns>An instance of the named class.</returns>
		public object LoadClass(string assemblyName, string className, out string fileName)
		{			
			object loadedClass = null;
			fileName = String.Empty;
            if (log.IsInfoEnabled)
                log.InfoFormat("Loading {0} from {1}.",className,assemblyName);			
			try
			{				
				Assembly assembly = AppDomain.CurrentDomain.Load(assemblyName);
                if (log.IsInfoEnabled)
                    log.InfoFormat("Got {0} from file {1}.", assembly.FullName, assembly.Location);
				fileName = assembly.Location.Substring(assembly.Location.LastIndexOf(@"\")+1).ToLower();
				loadedClass = assembly.CreateInstance(className);
			}
			catch (Exception ex)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error loading assembly: {0}", ex);
				loadedClass = null;
			}

			return loadedClass;
		}

	}
}
