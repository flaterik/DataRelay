using System;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Text;
using Level = log4net.Core.Level;
using SystemStringFormat=log4net.Util.SystemStringFormat;
using CultureInfo = System.Globalization.CultureInfo;

//using MySpace.Diagnostics;

namespace MySpace.Logging
{
    public sealed class LogWrapper
    {
        private static readonly Type ThisType = typeof(LogWrapper);

        static LogWrapper()
        {
            string loggingConfigFile = "logging.production.config";
            string configFileValue=ConfigurationManager.AppSettings["LoggingConfigFile"];

            if (!string.IsNullOrEmpty(configFileValue))
                loggingConfigFile = configFileValue;

            /* Configure log4net based on a config file rather than a linked .config file. 
            * This allows to change logging without restarting the application pool.
                * */
            FileInfo configFile = new FileInfo(loggingConfigFile);

            if (!configFile.Exists)
            {
                configFile = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory , loggingConfigFile));
            }

            log4net.Config.XmlConfigurator.ConfigureAndWatch(configFile);
        }

        private log4net.ILog log;

        /// <summary>
        /// Creates a new instance of the logging wrapper by walking the stack to 
        /// find the calling class and configures the log based on this.
        /// </summary>
        public LogWrapper()
        {
            /*
             * Get the calling method, to determine the class name.
             * */
            StackFrame stackFrame = new StackFrame(1);

            string name = ExtractClassName(stackFrame.GetMethod());

            log = log4net.LogManager.GetLogger(name);
            
        }


        public bool IsDebugEnabled
        {
            get { return log.IsDebugEnabled; }
        }

        public bool IsErrorEnabled
        {
            get { return log.IsErrorEnabled; }
        }

        public bool IsInfoEnabled
        {
            get { return log.IsInfoEnabled; }
        }

        public bool IsWarnEnabled
        {
            get { return log.IsWarnEnabled; }
        }

        public bool IsSecurityWarningEnabled
        {
            get { return log.Logger.IsEnabledFor(Level.SecurityWarning); }
        }

        public bool IsSecurityErrorEnabled
        {
            get { return log.Logger.IsEnabledFor(Level.SecurityError); }
        }

        public bool IsSpamWarningEnabled
        {
            get { return log.Logger.IsEnabledFor(Level.SpamWarning); }
        }

        public bool IsSpamErrorEnabled
        {
            get { return log.Logger.IsEnabledFor(Level.SpamError); }
        }


        #region SecurityWarning
        public void SecurityWarning(object message, Exception exception)
        {
            if (IsSecurityWarningEnabled)
                log.Logger.Log(ThisType, Level.SecurityWarning, message, exception);
        }

        public void SecurityWarning(object message)
        {
            if (IsSecurityWarningEnabled)
                log.Logger.Log(ThisType, Level.SecurityWarning, message, null);
        }

        public void SecurityWarningFormat(IFormatProvider provider, string format, params object[] args)
        {
            if (IsSecurityWarningEnabled)
                log.Logger.Log(ThisType, Level.SecurityWarning,
                    new SystemStringFormat(provider, format, args),
                    null);
        }

        public void SecurityWarningFormat(string format, params object[] args)
        {
            if (IsSecurityWarningEnabled)
                log.Logger.Log(ThisType, Level.SecurityWarning,
                    new log4net.Util.SystemStringFormat(CultureInfo.InvariantCulture, format, args),
                    null);
        }
        #endregion
        #region SecurityError
        public void SecurityError(object message, Exception exception)
        {
            if (IsSecurityErrorEnabled)
                log.Logger.Log(ThisType, Level.SecurityError, message, exception);
        }

        public void SecurityError(object message)
        {
            if (IsSecurityErrorEnabled)
                log.Logger.Log(ThisType, Level.SecurityError, message, null);
        }

        public void SecurityErrorFormat(IFormatProvider provider, string format, params object[] args)
        {
            if (IsSecurityErrorEnabled)
                log.Logger.Log(ThisType, Level.SecurityError,
                    new SystemStringFormat(provider, format, args),
                    null);
        }

        public void SecurityErrorFormat(string format, params object[] args)
        {
            if (IsSecurityErrorEnabled)
                log.Logger.Log(ThisType, Level.SecurityError,
                    new log4net.Util.SystemStringFormat(CultureInfo.InvariantCulture, format, args),
                    null);
        }
        #endregion

        #region SpamWarning
        public void SpamWarning(object message, Exception exception)
        {
            if (IsSpamWarningEnabled)
                log.Logger.Log(ThisType, Level.SpamWarning, message, exception);
        }

        public void SpamWarning(object message)
        {
            if (IsSpamWarningEnabled)
                log.Logger.Log(ThisType, Level.SpamWarning, message, null);
        }

        public void SpamWarningFormat(IFormatProvider provider, string format, params object[] args)
        {
            if (IsSpamWarningEnabled)
                log.Logger.Log(ThisType, Level.SpamWarning,
                    new SystemStringFormat(provider, format, args),
                    null);
        }

        public void SpamWarningFormat(string format, params object[] args)
        {
            if (IsSpamWarningEnabled)
                log.Logger.Log(ThisType, Level.SpamWarning,
                    new log4net.Util.SystemStringFormat(CultureInfo.InvariantCulture, format, args),
                    null);
        }
        #endregion
        #region SpamError
        public void SpamError(object message, Exception exception)
        {
            if (IsSpamErrorEnabled)
                log.Logger.Log(ThisType, Level.SpamError, message, exception);
        }

        public void SpamError(object message)
        {
            if (IsSpamErrorEnabled)
                log.Logger.Log(ThisType, Level.SpamError, message, null);
        }

        public void SpamErrorFormat(IFormatProvider provider, string format, params object[] args)
        {
            if (IsSpamErrorEnabled)
                log.Logger.Log(ThisType, Level.SpamError,
                    new SystemStringFormat(provider, format, args),
                    null);
        }

        public void SpamErrorFormat(string format, params object[] args)
        {
            if (IsSpamErrorEnabled)
                log.Logger.Log(ThisType, Level.SpamError,
                    new log4net.Util.SystemStringFormat(CultureInfo.InvariantCulture, format, args),
                    null);
        }
        #endregion

        #region Debug
        public void Debug(object message, Exception exception)
        {
            log.Debug(message, exception);
        }

        public void Debug(object message)
        {
            log.Debug(message);
        }

        public void DebugFormat(IFormatProvider provider, string format, params object[] args)
        {
            log.DebugFormat(provider, format, args);
        }

        public void DebugFormat(string format, params object[] args)
        {
            log.DebugFormat(format, args);
        }
        #endregion

        #region Error
        public void Error(object message, Exception exception)
        {
            log.Error(message, exception);
        }

        public void Error(object message)
        {
            log.Error(message);
        }

        public void Error(Exception exception)
        {
            log.Error(null, exception);
        }

        public void ErrorFormat(IFormatProvider provider, string format, params object[] args)
        {
            log.ErrorFormat(provider, format, args);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            log.ErrorFormat(format, args);
        }
        #endregion

        #region Info
        public void Info(object message, Exception exception)
        {
            log.Info(message, exception);
        }

        public void Info(object message)
        {
            log.Info(message);
        }

        public void InfoFormat(IFormatProvider provider, string format, params object[] args)
        {
            log.InfoFormat(provider, format, args);
        }

        public void InfoFormat(string format, params object[] args)
        {
            log.InfoFormat(format, args);
        }
        #endregion

        #region Warn
        public void Warn(object message, Exception exception)
        {
            log.Warn(message, exception);
        }

        public void Warn(object message)
        {
            log.Warn(message);
        }

        public void WarnFormat(IFormatProvider provider, string format, params object[] args)
        {
            log.WarnFormat(provider, format, args);
        }

        public void WarnFormat(string format, params object[] args)
        {
            log.WarnFormat(format, args);
        }
        #endregion

        #region Method Debug (Uses call-stack to output method name)
        /// <summary>
        /// Delegate to allow custom information to be logged
        /// </summary>
        /// <param name="logOutput">Initialized <see cref="StringBuilder"/> object which will be appended to output string</param>
        public delegate void LogOutputMapper(StringBuilder logOutput);

        public void MethodDebugFormat(IFormatProvider provider, string format, params object[] args)
        {
            if(log.IsDebugEnabled)
                log.DebugFormat(provider, string.Format("Page: {2}, MethodName: {1}, {0}", format, GetDebugCallingMethod(), GetDebugCallingPage()), args);
        }

        public void MethodDebugFormat(string format, params object[] args)
        {
            if (log.IsDebugEnabled)
                log.DebugFormat(string.Format("Page: {2}, MethodName: {1}, {0}", format, GetDebugCallingMethod(), GetDebugCallingPage()), args);
        }

        public void MethodDebug(string message)
        {
            if (log.IsDebugEnabled)
                log.Debug(string.Format("Page: {2}, MethodName: {1}, {0}", message, GetDebugCallingMethod(), GetDebugCallingPage()));
        }

        // With Log Prefix

        public void MethodDebugFormat(IFormatProvider provider, string logPrefix, string format, params object[] args)
        {
            if (log.IsDebugEnabled)
                log.DebugFormat(provider, string.Format("{0}| {1} , MethodName: {2} , Page: {3}", logPrefix, format, GetDebugCallingMethod(), GetDebugCallingPage()), args);
        }

        public void MethodDebugFormat(string logPrefix, string format, params object[] args)
        {
            if (log.IsDebugEnabled)
                log.DebugFormat(string.Format("{0}| Page: {3}, MethodName: {2} , {1}", logPrefix, format, GetDebugCallingMethod(), GetDebugCallingPage()), args);
        }

        public void MethodDebug(string logPrefix, string message)
        {
            if (log.IsDebugEnabled)
                log.Debug(string.Format("{0}| Page: {3}, MethodName: {2}, {1}", logPrefix, message, GetDebugCallingMethod(), GetDebugCallingPage()));
        }

        // With Log Prefix and delegate to add custom logging info
        public void MethodDebugFormat(string logPrefix, LogOutputMapper customLogOutput, string format, params object[] args)
        {
            if (log.IsDebugEnabled)
            {
                StringBuilder additionalLogData = new StringBuilder();
                if (customLogOutput != null)
                    customLogOutput(additionalLogData);

                log.DebugFormat(string.Format("{0}| Page: {3}, MethodName: {2}, {1}, {4}", logPrefix, format, GetDebugCallingMethod(), GetDebugCallingPage(), additionalLogData.ToString()), args);
            }
        }

		/// <summary>
        /// Returns calling method name using current stack 
        /// and assuming that first non Logging method is the parent
        /// </summary>
        /// <returns>Method Name</returns>
        private string GetDebugCallingMethod()
        {
            // Walk up the stack to get parent method
            StackTrace st = new StackTrace();
            if (st != null)
            {
                for (int i = 0; i < st.FrameCount; i++)
                {
                    StackFrame sf = st.GetFrame(i);
                    MethodBase method = sf.GetMethod();
                    if (method != null)
                    {
                        string delaringTypeName = method.DeclaringType.FullName;
                        if (delaringTypeName != null && delaringTypeName.IndexOf("MySpace.Logging") < 0)
                            return method.Name;
                    }
                }
            }

            return "Unknown Method";
        }

		public string CurrentStackTrace()
		{
			StringBuilder sb = new StringBuilder();
			// Walk up the stack to return everything
			StackTrace st = new StackTrace();
			if (st != null)
			{
				for (int i = 0; i < st.FrameCount; i++)
				{
					StackFrame sf = st.GetFrame(i);
					MethodBase method = sf.GetMethod();
					if (method != null)
					{
					    Type declaringType = method.DeclaringType;
                        //If the MemberInfo object is a global member, (that is, it was obtained from Module.GetMethods(), 
                        //which returns global methods on a module), then the returned DeclaringType will be null reference
                        if(declaringType == null)
                            continue;
						string declaringTypeName = declaringType.FullName;
						if (declaringTypeName != null && declaringTypeName.IndexOf("MySpace.Logging") < 0)
						{
							sb.AppendFormat("{0}.{1}(", declaringTypeName, method.Name);

							ParameterInfo[] paramArray = method.GetParameters();

							if (paramArray.Length > 0)
							{
								for (int j = 0; j < paramArray.Length; j++)
								{
									sb.AppendFormat("{0} {1}", paramArray[j].ParameterType.Name, paramArray[j].Name);
									if (j + 1 < paramArray.Length)
									{
										sb.Append(", ");
									}
								}
							}
							sb.AppendFormat(")\n - {0}, {1}", sf.GetFileLineNumber(), sf.GetFileName());
						}
					}
					else
					{
						sb.Append("The method returned null\n");
					}
				}
			}
			else
			{
				sb.Append("Unable to get stack trace");
			}

			return sb.ToString();
		}

        /// <summary>
        /// Returns ASP.NET method name which called current method. 
        /// Uses call stack and assumes that all methods starting with 'ASP.' are the ASP.NET page methods
        /// </summary>
        /// <returns>Class Name of the ASP.NET page</returns>
        private string GetDebugCallingPage()
        {
            // Walk up the stack to get calling method which is compiled ASP.Net page
            StackTrace st = new StackTrace();
            if (st != null)
            {
                for (int i = 0; i < st.FrameCount; i++)
                {
                    StackFrame sf = st.GetFrame(i);
                    MethodBase method = sf.GetMethod();
                    if (method != null && method.DeclaringType != null)
                    {
                        string declaringTypeName = method.DeclaringType.FullName;
                        if (declaringTypeName != null && declaringTypeName.IndexOf("ASP.") == 0)
                            return declaringTypeName;
                    }
                }
            }

            return "Unknown Page";
        }

        #endregion
		
		#region ILogMore methods

		public void MoreInfo(params object[] traceMessages)
		{
            if (log.IsInfoEnabled && null!=traceMessages) log.Info(string.Concat(traceMessages));
		}

		public void MoreError(params object[] traceMessages)
		{
            if (log.IsErrorEnabled && null != traceMessages) log.Error(string.Concat(traceMessages));
		}

		public void MoreWarn(params object[] traceMessages)
		{
            if (log.IsWarnEnabled && null != traceMessages) log.Warn(string.Concat(traceMessages));
		}

		public void MoreDebug(params object[] traceMessages)
		{
            if (log.IsDebugEnabled && null != traceMessages) log.Debug(string.Concat(traceMessages));
		}

        [Obsolete("Fatal is not a supported level.")]
		public void MoreFatal(params object[] traceMessages)
		{
            if (log.IsErrorEnabled && null != traceMessages) log.Error(string.Concat(traceMessages));
		}

    	public bool IsMoreDebugEnabled
    	{
            get { return log.IsDebugEnabled && IsMoreEnabled; }
    	}

    	public bool IsMoreInfoEnabled
    	{
            get { return log.IsInfoEnabled && IsMoreEnabled; }
    	}

    	public bool IsMoreErrorEnabled
    	{
            get { return log.IsErrorEnabled && IsMoreEnabled; }
    	}

    	public bool IsMoreWarnEnabled
    	{
            get { return log.IsWarnEnabled && IsMoreEnabled; }
    	}

    	public bool IsMoreFatalEnabled
    	{
			get { return log.IsFatalEnabled && IsMoreEnabled; }
    	}

		#endregion

        private bool IsMoreEnabled
        {
            get
            {
                if (System.Web.HttpContext.Current == null)
                    return false;


                return false;
            }
        }

        /// <summary>
        /// Method is to be used by unit tests to get TraceLogging.
        /// </summary>
        public static void ConfigureForUnitTesting()
        {
            log4net.Appender.TraceAppender traceAppender = new log4net.Appender.TraceAppender();
            traceAppender.Layout = new log4net.Layout.PatternLayout("%d [%t] %-5p %c [%x] - %m%n");
            traceAppender.ImmediateFlush = true;
            log4net.Config.BasicConfigurator.Configure(traceAppender);
        }



        #region Exception Logging
        /// <summary>
        /// Logs exception 
        /// </summary>
        /// <param name="exc">Exception to log</param>
        /// <param name="policyName">Policy name to append to logged exception</param>
        /// <remarks>
        /// Does not rethrow exceptions. Use throw; statement to rethrow original exception within catch() block
        /// </remarks>
        /// <returns>true if successful</returns>
        [Obsolete("This is a bad pattern and should not be used")]
        public bool HandleException(Exception exc, string policyName)
        {
            log.Warn(policyName, exc);
            return true;
        }
        #endregion

        #region Helper Functions

        private static string ExtractClassName(MethodBase callingMethod)
        {
            string name;

            if (callingMethod == null)
            {
                name = "Unknown";
            }
            else
            {
                Type callingType = callingMethod.DeclaringType;

                if (callingType != null)
                {
                    // This is the typical way to get a name on a managed stack.
                    name = callingType.FullName;
                }
                else
                {
                    // In an unmanaged stack, or in a static function without
                    // a declaring type, try getting everything up to the
                    // function being called (everything before the last dot).
                    name = callingMethod.Name;
                    int lastDotIndex = name.LastIndexOf('.');
                    if (lastDotIndex > 0)
                    {
                        name = name.Substring(0, lastDotIndex);
                    }
                }
            }

            return name;
        }


        #endregion
    }
}
