using System;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Text;
using Level = log4net.Core.Level;
using SystemStringFormat=log4net.Util.SystemStringFormat;
using CultureInfo = System.Globalization.CultureInfo;
using System.Threading;

//using MySpace.Diagnostics;

namespace MySpace.Logging
{
	public sealed class LogWrapper
	{
		private static readonly Type ThisType = typeof(LogWrapper);
		private static readonly bool _useSyncLogging;
		private static readonly bool _isInitialized;

		static LogWrapper()
		{
			string loggingConfigFile = "logging.production.config";
			string configFileValue=ConfigurationManager.AppSettings["LoggingConfigFile"];

			if (!string.IsNullOrEmpty(configFileValue))
				loggingConfigFile = configFileValue;

			/* Configure log4net based on a config file rather than a linked .config file. 
			* This allows to change logging without restarting the application pool.
			*/
			FileInfo configFile = new FileInfo(loggingConfigFile);

			if (!configFile.Exists)
			{
				configFile = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory , loggingConfigFile));
			}

			if (!configFile.Exists)
			{
				ConfigureForUnitTesting(); // No config found--log to trace by default for unit testing.
			}

			_isInitialized = configFile.Exists;
			log4net.Config.XmlConfigurator.ConfigureAndWatch(configFile);

			string syncLogging = ConfigurationManager.AppSettings["UseSyncLogging"];

