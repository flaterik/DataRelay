using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace MySpace.Common.UnitTests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Reflection;
	using System.Reflection.Emit;
	using System.Text;
	using Microsoft.VisualStudio.TestTools.UnitTesting;
	using MySpace.Common;
	using MySpace.Common.IO;
	using System.Threading;
using System.Xml;

	/// <summary>
	/// <para>Provides a number of custom assertions for unit tests.</para>
	/// </summary>
	public static class MSAssert
	{
		/// <summary>
		/// 	<para>Asserts that the specified item is contained in the specified <see cref="IEnumerable{T}"/> instance.</para>
		/// </summary>
		/// <typeparam name="T">
		/// 	<para>The type of item to look for in the collection.</para>
		/// </typeparam>
		/// <param name="item">
		/// <para> The item to find in the collection. </para>
		/// </param>
		/// <param name="collection">
		/// 	<para>The <see cref="IEnumerable{T}"/> to search.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="item"/> is <see langword="null"/>.</para>
		/// 	<para>-or-</para>
		/// 	<para>The argument <paramref name="collection"/> is <see langword="null"/>.</para>
		/// </exception>
		public static void IsInCollection<T>(T item, IEnumerable<T> collection)
		{
			if (item == null)
			{
				throw new ArgumentNullException("item");
			}

			if (collection == null)
			{
				throw new ArgumentNullException("collection");
			}

			foreach (T collectionItem in collection)
			{
				if (Object.Equals(collectionItem, item))
				{
					return;
				}
			}

			Assert.Fail("The item '{0}' is not present in the collection", item.ToString());
		}

		/// <summary>
		/// 	<para>Asserts that the Actual Value is within a range of values MaxValue and MinValue".</para>
		/// </summary>
		/// <typeparam name="T">
		/// 	<para>The type of values.</para>
		/// </typeparam>
		/// <param name="minValue">
		/// 	<para>The Minimum possible Value, of the range, that the Actual Value can have.</para>
		/// </param>
		/// <param name="maxValue">
		/// 	<para>The Maximum possible Value, of the range, that the Actual Value can have.</para>
		/// </param>
		/// <param name="actualValue">
		/// 	<para>The Value to find within the range.</para>
		/// </param>		
		/// <exception cref="ArgumentOutOfRangeException">
		///		<para>The <paramref name="minValue"/> is more than <paramref name="maxValue"/>.</para>			
		/// </exception>
		/// <exception cref="AssertFailedException">
		///		<para>The Actual Value is less than Min Value or more than Max Value</para>
		///		<para>ActualValue does not lie in the range of Min Value and Max Value.</para>		
		/// </exception>
		public static void IsInRange<T>(T minValue, T maxValue, T actualValue) where T : IComparable<T>
		{
			if (minValue.CompareTo(maxValue) > 0)
			{
				throw new ArgumentOutOfRangeException(String.Format("Min Value '{0}' cannot be greater than Max Value '{1}'", minValue, maxValue));
			}

			Assert.IsTrue(
				actualValue.CompareTo(minValue) >= 0 && (actualValue.CompareTo(maxValue) <= 0),
				String.Format("Actual Value '{0}' is not in limits of Min value '{1}' and Max Value '{2}", actualValue, minValue, maxValue));
		}

		/// <summary>
		/// 	<para>Asserts that the Actual Value is roughly equal to the Expected Value with a possible deviation of 
		/// 	Max Deviation.
		/// 	</para>
		/// </summary>
		/// <remarks>Currently implemented only for <see cref="Int32"/>, <see cref="Int64"/> (long), <see cref="Double"/>, <see cref="Single"/> (float)</remarks>
		/// <typeparam name="T">
		/// 	<para>The type of values.</para>
		/// </typeparam>
		/// <param name="expectedValue">
		/// 	<para>The expected value.</para>
		/// </param>
		/// <param name="maxDeviation">
		/// 	<para>The max allowable deviation of on the expectedValue.</para>
		/// </param>
		/// <param name="actualValue">
		/// 	<para>The Value to be verified.</para>
		/// </param>
		/// <exception cref="NotImplementedException">
		/// 	<para>The typeParam is not implemented.</para>		
		/// </exception>
		public static void AreRoughlyEqual<T>(T expectedValue, T maxDeviation, T actualValue) where T : IComparable<T>
		{
			var type = typeof(T);

			if (type != typeof(int)
				&& type != typeof(long)
				&& type != typeof(double)
				&& type != typeof(float)
				&& type != typeof(TimeSpan))
			{
				throw new NotImplementedException();
			}

			int diff = actualValue.CompareTo(expectedValue);
			if (diff != 0)
			{
				if (type == typeof(int))
				{
					int expectedInt = (int)(object)expectedValue;
					int actualInt = (int)(object)actualValue;
					int maxDevInt = (int)(object)maxDeviation;
					int maxPossibleInt = expectedInt + maxDevInt;
					int minPossibleInt = expectedInt - maxDevInt;
					Assert.IsTrue(
						((actualInt <= maxPossibleInt) && (actualInt >= minPossibleInt)),
						string.Format("Value '{0}' is not in limits of Min value '{1}' and Max Value '{2}'", actualInt, minPossibleInt, maxPossibleInt));
				}
				else if (type == typeof(long))
				{
					long expectedLong = (long)(object)expectedValue;
					long actualLong = (long)(object)actualValue;
					long maxDevLong = (long)(object)maxDeviation;
					long maxPossibleLong = expectedLong + maxDevLong;
					long minPossibleLong = expectedLong - maxDevLong;
					Assert.IsTrue(
						(actualLong <= maxPossibleLong && actualLong >= minPossibleLong),
						string.Format("Value '{0}' is not in limits of Min value '{1}' and Max Value '{2}'", actualLong, minPossibleLong, maxPossibleLong));
				}
				else if (type == typeof(double))
				{
					double expectedDouble = (double)(object)expectedValue;
					double actualDouble = (double)(object)actualValue;
					double maxDevDouble = (double)(object)maxDeviation;
					double maxPossibleDouble = expectedDouble + maxDevDouble;
					double minPossibleDouble = expectedDouble - maxDevDouble;
					Assert.IsTrue(
						(actualDouble <= maxPossibleDouble && actualDouble >= minPossibleDouble),
						string.Format("Value '{0}' is not in limits of Min value '{1}' and Max Value '{2}'", actualDouble, minPossibleDouble, maxPossibleDouble));
				}
				else if (type == typeof(float))
				{
					float expectedFloat = (float)(object)expectedValue;
					float actualFloat = (float)(object)actualValue;
					float maxDevFloat = (float)(object)maxDeviation;
					float maxPossibleFloat = expectedFloat + maxDevFloat;
					float minPossibleFloat = expectedFloat - maxDevFloat;
					Assert.IsTrue(
						(actualFloat <= maxPossibleFloat && actualFloat >= minPossibleFloat),
						string.Format("Value '{0}' is not in limits of Min value '{1}' and Max Value '{2}'", actualFloat, minPossibleFloat, maxPossibleFloat));
				}
				else if (type == typeof(TimeSpan))
				{
					TimeSpan expectedTimeSpan = (TimeSpan)(object)expectedValue;
					TimeSpan actualTimeSpan = (TimeSpan)(object)actualValue;
					TimeSpan maxDevTimeSpan = (TimeSpan)(object)maxDeviation;
					TimeSpan maxPossibleTimeSpan = expectedTimeSpan + maxDevTimeSpan;
					TimeSpan minPossibleTimeSpan = expectedTimeSpan - maxDevTimeSpan;
					Assert.IsTrue(
						(actualTimeSpan <= maxPossibleTimeSpan && actualTimeSpan >= minPossibleTimeSpan),
						string.Format("Value '{0}' is not in limits of Min value '{1}' and Max Value '{2}'", actualTimeSpan, minPossibleTimeSpan, maxPossibleTimeSpan));
				}
			}
		}

		/// <summary>
		/// 	<para>Asserts that two items in an <see cref="IList{T}"/> instance occur in a specified order.</para>
		/// </summary>
		/// <typeparam name="T">
		/// 	<para>The type of values.</para>
		/// </typeparam>
		/// <param name="expectedLeader">
		/// 	<para>The item that is expected to precede the other item in the collection.</para>
		/// </param>
		/// <param name="expectedFollower">
		/// 	<para>The item that is expected to follow the other item in the collection.</para>
		/// </param>
		/// <param name="collection">
		/// 	<para>The collection that contains the two compared items.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="collection"/> is <see langword="null"/>.</para>
		/// </exception>
		public static void IsAheadOf<T>(T expectedLeader, T expectedFollower, IList<T> collection)
		{
			if (collection == null)
			{
				throw new ArgumentNullException("collection");
			}

			int leaderIndex = collection.IndexOf(expectedLeader);
			int followerIndex = collection.IndexOf(expectedFollower);

			Assert.IsTrue(leaderIndex >= 0, String.Format("{0} is not found in the collection", expectedLeader));
			Assert.IsTrue(followerIndex >= 0, String.Format("{0} is not found in the collection", expectedFollower));

			Assert.IsTrue(
				leaderIndex < followerIndex,
				String.Format("'{0}' is expected to precede '{1}' in the collection but does not.", expectedLeader, expectedFollower));
		}

		/// <summary>
		/// 	<para>Asserts that the specified <see cref="IEnumerable{T}"/> instance contains the expected number of items.</para>
		/// </summary>
		/// <typeparam name="T">
		///		<para>The type of items that the collection contains.</para>
		/// </typeparam>
		/// <param name="expectedCount">
		/// 	<para>The number of items expected in the collection.</para>
		/// </param>
		/// <param name="collection">
		/// 	<para>The <see cref="IEnumerable{T}"/> instance to count.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="collection"/> is <see langword="null"/>.</para>
		/// </exception>
		public static void IsOfSize<T>(int expectedCount, IEnumerable<T> collection)
		{
			if (collection == null)
			{
				throw new ArgumentNullException("collection");
			}

			int count = 0;
			foreach (T item in collection)
			{
				count++;
			}

			Assert.AreEqual(
				expectedCount,
				count,
				String.Format("Expected Count '{0}' is not same as actual count '{1}'", expectedCount, count));
		}

		/// <summary>
		/// <para>Asserts that the code path encapsulated by <paramref name="dlg"/> throws an
		/// <see cref="ArgumentNullException"/> with parameter name of <paramref name="paramName"/>.</para>
		/// </summary>
		/// <param name="paramName">The name of the expected <see langword="null"/> parameter.</param>
		/// <param name="dlg">The delegate to execute.</param>
		/// <returns>The caught exception. Never <see langword="null"/>.</returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="paramName"/> is <see langword="null"/>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="dlg"/> is <see langword="null"/>.</para>
		/// </exception>
		public static ArgumentNullException ThrowsArgumentNullException(string paramName, ParameterlessDelegate dlg)
		{
			var exc = ThrowsArgumentException<ArgumentNullException>(paramName, dlg);
			return exc;
		}

		/// <summary>
		/// <para>Asserts that the code path encapsulated by <paramref name="dlg"/> throws an
		/// <see cref="ArgumentOutOfRangeException"/> with parameter name of <paramref name="paramName"/>.</para>
		/// </summary>
		/// <param name="paramName">The name of the expected <see langword="null"/> parameter.</param>
		/// <param name="dlg">The delegate to execute.</param>
		/// <returns>The caught exception. Never <see langword="null"/>.</returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="paramName"/> is <see langword="null"/>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="dlg"/> is <see langword="null"/>.</para>
		/// </exception>
		public static ArgumentOutOfRangeException ThrowsArgumentOutOfRangeException(string paramName, ParameterlessDelegate dlg)
		{
			var exc = ThrowsArgumentException<ArgumentOutOfRangeException>(paramName, dlg);
			return exc;
		}

		/// <summary>
		/// <para>Asserts that the code path encapsulated by <paramref name="dlg"/> throws an
		/// exception of <typeparamref name="T"/> which is a subclass of <see cref="ArgumentException"/>, with
		/// parameter name of <paramref name="paramName"/>.</para>
		/// </summary>
		/// <typeparam name="T">
		///		<para>The type of items that the collection contains.</para>
		/// </typeparam>
		/// <param name="paramName">The name of the expected <see langword="null"/> parameter.</param>
		/// <param name="dlg">The code path to execute.</param>
		/// <returns>The caught exception.</returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="dlg"/> is <see langword="null"/>.</para>
		/// </exception>
		public static T ThrowsArgumentException<T>(string paramName, ParameterlessDelegate dlg) where T : ArgumentException
		{
			var exc = ThrowsException<T>(dlg);
			Assert.AreEqual(paramName, exc.ParamName, "Incorrect parameter name of " + typeof(T).Name);
			return exc;
		}


		/// <summary>
		/// <para>Asserts that the code path encapsulated by <paramref name="dlg"/> throws an
		/// <see cref="ApplicationException"/>.</para>
		/// </summary>
		/// <param name="dlg">The delegate to execute.</param>
		/// <returns>The caught exception. Never <see langword="null"/>.</returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="dlg"/> is <see langword="null"/>.</para>
		/// </exception>
		public static ApplicationException ThrowsApplicationException(ParameterlessDelegate dlg)
		{
			var exc = ThrowsException<ApplicationException>(dlg);
			return exc;
		}

		/// <summary>
		/// <para>Asserts that the code path encapsulated by <paramref name="dlg"/> throws an excpetion of type 
		/// <typeparamref name="T"/>, and returns caught exception for additional optional validation.</para>
		/// </summary>
		/// <typeparam name="T">The exception type.</typeparam>
		/// <param name="dlg">The code path to execute.</param>
		/// <returns>The caught exception.</returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="dlg"/> is <see langword="null"/>.</para>
		/// </exception>
		public static T ThrowsException<T>(ParameterlessDelegate dlg) where T : Exception
		{
			if (dlg == null)
			{
				throw new ArgumentNullException("dlg");
			}

			try
			{
				dlg();
			}
			catch (Exception exc)
			{
				T ret = exc as T;
				if (ret != null)
				{
					return ret;
				}

				Assert.Fail("Unexpected exception thrown: " + exc.ToString());
			}

			Assert.Fail("No exception thrown.");
			return null;
		}

		/// <summary>
		/// <para>Asserts that an implementation of <see cref="ICustomSerializable"/> throws 
		/// <see cref="NotSupportedException"/> on serialization or deserialization.</para>		
		/// </summary>
		/// <param name="cs">
		/// 	<para>Instace of ICustomSerializable.</para>
		/// </param>			
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="cs"/> is <see langword="null"/>.</para>
		/// </exception>
		public static void AssertCustomSerializableNotSupported(ICustomSerializable cs)
		{
			if (cs == null)
			{
				throw new ArgumentNullException("instance");
			}

			MemoryStream stream = new MemoryStream();
			try
			{
				Serializer.Serialize<ICustomSerializable>(stream, cs);
				Assert.Fail("NotSupportedException expected by ICustomSerializable.Serialize but not thrown.");
			}
			catch (Exception exc)
			{
				NotSupportedException nsexc = exc as NotSupportedException;
				if (nsexc != null)
				{
				}
				else
				{
					Assert.Fail("Expected exception of type NotSupportedException but received" + exc.ToString());
				}
			}

			try
			{
				Serializer.Deserialize<ICustomSerializable>(stream, cs);
				Assert.Fail("NotSupportedException expected by ICustomSerializable.Deserialize but not thrown.");
			}
			catch (Exception exc)
			{
				NotSupportedException nsexc = exc as NotSupportedException;
				if (nsexc != null)
				{
					return;
				}

				Assert.Fail("Expected exception of type NotSupportedException but received" + exc.ToString());
			}
		}

		/// <summary>
		/// 	<para>Asserts that a <see cref="WaitHandle"/> signals within the specified
		/// 	time limit, with a custom assertion message.</para>
		/// </summary>
		/// <param name="handle">
		/// 	<para>The <see cref="WaitHandle"/> to wait for.</para>
		/// </param>
		/// <param name="timeout">
		/// 	<para>The time limit, in milliseconds, that the handle must signal within, 
		/// 	in order for the assertion not to fail.</para>
		/// </param>
		/// <param name="message">
		/// 	<para>The message displayed when the assertion fails.  Optional.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="handle"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// 	<para>The argument <paramref name="timeout"/> is less than 0.</para>
		/// </exception>
		public static void DoesSignal(WaitHandle handle, int timeout, string message)
		{
			DoesSignal(handle, timeout, true, message);
		}


		/// <summary>
		/// 	<para>Asserts that a <see cref="WaitHandle"/> signals within the specified
		/// 	time limit, with a custom assertion message.</para>
		/// </summary>
		/// <param name="handle">
		/// 	<para>The <see cref="WaitHandle"/> to wait for.</para>
		/// </param>
		/// <param name="timeout">
		/// 	<para>The time limit, in milliseconds, that the handle must signal within, 
		/// 	in order for the assertion not to fail.</para>
		/// </param>
		/// <param name="timeoutWhenDebugging">
		///	<para>Specify <see langword="true"/> to timeout normally in debug mode.
		///	Specify <see langword="false"/> to wait indefinatly if the handle doesn't
		///	signal when debugging.</para>
		/// </param>
		/// <param name="message">
		/// 	<para>The message displayed when the assertion fails.  Optional.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="handle"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// 	<para>The argument <paramref name="timeout"/> is less than 0.</para>
		/// </exception>
		public static void DoesSignal(WaitHandle handle, int timeout, bool timeoutWhenDebugging, string message)
		{
			if (handle == null) throw new ArgumentNullException("handle");
			if (timeout < 0) throw new ArgumentOutOfRangeException("timeout", timeout, "Must be greater than or equal to 0.");

			if (!timeoutWhenDebugging && Debugger.IsAttached) timeout = Timeout.Infinite;

			if (!handle.WaitOne(timeout, false))
			{
				if (message == null) message = String.Format("The specified wait handle did not signal as expected within {0}ms.", timeout);
				Assert.Fail(message);
			}
		}

		/// <summary>
		/// 	<para>Asserts that a <see cref="WaitHandle"/> signals within the specified
		/// 	time limit, with a default message.</para>
		/// </summary>
		/// <param name="handle">
		/// 	<para>The <see cref="WaitHandle"/> to wait for.</para>
		/// </param>
		/// <param name="timeout">
		/// 	<para>The time limit, in milliseconds, that the handle must signal within, 
		/// 	in order for the assertion not to fail.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="handle"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// 	<para>The argument <paramref name="timeout"/> is less than 0.</para>
		/// </exception>
		public static void DoesSignal(WaitHandle handle, int timeout)
		{
			DoesSignal(handle, timeout, true, null);
		}

		/// <summary>
		/// 	<para>Asserts that a <see cref="WaitHandle"/> signals within the specified
		/// 	time limit, with a default message.</para>
		/// </summary>
		/// <param name="handle">
		/// 	<para>The <see cref="WaitHandle"/> to wait for.</para>
		/// </param>
		/// <param name="timeout">
		/// 	<para>The time limit, in milliseconds, that the handle must signal within, 
		/// 	in order for the assertion not to fail.</para>
		/// </param>
		/// <param name="timeoutWhenDebugging">
		///	<para>Specify <see langword="true"/> to timeout normally in debug mode.
		///	Specify <see langword="false"/> to wait indefinatly if the handle doesn't
		///	signal when debugging.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="handle"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// 	<para>The argument <paramref name="timeout"/> is less than 0.</para>
		/// </exception>
		public static void DoesSignal(WaitHandle handle, int timeout, bool timeoutWhenDebugging)
		{
			DoesSignal(handle, timeout, timeoutWhenDebugging, null);
		}

		/// <summary>
		/// 	<para>Asserts that the specified string is not null or empty.</para>
		/// </summary>
		/// <param name="str">
		/// 	<para>The <see cref="String"/> to check if it's null or empty.</para>
		/// </param>
		public static void IsNotNullOrEmpty(string str)
		{
			if (String.IsNullOrEmpty(str))
			{
				Assert.Fail("String is unexpectedly null or empty.");
			}
		}

		/// <summary>
		/// 	<para>Asserts that an XPath query does yield a node in the specified
		///		XML document.</para>
		/// </summary>
		/// <param name="xml">
		/// 	<para>The XML document on which to run the XPath.</para>
		/// </param>
		/// <param name="xpath">
		/// 	<para>The XPath query to run on the specified XML document.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="xml"/> is <see langword="null"/>.</para>
		/// 	<para>-or-</para>
		/// 	<para>The argument <paramref name="xpath"/> is <see langword="null"/>.</para>
		/// </exception>
		public static void XPathExists(XmlDocument xml, string xpath)
		{
			if (xml == null) throw new ArgumentNullException("xml");
			if (xpath == null) throw new ArgumentNullException("xpath");

			if (xml.SelectSingleNode(xpath) == null)
			{
				Assert.Fail(
					String.Format(
						"XPath '{0}' failed to yield a node in this XML document: {1}",
						xpath,
						xml.InnerXml));
			}
		}

		/// <summary>
		/// 	<para>Asserts that an XPath query does NOT yield a node in the specified
		///		XML document.</para>
		/// </summary>
		/// <param name="xml">
		/// 	<para>The XML document on which to run the XPath.</para>
		/// </param>
		/// <param name="xpath">
		/// 	<para>The XPath query to run on the specified XML document.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="xml"/> is <see langword="null"/>.</para>
		/// 	<para>-or-</para>
		/// 	<para>The argument <paramref name="xpath"/> is <see langword="null"/>.</para>
		/// </exception>
		public static void XPathDoesNotExist(XmlDocument xml, string xpath)
		{
			if (xml == null) throw new ArgumentNullException("xml");
			if (xpath == null) throw new ArgumentNullException("xpath");

			if (xml.SelectSingleNode(xpath) != null)
			{
				Assert.Fail(
					String.Format(
						"XPath '{0}' unexpectedly yielded a node in this XML document: {1}",
						xpath,
						xml.InnerXml));
			}
		}

		[Serializable]
		class ExceptionInfo
		{
			public string ClassName { get; private set; }

			public string Message { get; private set; }

			public string StackTrace { get; private set; }

			public ExceptionInfo InnerInfo { get; private set; }

			public ExceptionInfo(Exception exc)
			{
				ClassName = exc.GetType().FullName;
				Message = exc.Message;
				StackTrace = exc.StackTrace;
				if (exc.InnerException != null)
				{
					InnerInfo = new ExceptionInfo(exc.InnerException);
				}
			}

			private void AddString(IndentedTextWriter writer)
			{
				writer.WriteLine("Class: {0}", ClassName);
				writer.WriteLine("Message: {0}", Message);
				writer.WriteLine("Stack Trace: {0}", StackTrace);
				if (InnerInfo != null)
				{
					writer.WriteLine("Inner Exception:");
					++writer.Indent;
					InnerInfo.AddString(writer);
				}
			}

			public override string ToString()
			{
				var sbd = new StringBuilder();
				using(var writer = new IndentedTextWriter(new StringWriter(sbd)))
				{
					AddString(writer);
				}
				return sbd.ToString();
			}
		}

		class TestProxy : MarshalByRefObject
		{
			private readonly Action _tests;

			public TestProxy(Action tests)
			{
				_tests = tests;
			}

			public ExceptionInfo PerformTests()
			{
				try
				{
					_tests();
				}
				catch(Exception exc)
				{
					return new ExceptionInfo(exc);
				}
				return null;
			}
		}

		/// <summary>
		/// Performs tests in a new domain.
		/// </summary>
		/// <param name="configPath">The <see cref="String"/> path to the
		/// configuration file to use for this new domain.</param>
		/// <param name="tests"><see cref="Action"/> of tests.</param>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="configPath"/> is null or empty</para>
		/// <para>-or-</para>
		/// <para><paramref name="tests"/> is null.</para>
		/// </exception>
		public static void PerformTestsInSeparateDomain(string configPath,
			Action tests)
		{
			if (string.IsNullOrEmpty(configPath))
			{
				throw new ArgumentNullException("configPath");
			}
			if (tests == null)
			{
				throw new ArgumentNullException("tests");
			}
			var domain = AppDomain.CreateDomain(
				"PerformTestsInSeparateDomain_domain", null, new AppDomainSetup
             	{
             		ApplicationBase = Environment.CurrentDirectory,
             		ConfigurationFile = Path.Combine(Environment.CurrentDirectory,
						configPath)
             	});
			var proxy = (TestProxy) domain.CreateInstanceAndUnwrap(
        		typeof (TestProxy).Assembly.FullName, typeof (TestProxy).FullName,
				false, BindingFlags.Default, null, new object[] {tests}, null,
				null, null);
			var info = proxy.PerformTests();
			if (info != null)
			{
				Assert.Fail(info.ToString());
			}
		}
	}
}