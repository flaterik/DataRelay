using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Collections;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using MySpace.Common;
using MySpace.Common.IO;
using MySpace.Common.Framework;

namespace MySpace.Common.UnitTests
{
	public class SerializationTests
	{
		/// <summary>
		/// Tests the custom serialization of type T that implements ICustomSerializable.
		/// All public and private static member variables will be filled with randomly generated data,
		/// passed through the serialization process, and the output compared with the input.
		/// Note that not all types of data can be generated - please see the console log of the test 
		/// for more information.
		/// This method obeys the [NonSerialized()] attribute - fields marked with it will not be filled or
		/// tested.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		public static void TestSerializationObject<T>() where T : ICustomSerializable, new()
		{
			T testObject = new T();
			T testedObject = default(T);
			MemoryStream stream = new MemoryStream();

			FillObject(testObject, true);

			Serializer.Serialize<T>(stream, testObject);
			stream.Seek(0, SeekOrigin.Begin);
			testedObject = Serializer.Deserialize<T>(stream);

			Assert.IsTrue(CompareObjects(testObject, testedObject), "Type " + typeof(T).Name + " failed serialization check. Check console output for details.");
		}

		public static void TestSerializationObjectVersion<T>() where T : ICustomSerializable, new()
		{
			T expectedObject = new T();
			FillObject(expectedObject, false);
			T actualObject = GetActualObject<T>();
			Assert.IsTrue(CompareObjects(expectedObject, actualObject), "Type " + typeof(T).Name + " failed version compatibility serialization check. Check console output for details.");
		}

		#region Helper Methods
		private static void SaveSerializationObject<T>() where T : ICustomSerializable, new()
		{
			T expectedObject = new T();
			FillObject(expectedObject, false);
			MemoryStream stream = new MemoryStream();
			Serializer.Serialize<T>(stream, expectedObject);
			stream.Seek(0, SeekOrigin.Begin);
			SaveStreamToDB(typeof(T).FullName, stream);
		}


		private static T GetActualObject<T>() where T : ICustomSerializable, new()
		{
			string typeName = typeof(T).FullName;
			MemoryStream stream = GetStreamFromDB(typeName);
			return Serializer.Deserialize<T>(stream);
		}

		private static MemoryStream GetStreamFromDB(string typeName)
		{
            //SqlConnection conn = new SqlConnection(MySpace.Configuration.ConnectionStringProvider.ConnectionStrings["TestAssistants"].ConnectionString);
			SqlConnection conn = new SqlConnection(@"server=devsrv\sql2005;database=TestAssistants;uid=ASPADONet;pwd=12345;Connect Timeout=20;Integrated Security=false;");

			System.Data.SqlClient.SqlCommand sqlCommand = new System.Data.SqlClient.SqlCommand();
			sqlCommand.Connection = conn;
			sqlCommand.CommandType = System.Data.CommandType.StoredProcedure;
			sqlCommand.CommandText = "getSerializedObject";

			SqlParameter paramClassName = new SqlParameter("@className", SqlDbType.NVarChar, 100);
			paramClassName.Value = typeName;
			sqlCommand.Parameters.Add(paramClassName);


			object returnValue;
			try
			{
				conn.Open();
				returnValue = sqlCommand.ExecuteScalar();
			}
			finally
			{
				if (conn.State != ConnectionState.Closed)
				{
					conn.Close();
				}
			}

			MemoryStream stream = new MemoryStream((byte[])returnValue);
			return stream;
		}

		private static void SaveStreamToDB(string className, MemoryStream stream)
		{

            //SqlConnection conn = new SqlConnection(MySpace.Configuration.ConnectionStringProvider.ConnectionStrings["TestAssistants"].ConnectionString);
			SqlConnection conn = new SqlConnection(@"server=devsrv\sql2005;database=TestAssistants;uid=ASPADONet;pwd=12345;Connect Timeout=20;Integrated Security=false;");

			System.Data.SqlClient.SqlCommand sqlCommand = new System.Data.SqlClient.SqlCommand();
			sqlCommand.Connection = conn;
			sqlCommand.CommandType = System.Data.CommandType.StoredProcedure;
			sqlCommand.CommandText = "saveSerializedObject";

			SqlParameter paramClassName = new SqlParameter("@className", SqlDbType.NVarChar, 100);
			paramClassName.Value = className;
			sqlCommand.Parameters.Add(paramClassName);

			SqlParameter paramObject = new SqlParameter("@serializedObject", SqlDbType.VarBinary);
			paramObject.Value = stream.GetBuffer();
			sqlCommand.Parameters.Add(paramObject);

			try
			{
				conn.Open();
				sqlCommand.ExecuteNonQuery();
			}
			finally
			{
				if (conn.State != ConnectionState.Closed)
				{
					conn.Close();
				}
			}
		}


