using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;
using System.Diagnostics;

using MySpace.Common.IO;
using MySpace.Common.CompactSerialization.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MySpace.Common.UnitTests
{
	/// <summary>
	///<para>Helper methods for unit tests.</para>
	/// </summary>
	public static class UnitTestHelper
	{
		private static string persistenceFolder = DateTime.Now.ToString("yyyyMMddHHmm");

		/// <summary>
		/// 	<para>Executes a collection of methods in all possible combinations.</para>
		/// </summary>
		/// <param name="initializer">
		/// 	<para>The method to execute before the execution of each method combination.</para>
		/// </param>
		/// <param name="finalizer">
		/// 	<para>The method to execute after the execution of each method combination.</para>
		/// </param>
		/// <param name="arrangedMethods">
		/// 	<para>The collection of methods to execute in all possible combinations.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="initializer"/> is <see langword="null"/>.</para>
		/// 	<para>-or-</para>
		/// 	<para>The argument <paramref name="finalizer"/> is <see langword="null"/>.</para>
		/// 	<para>-or-</para>
		/// 	<para>The argument <paramref name="arrangedMethods"/> is <see langword="null"/>.</para>
		/// </exception>
		public static void ExerciseAllCombinations(
			ParameterlessDelegate initializer,
			ParameterlessDelegate finalizer,
			params ParameterlessDelegate[] arrangedMethods)
		{
			if (initializer == null) throw new ArgumentNullException("initializer");
			if (finalizer == null) throw new ArgumentNullException("finalizer");
			if (arrangedMethods == null) throw new ArgumentNullException("arrangedMethods");

			if (arrangedMethods.Length == 1)
			{
				initializer();
				arrangedMethods[0]();
				finalizer();
			}
			else if (arrangedMethods.Length > 1)
			{
				var workingSet = new List<ParameterlessDelegate>(arrangedMethods);

				for (int i = 0; i < workingSet.Count; i++)
				{
					var movingMethod = workingSet[0];

					for (int j = 0; j < workingSet.Count - 1; j++)
					{
						initializer();
						foreach (ParameterlessDelegate method in workingSet)
						{
							method();
						}
						finalizer();

						workingSet.RemoveAt(j);
						workingSet.Insert(j + 1, movingMethod);
					}
				}
			}
		}

		/// <summary>
		/// 	<para>Binary-serializes an object and deserializes it into another object.</para>
		/// </summary>
		/// <typeparam name="T">The object type.</typeparam>
		/// <param name="original">
		/// 	<para>The original instance to serialize.</para>
		/// </param>
		/// <param name="deserialized">
		/// 	<para>The instance to deserialize into.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="original"/> is <see langword="null"/>.</para>
		/// 	<para>-or-</para>
		/// 	<para>The argument <paramref name="deserialized"/> is <see langword="null"/>.</para>
		/// </exception>
		public static void PerformBinaryRoundTrip<T>(T original, T deserialized) where T : IVersionSerializable
		{
			if (original == null) throw new ArgumentNullException("original");
			if (deserialized == null) throw new ArgumentNullException("deserialized");

			using (MemoryStream stream = new MemoryStream())
			{
				Serializer.Serialize<T>(stream, original);

				SaveToFile(stream.ToArray(), ".bin");

				stream.Position = 0;
				Serializer.Deserialize<T>(stream, deserialized);
			}
		}

		/// <summary>
		/// 	<para>Binary-serializes an object and deserializes it into another object.</para>
		/// </summary>
		/// <typeparam name="T">The Object type.</typeparam>
		/// <param name="original">
		/// 	<para>The original instance to serialize.</para>
		/// </param>
		/// <param name="method">
		/// 	<para>The method to deserialize the object with.</para>
		/// </param>
		/// <returns>
		///		<para>The deserialized object of type <typeparam name="T"/>;
		///		never <see langword="null"/>.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="original"/> is <see langword="null"/>.</para>
		/// 	<para>-or-</para>
		/// 	<para>The argument <paramref name="method"/> is <see langword="null"/>.</para>
		/// </exception>
		public static T PerformBinaryRoundTrip<T>(T original, DeserializeMethod<T> method) where T : IVersionSerializable
		{
			using (MemoryStream stream = new MemoryStream())
			{
				Serializer.Serialize<T>(stream, original);

				SaveToFile(stream.ToArray(), ".bin");

				stream.Position = 0;
				using (CompactBinaryReader reader = new CompactBinaryReader(stream))
				{
					return method(reader);
				}
			}
		}

		private static void SaveToFile(byte[] buffer, string extension)
		{
			MethodBase testMethod = GetTestMethodInCallStack();
			string methodName = testMethod.DeclaringType.Name + "." + testMethod.Name;
			string ns = testMethod.DeclaringType.Namespace;

			if (methodName != null)
			{
				string folderPath = Path.Combine(Environment.CurrentDirectory, "Serialized");
				folderPath = Path.Combine(folderPath, ns);
				folderPath = Path.Combine(folderPath, persistenceFolder);
				Directory.CreateDirectory(folderPath);

				string filePath;
				int index = 0;
				while (File.Exists(filePath = Path.Combine(folderPath, methodName + index.ToString() + extension)))
				{
					index++;
				}

				using (FileStream stream = new FileStream(filePath, FileMode.Create))
				{
					stream.Write(buffer, 0, buffer.Length);
				}
			}
		}

		/// <summary>
		/// 	<para>Obtain the name of the test method, if any, that is present
		///		in the current call stack.</para>
		/// </summary>
		/// <returns>
		///		<para>A <see cref="MethodBase"/> of the test method found in the call stack;
		///		<see langword="null"/> if no test method is found.</para>
		/// </returns>
		internal static MethodBase GetTestMethodInCallStack()
		{
			StackTrace stackTrace = new StackTrace();
			for (int i = 2; i < stackTrace.FrameCount; i++)
			{
				StackFrame stackFrame = stackTrace.GetFrame(i);
				MethodBase callingMethod = stackFrame.GetMethod();
				if (callingMethod.GetCustomAttributes(typeof(TestMethodAttribute), true).Length > 0)
				{
					return callingMethod;
				}
			}

			return null;
		}
	}
}
