using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace MySpace.Common.HelperObjects
{
    public static class StringUtility
    {
        public static string ConvertIPAddressToString(byte[] ipAddress)
        {
            if (ipAddress == null || ipAddress[0] == 0) // If ipAddress was not determined.
                return string.Empty;

            string result = ipAddress[0].ToString();

            for (int i = 1; i < 4; ++i)
            {
                result = result + '.' + ipAddress[i];
            }
            return result;
        }
        /// <summary>
        /// Indicates whether the specified <see cref="String"/> object is null or an <see cref="string.Empty"/> string.
        /// </summary>
        /// <param name="value">A <see cref="String"/> reference.</param>
        /// <returns><see langword="true"/> if the <paramref name="value"/> parameter is <see langword="null"/> or an empty string (""); otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// Note: this uses the <see cref="MethodImplAttribute"/> setting it to <see cref="MethodImplOptions.NoInlining"/> 
        /// to workaround the JIT bug noted at: http://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=113102
		/// Note: UPDATE: This bug was fixed in .NET 2.0 SP1
		/// 940900  (http://support.microsoft.com/kb/940900/ ) FIX: You receive the NullReferenceException exception when you call the String.IsNullOrEmpty function in an application that is built on the .NET Framework 2.0
        /// </remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsNullOrEmpty(string value)
        {
            if (value != null)
            {
                return (value.Length == 0);
            }
            return true;
        }

        #region CleanString
        /// <summary>
        /// trims and returns input string.  If the input is null or empty string, returns null.
        /// </summary>
        /// <param name="input">possibly unclean string or null.</param>
        /// <returns>trimmed string or null if input is null.</returns>
        public static string CleanString(string input)
        {
            string value = null;
            if (!IsNullOrEmpty(input))
            {
                string trimmed = input.Trim();
                if (trimmed.Length > 0)
                    value = trimmed;
            }
            return value;
        }
        #endregion

        #region ReverseString
        /// <summary>
        /// Reverses all characters in input string.
        /// </summary>
        /// <param name="input">string to be reversed.</param>
        /// <returns>reversed string, or input if null or empty.</returns>
        public static string ReverseString(string input)
        {
            if (!IsNullOrEmpty(input))
            {
                char[] chars = input.ToCharArray();
                Array.Reverse(chars);
                return new String(chars);
            }
            else
                return input;
        }
        #endregion

        #region GetDelimitedString
        /// <summary>
        /// Creates a comma ", " delimited string out of an IEnumerable list of T
        /// </summary>
        /// <typeparam name="T">Type contained in IEnumerable&lt;T&gt;</typeparam>
        /// <param name="list">IEnumerable list</param>
        /// <returns>comma delimited string of the items in the list.</returns>
        public static string GetDelimitedString<T>(IEnumerable<T> list)
        {
            return GetDelimitedString<T>(list, null);
        }

        /// <summary>
        /// Creates a delimited string out of an IEnumerable list of T
        /// </summary>
        /// <typeparam name="T">Type contained in IEnumerable&lt;T&gt;</typeparam>
        /// <param name="list">IEnumerable list</param>
        /// <param name="delimiter">delimiter string, defaults to ", ".</param>
        /// <returns>delimited string of the items in the list.</returns>
        public static string GetDelimitedString<T>(IEnumerable<T> list, string delimiter)
        {
            if (list != null)
            {
                delimiter = IsNullOrEmpty(delimiter) ? ", " : delimiter;
                StringBuilder builder = new StringBuilder();
                foreach (T item in list)
                    builder.Append(item.ToString()).Append(delimiter);

                if (builder.Length > 2)
                    return builder.ToString(0, builder.Length - 2);
            }
            return string.Empty;
        }
        #endregion

 
        /// <summary>
        /// A safe mode implementation of the 32 bit .Net framework's string.GetHashCode() method. 
        /// Because this uses bitconverter instead of an int* for the algorithm, the results are different, but they are
        /// equally well distributed. 
        /// This is also slower, but it is guaranteed to be the same no matter where it is run, unlike String.GetHashCode() 
        /// which varies by implementation.
        /// This is slow because I was dumb when I implemented it. Use GetStringHashFast unless you need to stay consistent
        /// with results from this method.
        /// </summary>
        public static Int32 GetStringHash(string name)
        {            
            byte[] bytes = StringUtility.GetStringBytes(name);
			
            if (bytes == null || bytes.Length == 0)
            {
                return 0;
            }

            char[] chars = name.ToCharArray();
            
            Int32 initialResult = 352654597;
            Int32 otherNumberToMungeWith = initialResult;
            Int32 power1, power2;

            for (int i = chars.Length, j = 0; i > 0; i -= 4, j++)
            {
                GetPowers(bytes, j, out power1, out power2);
                initialResult = (((initialResult << 5) + initialResult) + (initialResult >> 27)) ^ power1;
                if (i <= 2)
                {
                    break;
                }
                otherNumberToMungeWith = 
                    (((otherNumberToMungeWith << 5) + otherNumberToMungeWith) + (otherNumberToMungeWith >> 27)) ^ power2;
            }

            return (initialResult + (otherNumberToMungeWith * 1566083941));
        }

		/// <summary>
		/// A safe mode implementation of the 32 bit .Net framework's string.GetHashCode() method. 
		/// Because this uses bitconverter instead of an int* for the algorithm, the results are different, but they are
		/// equally well distributed. 
		/// This is also slower, but it is guaranteed to be the same no matter where it is run, unlike String.GetHashCode() 
		/// which varies by implementation.
		/// This version is much faster than GetStringHash, but it produes different results, so it cannot be used as a drop in replacement.
		/// </summary>
		public static unsafe Int32 GetStringHashFast(string name)
		{
			if(string.IsNullOrEmpty(name))
			{
				return 0;
			}
			char[] charray = name.ToCharArray();
			fixed (char* str = charray)
			{
				char* chPtr = str;
				int num = 0x15051505;
				int num2 = num;
				int* numPtr = (int*)chPtr;
				for (int i = charray.Length; i > 0; i -= 4)
				{
					num = (((num << 5) + num) + (num >> 0x1b)) ^ numPtr[0];
					if (i <= 2)
					{
						break;
					}
					num2 = (((num2 << 5) + num2) + (num2 >> 0x1b)) ^ numPtr[1];
					numPtr += 2;
				}
				return (num + (num2 * 0x5d588b65));
			}
		}

        private static void GetPowers(byte[] bytes, Int32 index, out Int32 power1, out Int32 power2)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException("bytes");
            }

            Int32 firstOffset = (index * 8);
            Int32 secondOffset = firstOffset + 4;
            if (bytes.Length >= (firstOffset + 4))
            {
                power1 = BitConverter.ToInt32(bytes, firstOffset);
            }
            else if (bytes.Length >= (firstOffset + 2))
            {
                power1 = (Int32)BitConverter.ToInt16(bytes, firstOffset);
            }
            else if (bytes.Length > (firstOffset))
            {
                power1 = (Int32)bytes[firstOffset];
            }
            else
            {
                power1 = 0;
            }

            if (bytes.Length >= (secondOffset + 4))
            {
                power2 = BitConverter.ToInt32(bytes, secondOffset);
            }
            else if (bytes.Length >= (secondOffset + 2))
            {
                power2 = (Int32)BitConverter.ToInt16(bytes, secondOffset);
            }
            else if (bytes.Length > (secondOffset))
            {
                power2 = (Int32)bytes[secondOffset];
            }
            else
            {
                power2 = 0;
            }
        }

        private static readonly Encoding stringEncoder = new UTF8Encoding(false, true); //same as the default for a BinaryWriter
        /// <summary>
        /// Returns the bytes produced by a default UTF8 encoding of the string. 
        /// This uses the same parameters for encoding as a BinaryWriter.
        /// </summary>        
        public static byte[] GetStringBytes(string str)
        {
            byte[] extendedKeyBytes = null;
            if (str != null)
            {
                extendedKeyBytes = stringEncoder.GetBytes(str);
            }
            return extendedKeyBytes;
        }
    }

}