		private static bool CompareObjects(object expected, object actual)
		{
			foreach (FieldInfo field in GetFieldInfo(expected))
			{
				if (field.GetCustomAttributes(typeof(NonSerializedAttribute), false).Length == 0)
				{
					if (!CompareField(field.FieldType, field.GetValue(expected), field.GetValue(actual)))
					{
						Console.WriteLine("Field " + field.Name + " differed in " + expected.GetType().FullName);
						return false;
					}
				}
			}
			return true;
		}

		private static bool CompareField(Type type, object expected, object actual)
		{
			if (actual == null)
			{
				if (expected == null)
				{
					return true;
				}
				else
				{
					return false;
				}
			}
			if (type.IsPrimitive)
			{
				return actual.Equals(expected);
			}
            if (type.Name == "Color")
            {
               return  CompareColor(expected, actual);
            }
			if (type.Name == "String") //common enough to deserve a special case.
			{
				return actual.Equals(expected);
			}
			else if (type.Name == "DateTime")
			{
			    DateTime actual_t = (DateTime)actual;
			    DateTime expected_t = (DateTime) expected;
                DateTime actual_comp = new DateTime(actual_t.Year, actual_t.Month, actual_t.Day, actual_t.Hour, actual_t.Minute, actual_t.Second);
                DateTime expected_comp = new DateTime(expected_t.Year, expected_t.Month, expected_t.Day, expected_t.Hour, expected_t.Minute, expected_t.Second);
                return (actual_comp == expected_comp);
			}
			else if (type.Name == "Decimal")
			{
				return actual.Equals(expected);
			}
			else if (type.IsArray)
			{
				Array actualArray = (Array)actual;
				Array expectedArray = (Array)expected;
				if (actualArray.Length == expectedArray.Length)
				{
					for (int i = 0; i < actualArray.Length; i++)
					{
						if (!CompareField(type.GetElementType(), expectedArray.GetValue(i), actualArray.GetValue(i)))
						{
							Console.WriteLine("Element " + i.ToString() + " of array differed.");
							return false;
						}
					}
				}
				else
				{
					Console.WriteLine("Length of arrays differed.");
					return false;
				}
				return true;
			}
			else if (type.IsEnum)
			{
				return actual.Equals(expected);
			}
			else //not a simple type
			{
				if (type.GetInterface("ICollection") != null)
				{
					if (type.GetInterface("ICollection`1") != null) //generic collection of some sort
					{
						if (((System.Collections.ICollection)expected).Count != ((System.Collections.ICollection)actual).Count)
						{
							Console.WriteLine("ICollection count differed.");
							return false;
						}
						else
						{
							if (type.GetInterface("IList") != null)
							{
								for (int i = 0; i < ((System.Collections.ICollection)expected).Count; i++)
								{
									if (!CompareObjects(((System.Collections.IList)expected)[i], ((System.Collections.IList)actual)[i]))
									{
										Console.WriteLine("Element " + i.ToString() + " of IList differed.");
										return false;
									}
								}
							}
							else if (type.GetInterface("IDictionary") != null)
							{
								foreach (object key in ((System.Collections.IDictionary)actual).Keys)
								{
									object expectedItem = ((System.Collections.IDictionary)expected)[key];
									object actualItem = ((System.Collections.IDictionary)actual)[key];
									if (actualItem == null || !CompareObjects(expectedItem, actualItem))
									{
										Console.WriteLine("Element " + key.ToString() + " of IDictionary differed.");
										return false;
									}
								}
							}
							else
							{
								Console.WriteLine("Don't know how to compare type " + type.FullName);
								return false;
							}
						}
						return true;
					}
					else
					{
						if (type.FullName == "System.Collections.IList") //ILists are always populated as ArrayLists of ints
						{
							for (int i = 0; i < ((System.Collections.ICollection)expected).Count; i++)
							{
								if (!CompareObjects(((System.Collections.IList)expected)[i], ((System.Collections.IList)actual)[i]))
								{
									Console.WriteLine("Element " + i.ToString() + " of non-generic IList differed.");
									return false;
								}
							}
							return true;
						}
						else
						{
							Console.WriteLine("Don't know how to compare non-generic collections: " + type.FullName);
							return false;
						}
					}
				}
				else
				{
					return CompareObjects(expected, actual);
				}
			}

		}

