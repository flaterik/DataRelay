using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.Common
{
    /// <summary>
    /// Common argument checking calls
    /// </summary>
    public static class ArgumentHelper
    {
        #region AssertNotNull
		/// <summary>
		/// Throws <see cref="ArgumentNullException"/> exception if any of the values are null 
		/// </summary>
		/// <typeparam name="T">Any type</typeparam>
		/// <param name="values">List of values to check</param>
		/// <exception cref="ArgumentNullException"/>
		public static void AssertValuesNotNull<T>(params T[] values) where T : class
		{
			foreach(T value in values)
				if (value == null)
					throw new ArgumentNullException();
		}

        /// <summary>
        /// Throws <see cref="ArgumentNullException"/> exception if any of values are null 
        /// </summary>
        /// <typeparam name="T">Any type</typeparam>
		/// <param name="values">List of values to check</param>
        /// <param name="paramName">Name of parameter(s)</param>
        /// <exception cref="ArgumentNullException"/>
		public static void AssertValuesNotNull<T>(string paramName, params T[] values) where T : class
        {
			foreach (T value in values)
				if (value == null)
					throw new ArgumentNullException("Argument name: " + paramName);
        }
		public static void AssertNotNull<T>(T value, string paramName) where T : class
		{
			AssertValuesNotNull<T>(paramName, value);
		}

        /// <summary>
        /// Throws <see cref="ArgumentNullException"/> exception if any of the values are null
        /// </summary>
        /// <typeparam name="T">Any type</typeparam>
        /// <param name="values">List of values to check</param>
        /// <exception cref="ArgumentNullException"/>
		public static void AssertValuesNotNull<T>(params Nullable<T>[] values) where T : struct
        {
			foreach (Nullable<T> value in values)
				if (!value.HasValue)
					throw new ArgumentNullException();
        }
		public static void AssertNotNull<T>(T value) where T : class
		{
			AssertValuesNotNull<T>(value);
		}

        /// <summary>
        /// Throws <see cref="ArgumentNullException"/> exception if any of the values are null 
        /// </summary>
        /// <typeparam name="T">Any type</typeparam>
		/// <param name="values">List of values to check</param>
        /// <param name="paramName">Name of parameter</param>
        /// <exception cref="ArgumentNullException"/>
		public static void AssertValuesNotNull<T>(string paramName, params Nullable<T>[] values) where T : struct
        {
			foreach (Nullable<T> value in values)
				if (!value.HasValue)
					throw new ArgumentNullException("Argument name: " + paramName);
        }
		public static void AssertNotNull<T>(Nullable<T> value, string paramName) where T : struct
		{
			AssertValuesNotNull<T>(paramName, value);
		}
		public static void AssertNotNull<T>(Nullable<T> value) where T : struct
		{
			AssertValuesNotNull<T>(value);
		}
        #endregion

        #region AssertNotEmpty
        /// <summary>
        /// Throws <see cref="ArgumentOutOfRangeException"/> exception if any of the strings are empty or null
        /// </summary>
        /// <param name="values">List of values to check</param>
        /// <exception cref="ArgumentOutOfRangeException"/>
        public static void AssertValuesNotEmpty(params string[] values)
        {
			foreach (string value in values)
			{
				if (value == null)
					throw new ArgumentNullException("Argument is null.");
				if (value.Length == 0)
					throw new ArgumentOutOfRangeException("String is empty.");
			}
        }

        /// <summary>
        /// Throws <see cref="ArgumentOutOfRangeException"/> exception if any of the strings are empty or null
        /// </summary>
		/// <param name="values">List of values to check</param>
        /// <param name="paramName">Name of parameter</param>
        /// <exception cref="ArgumentOutOfRangeException"/>
		public static void AssertValuesNotEmpty(string paramName, params string[] values)
        {
			foreach (string value in values)
			{
				if (value == null)
					throw new ArgumentNullException("Argument name: " + paramName + ".");
				if (value.Length == 0)
					throw new ArgumentOutOfRangeException("String is empty. Argument name: " + paramName + ".");
			}
        }
		public static void AssertNotEmpty(string value, string paramName) 
		{
			AssertValuesNotEmpty(paramName, value);
		}
		public static void AssertNotEmpty(string value)
		{
			AssertValuesNotEmpty(value);
		}
        #endregion

        #region AssertPositive
        /// <summary>
        /// Throws <see cref="ArgumentOutOfRangeException"/> exception if any of the values are not positive
        /// </summary>
		/// <param name="values">List of values to check</param>
        /// <exception cref="ArgumentOutOfRangeException"/>
        public static void AssertValuesPositive(params int[] values)
        {
			foreach(int value in values)
				if (value <= 0)
					throw new ArgumentOutOfRangeException("Argument not positive. Value: (" + value + ").");
        }

        /// <summary>
		/// Throws <see cref="ArgumentOutOfRangeException"/> exception if any of the values are not positive
        /// </summary>
		/// <param name="values">List of values to check</param>
        /// <param name="paramName">Name of parameter</param>
        /// <exception cref="ArgumentOutOfRangeException"/>
		public static void AssertValuesPositive(string paramName, params int[] values)
        {
			foreach (int value in values)
				if (value <= 0)
					throw new ArgumentOutOfRangeException("Argument not positive. Argument name: " + paramName + "; Value: " + value + ".");
        }

		public static void AssertPositive(int value, string paramName) 
		{
			AssertValuesPositive(paramName, value);
		}

		public static void AssertPositive(int value)
		{
			AssertValuesPositive(value);
		}
        #endregion

		#region AssertInRange
		public static void AssertInRange(string paramName, int value, int rangeBegin, int rangeEnd)
		{
			if ( value < rangeBegin || value > rangeEnd )
				throw new ArgumentOutOfRangeException(paramName, 
					string.Format("{0} must be within the range {1} - {2}.  The value given was {3}.", paramName, rangeBegin, rangeEnd, value));
		}
		#endregion

		#region AssertIsTrue and IsFalse
		public static void AssertIsTrue(bool assertion, string message)
		{
			if (!assertion)
				throw new ArgumentException(message);
		}
		public static void AssertIsTrue(bool assertion, string messageTemplate, params object[] parameters)
		{
			if (!assertion)
				throw new ArgumentException(string.Format(messageTemplate, parameters));
		}
		public static void AssertIsFalse(bool assertion, string message)
		{
			AssertIsTrue(!assertion, message);
		}
		public static void AssertIsFalse(bool assertion, string messageTemplate, params object[] parameters)
		{
			AssertIsTrue(!assertion, messageTemplate, parameters);
		}
		#endregion
	}

    /// <summary>
    /// Common result checking calls
    /// </summary>
    public static class OutputResultHelper
    {
        #region AssertNotNull
        /// <summary>
        /// Throws <see cref="InvalidOperationException"/> exception if any of the values are null 
        /// </summary>
        /// <typeparam name="T">Any type</typeparam>
        /// <param name="values">List of values to check</param>
        /// <exception cref="InvalidOperationException"/>
        public static void AssertValuesNotNull<T>(params T[] values) where T : class
        {
            foreach (T value in values)
                if (value == null)
                    throw new InvalidOperationException("Some or all values are null. Type: " + typeof(T).FullName);
        }

        /// <summary>
        /// Throws <see cref="InvalidOperationException"/> exception if any of values are null 
        /// </summary>
        /// <typeparam name="T">Any type</typeparam>
        /// <param name="values">List of values to check</param>
        /// <param name="paramName">Name of parameter(s)</param>
        /// <exception cref="InvalidOperationException"/>
        public static void AssertValuesNotNull<T>(string paramName, params T[] values) where T : class
        {
            foreach (T value in values)
                if (value == null)
                    throw new InvalidOperationException("Some or all values are null. Name: " + paramName + "Type: " + typeof(T).FullName);
        }

        public static void AssertNotNull<T>(T value, string paramName) where T : class
        {
            AssertValuesNotNull<T>(paramName, value);
        }
        #endregion

        #region AssertInRange
        public static void AssertInRange(string paramName, int value, int rangeBegin, int rangeEnd)
        {
            if (value < rangeBegin || value > rangeEnd)
                throw new InvalidOperationException(
                    string.Format("Value is null: Parameter '{0}' must be within the range {1} - {2}.  The value given was {3}.", paramName, rangeBegin, rangeEnd, value));
        }
        #endregion
    }

}