			if (!string.IsNullOrEmpty(syncLogging) && syncLogging.ToLowerInvariant() == "true")
			{
				_useSyncLogging = true;
			}
		}

		private bool UseSyncLogging
		{
			get { return _useSyncLogging || _unitTestingConfigured; }
		}

		/// <summary>
		/// Gets a value indicating whether or not logging was initialized correctly.
		/// </summary>
		/// <value>
		/// 	<see langword="true"/> if this instance is initialized; otherwise, <see langword="false"/>.
		/// </value>
		public static bool IsInitialized
		{
			get { return _isInitialized; }
		}

		private readonly log4net.ILog _log;

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

			_log = log4net.LogManager.GetLogger(name);			
		}

		#region Enabled Checks
		
		public bool IsDebugEnabled
		{
			get { return _log.IsDebugEnabled; }
		}

		public bool IsErrorEnabled
		{
			get { return _log.IsErrorEnabled; }
		}

		public bool IsInfoEnabled
		{
			get { return _log.IsInfoEnabled; }
		}

		public bool IsWarnEnabled
		{
			get { return _log.IsWarnEnabled; }
		}

		public bool IsSecurityWarningEnabled
		{
			get { return _log.Logger.IsEnabledFor(Level.SecurityWarning); }
		}

		public bool IsSecurityErrorEnabled
		{
			get { return _log.Logger.IsEnabledFor(Level.SecurityError); }
		}

		public bool IsSpamWarningEnabled
		{
			get { return _log.Logger.IsEnabledFor(Level.SpamWarning); }
		}

		public bool IsSpamErrorEnabled
		{
			get { return _log.Logger.IsEnabledFor(Level.SpamError); }
		}

		#endregion

		#region SecurityWarning

		public void SecurityWarning(object message, Exception exception)
		{
			if (IsSecurityWarningEnabled)
				_log.Logger.Log(ThisType, Level.SecurityWarning, message, exception);
		}

		public void SecurityWarning(object message)
		{
			if (IsSecurityWarningEnabled)
				_log.Logger.Log(ThisType, Level.SecurityWarning, message, null);
		}

		public void SecurityWarningFormat(IFormatProvider provider, string format, params object[] args)
		{
			if (IsSecurityWarningEnabled)
				_log.Logger.Log(ThisType, Level.SecurityWarning,
					new SystemStringFormat(provider, format, args),
					null);
		}

		public void SecurityWarningFormat(string format, params object[] args)
		{
			if (IsSecurityWarningEnabled)
				_log.Logger.Log(ThisType, Level.SecurityWarning,
					new SystemStringFormat(CultureInfo.InvariantCulture, format, args),
					null);
		}
		
		#endregion
		
		#region SecurityError
		public void SecurityError(object message, Exception exception)
		{
			if (IsSecurityErrorEnabled)
				_log.Logger.Log(ThisType, Level.SecurityError, message, exception);
		}

		public void SecurityError(object message)
		{
			if (IsSecurityErrorEnabled)
				_log.Logger.Log(ThisType, Level.SecurityError, message, null);
		}

		public void SecurityErrorFormat(IFormatProvider provider, string format, params object[] args)
		{
			if (IsSecurityErrorEnabled)
				_log.Logger.Log(ThisType, Level.SecurityError,
					new SystemStringFormat(provider, format, args),
					null);
		}

		public void SecurityErrorFormat(string format, params object[] args)
		{
			if (IsSecurityErrorEnabled)
				_log.Logger.Log(ThisType, Level.SecurityError,
					new SystemStringFormat(CultureInfo.InvariantCulture, format, args),
					null);
		}
		#endregion

		#region SpamWarning
		public void SpamWarning(object message, Exception exception)
		{
			if (IsSpamWarningEnabled)
				_log.Logger.Log(ThisType, Level.SpamWarning, message, exception);
		}

		public void SpamWarning(object message)
		{
			if (IsSpamWarningEnabled)
				_log.Logger.Log(ThisType, Level.SpamWarning, message, null);
		}

		public void SpamWarningFormat(IFormatProvider provider, string format, params object[] args)
		{
			if (IsSpamWarningEnabled)
				_log.Logger.Log(ThisType, Level.SpamWarning,
					new SystemStringFormat(provider, format, args),
					null);
		}

		public void SpamWarningFormat(string format, params object[] args)
		{
			if (IsSpamWarningEnabled)
				_log.Logger.Log(ThisType, Level.SpamWarning,
					new SystemStringFormat(CultureInfo.InvariantCulture, format, args),
					null);
		}
		#endregion

		#region SpamError
		public void SpamError(object message, Exception exception)
		{
			if (IsSpamErrorEnabled)
				_log.Logger.Log(ThisType, Level.SpamError, message, exception);
		}

		public void SpamError(object message)
		{
			if (IsSpamErrorEnabled)
				_log.Logger.Log(ThisType, Level.SpamError, message, null);
		}

		public void SpamErrorFormat(IFormatProvider provider, string format, params object[] args)
		{
			if (IsSpamErrorEnabled)
				_log.Logger.Log(ThisType, Level.SpamError,
					new SystemStringFormat(provider, format, args),
					null);
		}

		public void SpamErrorFormat(string format, params object[] args)
		{
			if (IsSpamErrorEnabled)
				_log.Logger.Log(ThisType, Level.SpamError,
					new SystemStringFormat(CultureInfo.InvariantCulture, format, args),
					null);
		}
		#endregion

		#region Debug
		//Due to the much higher possibilty of very high rate logging at the debug level and wanting to 
		//avoid any possibilty of adding exceptions to debugging attempts, these are wrapped in try/catches. Thread pool
		//enqueues have OOM as a documented exception, and of course who knows what undocumented exceptions might be thrown
		public void Debug(object message, Exception exception)
		{
			if (!IsDebugEnabled) return;

			try
			{
				ExceptionArgs eargs = new ExceptionArgs(message, exception);
				
				if(UseSyncLogging)
					DoDebugException(eargs);
				else
					ThreadPool.UnsafeQueueUserWorkItem(DoDebugException, eargs);
			}
			catch (Exception e)
			{
				_log.ErrorFormat("Could not enqueue debug log write for message \"{0}: {1}\" due to exception {2}", message, exception, e);
			}
		}

		internal void DoDebugException(object state)
		{
			ExceptionArgs args = state as ExceptionArgs;
			if (args != null)
				_log.Debug(args.Message, args.ThisException);
		}

		public void Debug(object message)
		{
			if (!IsDebugEnabled) return;

			try
			{
				if (UseSyncLogging)
					DoDebug(message);
				else
					ThreadPool.UnsafeQueueUserWorkItem(DoDebug, message);
			}
			catch (Exception e)
			{
				_log.ErrorFormat("Could not enqueue debug log write for message \"{0}\" due to exception {1}", message, e);
			}
		}
		
		internal void DoDebug(object state)
		{
			_log.Debug(state);
		}
		
		public void DebugFormat(IFormatProvider provider, string format, params object[] args)
		{
			if (!IsDebugEnabled) return;

			try
			{
				FormatArgs fargs = new FormatArgs(provider, format, args);

				if(UseSyncLogging)
					DoDebugFormat(fargs);
				else
					ThreadPool.UnsafeQueueUserWorkItem(DoDebugFormat, fargs);
			}
			catch (Exception e)
			{
				_log.ErrorFormat("Could not enqueue debug log write for message \"{0}\" due to exception {1}", string.Format(provider, format, args), e);
			}

		}

		public void DebugFormat(string format, params object[] args)
		{
			if (!IsDebugEnabled) return;

			try
			{
				FormatArgs fargs = new FormatArgs(null, format, args);
				
				if(UseSyncLogging)
					DoDebugFormat(fargs);
				else
					ThreadPool.UnsafeQueueUserWorkItem(DoDebugFormat, fargs);
			}
			catch (Exception e)
			{
				_log.ErrorFormat("Could not enqueue debug log write for message \"{0}\" due to exception {1}", string.Format(format, args), e);
			}
		}

		internal void DoDebugFormat(object state)
		{
			FormatArgs args = state as FormatArgs;
			if (args != null)
			{
				if (args.Provider != null)
				{
					_log.DebugFormat(args.Provider, args.Format, args.Args);
				}
				else
				{
					_log.DebugFormat(args.Format, args.Args);
				}
			}			
		}

		#endregion

		#region Info

		public void Info(object message, Exception exception)
		{
			if (!IsInfoEnabled) return;

			ExceptionArgs eargs = new ExceptionArgs(message, exception);
			
			if(UseSyncLogging)
				DoInfoException(eargs);
			else
				ThreadPool.UnsafeQueueUserWorkItem(DoInfoException, eargs);
		}

		internal void DoInfoException(object state)
		{
			ExceptionArgs args = state as ExceptionArgs;
			if(args != null)
				_log.Info(args.Message, args.ThisException);
		}

		public void Info(object message)
		{
			if (!IsInfoEnabled) return;

			if(UseSyncLogging)
				DoInfo(message);
			else
				ThreadPool.UnsafeQueueUserWorkItem(DoInfo, message);
		}

		internal void DoInfo(object state)
		{
			_log.Info(state);
		}

		public void InfoFormat(IFormatProvider provider, string format, params object[] args)
		{
			if (!IsInfoEnabled) return;

			FormatArgs fargs = new FormatArgs(provider, format, args);
			
			if(UseSyncLogging)
				DoInfoFormat(fargs);
			else
				ThreadPool.UnsafeQueueUserWorkItem(DoInfoFormat, fargs);
		}

		public void InfoFormat(string format, params object[] args)
		{
			if (!IsInfoEnabled) return;
			FormatArgs fargs = new FormatArgs(null, format, args);

			if(UseSyncLogging)
				DoInfoFormat(fargs);
			else
				ThreadPool.UnsafeQueueUserWorkItem(DoInfoFormat, fargs);
		}
		
		internal void DoInfoFormat(object state)
		{
			FormatArgs args = state as FormatArgs;
			if (args != null)
			{
				if (args.Provider != null)
				{
					_log.InfoFormat(args.Provider, args.Format, args.Args);
				}
				else
				{
					_log.InfoFormat(args.Format, args.Args);
				}
			}
		}

		#endregion

		#region Warn

		public void Warn(object message, Exception exception)
		{
			if (!IsWarnEnabled) return;

			ExceptionArgs eargs = new ExceptionArgs(message, exception);
			
			if(UseSyncLogging)
				DoWarnException(eargs);
			else
				ThreadPool.UnsafeQueueUserWorkItem(DoWarnException, eargs);
		}

		internal void DoWarnException(object state)
		{
			ExceptionArgs args = state as ExceptionArgs;
			if (args != null)
				_log.Warn(args.Message, args.ThisException);
		}

		public void Warn(object message)
		{
			if (!IsWarnEnabled) return;

			if(UseSyncLogging)
				DoWarn(message);
			else
				ThreadPool.UnsafeQueueUserWorkItem(DoWarn, message);
		}
		
		internal void DoWarn(object state)
		{
			_log.Warn(state);
		}

		public void WarnFormat(IFormatProvider provider, string format, params object[] args)
		{
			if (!IsWarnEnabled) return;

			FormatArgs fargs = new FormatArgs(provider, format, args);

			if(UseSyncLogging)
				DoWarnFormat(fargs);
			else
				ThreadPool.UnsafeQueueUserWorkItem(DoWarnFormat, fargs);
		}

		public void WarnFormat(string format, params object[] args)
		{
			if (!IsWarnEnabled) return;

			FormatArgs fargs = new FormatArgs(null, format, args);

			if (UseSyncLogging)
				DoWarnFormat(fargs);
			else
				ThreadPool.UnsafeQueueUserWorkItem(DoWarnFormat, fargs);
		}

		internal void DoWarnFormat(object state)
		{
			FormatArgs args = state as FormatArgs;
			if (args != null)
			{
				if (args.Provider != null)
				{
					_log.WarnFormat(args.Provider, args.Format, args.Args);
				}
				else
				{
					_log.WarnFormat(args.Format, args.Args);
				}
			}
		}

		#endregion

		#region Error

		public void Error(object message, Exception exception)
		{
			if (!IsErrorEnabled) return;

			ExceptionArgs eargs = new ExceptionArgs(message, exception);

			if(UseSyncLogging)
				DoErrorException(eargs);
			else
				ThreadPool.UnsafeQueueUserWorkItem(DoErrorException, eargs);
		}

		internal void DoErrorException(object state)
		{
			ExceptionArgs args = state as ExceptionArgs;
			if (args != null)
				_log.Error(args.Message, args.ThisException);
		}

		public void Error(object message)
		{
			if (!IsErrorEnabled) return;
			
			if(UseSyncLogging)
				DoError(message);
			else
				ThreadPool.UnsafeQueueUserWorkItem(DoError, message);
		}

		public void Error(Exception exception)
		{
			if (!IsErrorEnabled) return;

			Error(null, exception);
		}

		internal void DoError(object state)
		{
			_log.Error(state);
		}

		public void ErrorFormat(IFormatProvider provider, string format, params object[] args)
		{
			if (!IsErrorEnabled) return;

			FormatArgs fargs = new FormatArgs(provider, format, args);
			
			if(UseSyncLogging)
				DoErrorFormat(fargs);
			else
				ThreadPool.UnsafeQueueUserWorkItem(DoErrorFormat, fargs);
		}

		public void ErrorFormat(string format, params object[] args)
		{
			if (!IsErrorEnabled) return;

			FormatArgs fargs = new FormatArgs(null, format, args);

			if (UseSyncLogging)
				DoErrorFormat(fargs);
			else
				ThreadPool.UnsafeQueueUserWorkItem(DoErrorFormat, fargs);
		}

		internal void DoErrorFormat(object state)
		{
			FormatArgs args = state as FormatArgs;
			if (args != null)
			{
				if (args.Provider != null)
				{
					_log.ErrorFormat(args.Provider, args.Format, args.Args);
				}
				else
				{
					_log.ErrorFormat(args.Format, args.Args);
				}
			}
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
			if(_log.IsDebugEnabled)
				_log.DebugFormat(provider, string.Format("Page: {2}, MethodName: {1}, {0}", format, GetDebugCallingMethod(), GetDebugCallingPage()), args);
		}

		public void MethodDebugFormat(string format, params object[] args)
		{
			if (_log.IsDebugEnabled)
				_log.DebugFormat(string.Format("Page: {2}, MethodName: {1}, {0}", format, GetDebugCallingMethod(), GetDebugCallingPage()), args);
		}

		public void MethodDebug(string message)
		{
			if (_log.IsDebugEnabled)
				_log.Debug(string.Format("Page: {2}, MethodName: {1}, {0}", message, GetDebugCallingMethod(), GetDebugCallingPage()));
		}

		// With Log Prefix

		public void MethodDebugFormat(IFormatProvider provider, string logPrefix, string format, params object[] args)
		{
			if (_log.IsDebugEnabled)
				_log.DebugFormat(provider, string.Format("{0}| {1} , MethodName: {2} , Page: {3}", logPrefix, format, GetDebugCallingMethod(), GetDebugCallingPage()), args);
		}

		public void MethodDebugFormat(string logPrefix, string format, params object[] args)
		{
			if (_log.IsDebugEnabled)
				_log.DebugFormat(string.Format("{0}| Page: {3}, MethodName: {2} , {1}", logPrefix, format, GetDebugCallingMethod(), GetDebugCallingPage()), args);
		}

		public void MethodDebug(string logPrefix, string message)
		{
			if (_log.IsDebugEnabled)
				_log.Debug(string.Format("{0}| Page: {3}, MethodName: {2}, {1}", logPrefix, message, GetDebugCallingMethod(), GetDebugCallingPage()));
		}

		// With Log Prefix and delegate to add custom logging info
		public void MethodDebugFormat(string logPrefix, LogOutputMapper customLogOutput, string format, params object[] args)
		{
			if (_log.IsDebugEnabled)
			{
				StringBuilder additionalLogData = new StringBuilder();
				if (customLogOutput != null)
					customLogOutput(additionalLogData);

				_log.DebugFormat(string.Format("{0}| Page: {3}, MethodName: {2}, {1}, {4}", logPrefix, format, GetDebugCallingMethod(), GetDebugCallingPage(), additionalLogData), args);
			}
		}

		/// <summary>
		/// Returns calling method name using current stack 
		/// and assuming that first non Logging method is the parent
		/// </summary>
		/// <returns>Method Name</returns>
		private static string GetDebugCallingMethod()
		{
			// Walk up the stack to get parent method
			StackTrace st = new StackTrace();

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

			return "Unknown Method";
		}

		public string CurrentStackTrace()
		{
			StringBuilder sb = new StringBuilder();
			// Walk up the stack to return everything
			StackTrace st = new StackTrace();

			for (int i = 0; i < st.FrameCount; i++)
			{
				StackFrame sf = st.GetFrame(i);
				MethodBase method = sf.GetMethod();
				if (method != null)
				{
					Type declaringType = method.DeclaringType;
					//If the MemberInfo object is a global member, (that is, it was obtained from Module.GetMethods(), 
					//which returns global methods on a module), then the returned DeclaringType will be null reference
					if (declaringType == null)
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

			return sb.ToString();
		}

		/// <summary>
		/// Returns ASP.NET method name which called current method. 
		/// Uses call stack and assumes that all methods starting with 'ASP.' are the ASP.NET page methods
		/// </summary>
		/// <returns>Class Name of the ASP.NET page</returns>
		private static string GetDebugCallingPage()
		{
			// Walk up the stack to get calling method which is compiled ASP.Net page
			StackTrace st = new StackTrace();

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

			return "Unknown Page";
		}

		#endregion
		
		#region ILogMore methods

		[Obsolete("More is not a supported level.")]
		public void MoreInfo(params object[] traceMessages)
		{
			if (_log.IsInfoEnabled && null!=traceMessages) _log.Info(string.Concat(traceMessages));
		}

		[Obsolete("More is not a supported level.")]
		public void MoreError(params object[] traceMessages)
		{
			if (_log.IsErrorEnabled && null != traceMessages) _log.Error(string.Concat(traceMessages));
		}

		[Obsolete("More is not a supported level.")]
		public void MoreWarn(params object[] traceMessages)
		{
			if (_log.IsWarnEnabled && null != traceMessages) _log.Warn(string.Concat(traceMessages));
		}

		[Obsolete("More is not a supported level.")]
		public void MoreDebug(params object[] traceMessages)
		{
			if (_log.IsDebugEnabled && null != traceMessages) _log.Debug(string.Concat(traceMessages));
		}

		[Obsolete("Fatal is not a supported level.")]
		public void MoreFatal(params object[] traceMessages)
		{
			if (_log.IsErrorEnabled && null != traceMessages) _log.Error(string.Concat(traceMessages));
		}

		/*
		 * These previously relied on "IsMoreEnabled", which was always false. These methods will eventually be removed 
		 */
		public bool IsMoreDebugEnabled
		{
			get { return false; }
		}

		public bool IsMoreInfoEnabled
		{
			get { return false; }
		}

		public bool IsMoreErrorEnabled
		{
			get { return false; }
		}

		public bool IsMoreWarnEnabled
		{
			get { return false; }
		}

		public bool IsMoreFatalEnabled
		{
			get { return false; }
		}

		#endregion

		/// <summary>
		/// Method is to be used by unit tests to get TraceLogging.  If log4net configuration file
		///	is not found, this method is called internally to enable logging to trace by default.
		/// </summary>
		public static void ConfigureForUnitTesting()
		{
			if (!_unitTestingConfigured)
			{
				_unitTestingConfigured = true;
				log4net.Appender.TraceAppender traceAppender = new log4net.Appender.TraceAppender();
				traceAppender.Layout = new log4net.Layout.PatternLayout("%date %-5level - %message%newline");
				traceAppender.ImmediateFlush = true;
				log4net.Config.BasicConfigurator.Configure(traceAppender);
			}
		}

		private static bool _unitTestingConfigured;

		#region Args Classes for Async Calls
		
		internal class ExceptionArgs
		{
			internal ExceptionArgs(object message, Exception exception)
			{
				Message = message;
				ThisException = exception;
			}

			internal object Message;
			internal Exception ThisException;
		}
		
		internal class FormatArgs
		{
			internal FormatArgs(IFormatProvider provider, string format, params object[] args)
			{
				Provider = provider;
				Format = format;
				Args = args;
			}

			internal IFormatProvider Provider;
			internal string Format;
			internal object[] Args;
		}

		#endregion

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
			ThreadPool.UnsafeQueueUserWorkItem(DoWarnException, new ExceptionArgs(policyName, exc));
			return true;
		}				

		#endregion
	
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

	}
}