        private static bool CompareColor(object expected,object actual)
        {
            System.Drawing.Color expectedColor = (System.Drawing.Color) expected;
            System.Drawing.Color actualColor = (System.Drawing.Color)actual;
            return ((expectedColor.A == actualColor.A) && (expectedColor.R == actualColor.R)
                    && (expectedColor.G == actualColor.G) && (expectedColor.B == actualColor.B));
        }

	    private static FieldInfo[] GetFieldInfo(object obj)
		{
			if (obj != null)
			{
				Type type = obj.GetType();

				FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

				return fields;
			}
			else
			{
				return new FieldInfo[0];
			}
		}

		private static object GetValueForType(Type type, bool randomize)
		{
			try
			{
				if (randomize)
				{
					if (random.Next(1, 100) == 15 && !type.IsValueType)
					{
						Console.WriteLine("Null value randomly assigned to value for type " + type.FullName);
						return null;
					}
				}

				if (type.FullName.StartsWith("System.Nullable")) 
				{ 
					string typeName = type.FullName;
					int startPos = typeName.IndexOf("`1[[");
					int endPos = typeName.IndexOf(", mscorlib");;
					if(startPos > -1 && endPos > -1)
					{
						startPos += 4;
						string wrapedTypeName = type.FullName.Substring(startPos, endPos - startPos);
						type = Type.GetType(wrapedTypeName);
					}
                    else if (typeName.Contains("MySpace.Friends.DobLockType"))
                    {
                    }
				}


				if (type.IsPrimitive)
				{
					//The primitive types are Boolean, Byte, SByte, Int16, UInt16, Int32, UInt32, Int64, UInt64, Char, Double, and Single.
					switch (type.Name)
					{
						case "Boolean":
							return randomize ? (random.Next(0, 1) == 1) : false;
                        case "Byte":
							return randomize ? Convert.ToByte(random.Next(Convert.ToInt32(Byte.MinValue), Convert.ToInt32(Byte.MaxValue))) : Byte.MaxValue;
						case "SByte":
							return randomize ? Convert.ToSByte(random.Next(Convert.ToInt32(SByte.MinValue), Convert.ToInt32(SByte.MaxValue))) : SByte.MinValue;
						case "Int16":
                            return randomize ? (Int16)random.Next(Int16.MinValue, Int16.MaxValue) : Int16.MinValue;
						case "UInt16":
							return randomize ? (UInt16)random.Next(UInt16.MinValue, UInt16.MaxValue) : UInt16.MaxValue;
						case "Int32":
							return randomize ? random.Next(Int32.MinValue, Int32.MaxValue) : Int32.MaxValue;
						case "UInt32":
							return randomize ? (UInt32)random.Next(0, Int32.MaxValue) : UInt32.MinValue;
						case "Int64":
							return randomize ? (Int64)random.Next(Int32.MinValue, Int32.MaxValue) : Int64.MinValue;
						case "UInt64":
							return randomize ? (UInt64)random.Next(0, Int32.MaxValue) : UInt64.MaxValue;
						case "Char":
							return randomize ? Char.Parse(random.Next(0, 9).ToString()) : Char.MaxValue;
						case "Double":
							return randomize ? random.NextDouble() : Double.MaxValue;
						case "Single":
							return randomize ? Convert.ToSingle(random.NextDouble()) : Single.MinValue;
						default:
							return new object();
					}
				}
				if (type.Name == "String") //common enough to deserve a special case.
				{
					if (randomize)
					{
						System.Threading.Thread.Sleep(2);
						long ticks = System.DateTime.Now.Ticks;
						string tickString = ticks.ToString("N");
						return tickString;
					}
					else
					{
						return "JimBobBanana";
					}
				}
				else if (type.Name == "DateTime")
				{
					if (randomize)
					{
						return System.DateTime.Now;
					}
					else
					{
						return System.DateTime.MinValue;
					}
				}
				else if (type.Name == "Decimal")
				{
					if (randomize)
					{
						return Decimal.Parse(random.NextDouble().ToString());
					}
					else
					{
						return Decimal.Parse("42.23");
					}
				}
				else if (type.IsArray)
				{
					Type elementType = type.GetElementType();
					Array arrayValue = randomize ? Array.CreateInstance(elementType, random.Next(1, 50)) : Array.CreateInstance(elementType, 42);

					for (int i = 0; i < arrayValue.Length; i++)
					{
						arrayValue.SetValue(GetValueForType(elementType, randomize), i);
					}
					return arrayValue;
				}
				else if (type.IsEnum)
				{
					Array values = Enum.GetValues(type);
					if (randomize)
					{
						return values.GetValue(random.Next(0, values.Length - 1));
					}
					else
					{
						return values.GetValue(0);
					}
				}
                else if (type.Name == "BitArray")
                {
                    Type elementType = Type.GetType("System.Boolean");
                    int arrayLength = randomize ? random.Next(1, 50) : 42;
                    BitArray arrayValue = new BitArray(arrayLength);
                    for (int i = 0; i < arrayLength; i++)
                    {
                        arrayValue[i] = ((Boolean)GetValueForType(elementType, randomize)) ;;
                    }
                    return arrayValue;
                } 
                else //not a simple type
                {
                    ConstructorInfo ctor = type.GetConstructor(System.Type.EmptyTypes);
                    if (ctor != null)
                    {
                        object value = ctor.Invoke(null);
                        if (value is System.Collections.ICollection)
                        {

                            int numElements = randomize ? random.Next(5, 10) : 7;
                            if (type.GetInterface("ICollection`1", false) != null)
                            {
                                Type[] genericArguments = null;
                                if (type.IsGenericType)
                                {
                                    genericArguments = type.GetGenericArguments();
                                    if (value is System.Collections.IList)
                                    {
                                        for (int i = 0; i < numElements; i++)
                                        {
                                            object elementValue = GetValueForType(genericArguments[0], randomize);
                                            ((System.Collections.IList)value).Add(elementValue);
                                        }

                                    }
                                    else if (value is System.Collections.IDictionary)
                                    {
                                        for (int i = 0; i < numElements; i++)
                                        {
                                            object elementKey = GetValueForType(genericArguments[0], randomize);
                                            object elementValue = GetValueForType(genericArguments[1], randomize);
                                            ((System.Collections.IDictionary)value).Add(elementKey, elementValue);
                                        }

                                    }
                                    else
                                    {
                                        value = null;
                                        Console.WriteLine("Don't know how to generate value for generic collections other than dictionary and list: " + type.FullName);
                                    }
                                }
                                else
                                {
                                    if (type.BaseType.IsGenericType)
                                    {
                                        genericArguments = type.BaseType.GetGenericArguments();
                                        if (type.BaseType.Name == "KeyedCollection`2")
                                        {
                                            //too difficult to guarantee unique keys.
                                            object elementValue = GetValueForType(genericArguments[1], randomize);
                                            ((System.Collections.IList)value).Add(elementValue);

                                        }
                                        else
                                        {
                                            value = null;
                                            Console.WriteLine("Don't know how to deal with generic collection of base type " + type.BaseType.Name);
                                        }
                                    }
                                    else
                                    {
                                        value = null;
                                        Console.WriteLine("Collection implements ICollection`1 but neither it nor its base IsGenericType!");
                                    }
                                }

                            }
                            else
                            {
                                value = null;
                                Console.WriteLine("Don't know how to fill non generic collection: " + type.FullName);
                            }
                            return value;
                        }
                        else
                        {
                            FillObject(value, randomize);

                            return value;
                        }

                    }
                    else
                    {
                        if (type.FullName == "System.Collections.IList")
                        {
                            int numElements = randomize ? random.Next(5, 10) : 7;
                            object value = new ArrayList(numElements);
                            for (int i = 0; i < numElements; i++)
                            {
                                ((ArrayList)value).Add(i);
                            }
                            return value;
                        }
                        else
                        {
                            Console.WriteLine("Don't know how to fill without default ctor: " + type.FullName);
                            return null;
                        }
                    }
                }
			}
			catch (Exception ex)
			{
				Console.WriteLine("Exception generating value for type " + type.FullName + ": " + ex.ToString());
			}
			return null;
		}

		private static Random random = null;
		private static void FillObject(object obj, bool randomize)
		{
			random = new Random(DateTime.Now.Second);
			foreach (FieldInfo field in GetFieldInfo(obj))
			{
				if (field.GetCustomAttributes(typeof(NonSerializedAttribute), false).Length == 0)
				{
					object value = GetValueForType(field.FieldType, randomize);
					if (value != null)
					{
						field.SetValue(obj, value);
					}
					else
					{
						Console.WriteLine("Didn't get value for field " + field.Name);
					}
				}
			}
		}

		#endregion
	}
}
