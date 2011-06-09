using System.Security;
using System;
using MySpace.Logging;

namespace MySpace.Common.IO
{
	/// <summary>
	/// For use indicating the currently executing processor platform
	/// </summary>
	public enum Platform
	{
		/// <summary>Win32 platform</summary>
		Win32,
		/// <summary>x64 platform</summary>
		X64
	}

	public static class ManagedZLibWrapper
	{
		private static readonly MySpace.Logging.LogWrapper log = new LogWrapper();
		public static readonly Platform CurrentPlatform;
	
		[SuppressUnmanagedCodeSecurity]
		public delegate byte[] CompressDelegate(byte[] data, int compressionLevel, bool useHeader);
		[SuppressUnmanagedCodeSecurity]
		public delegate byte[] DecompressDelegate(byte[] data, bool useHeader);

		public static CompressDelegate Compress;
		public static DecompressDelegate Decompress;
		
		static ManagedZLibWrapper()
		{
			
			if (IntPtr.Size == 4)
			{
				CurrentPlatform = Platform.Win32;
				log.Debug("Int Pointer is 4 bytes, using Win32");
			}
			else if (IntPtr.Size == 8)
			{
				CurrentPlatform = Platform.X64;
				log.Debug("Int Pointer is 8 bytes, using x64");
			}
			else
			{
				string errorMessage = string.Format("Int pointer is {0}, you must be running this code in the future!", IntPtr.Size);
				log.ErrorFormat(errorMessage);
				throw new ApplicationException(errorMessage);
			}

			try
			{
				switch (CurrentPlatform)
				{
					case Platform.Win32:
						SetWin32();
						break;
					case Platform.X64:
						SetX64();
						break;
				}
			}
			catch (System.IO.FileNotFoundException fnfe)
			{
				log.ErrorFormat("Could not load ManagedZLib because the file {0} could not be found. Please ensure that MySpace.ManagedZLib.*.exe has been deployed.", fnfe.FileName);
				throw;
			}
			catch (Exception e)
			{
				log.ErrorFormat("Exception loading ManagedZLib. Please ensure MySpace.ManagedZLib.*.exe has been deployed: {0}", e);
				throw;
			}
		}

		private static void SetX64()
		{
			Compress = MySpace.x64.ManagedZLib.Compress;
			Decompress = MySpace.x64.ManagedZLib.Decompress;
		}

		private static void SetWin32()
		{
			Compress = MySpace.Win32.ManagedZLib.Compress;
			Decompress = MySpace.Win32.ManagedZLib.Decompress;
		}

	}
}
