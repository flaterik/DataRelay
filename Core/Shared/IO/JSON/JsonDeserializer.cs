#region License
// Copyright 2006 James Newton-King
// http://www.newtonsoft.com
//
// This work is licensed under the Creative Commons Attribution 2.5 License
// http://creativecommons.org/licenses/by/2.5/
//
// You are free:
//    * to copy, distribute, display, and perform the work
//    * to make derivative works
//    * to make commercial use of the work
//
// Under the following conditions:
//    * You must attribute the work in the manner specified by the author or licensor:
//          - If you find this component useful a link to http://www.newtonsoft.com would be appreciated.
//    * For any reuse or distribution, you must make clear to others the license terms of this work.
//    * Any of these conditions can be waived if you get permission from the copyright holder.
#endregion

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Collections;
using System.Reflection;
using System.ComponentModel;
using MySpace.Common.IO.JSON.Utilities;

namespace MySpace.Common.IO.JSON
{
	/// <summary>
	/// Serializes and deserializes objects into and from the Json format.
	/// The <see cref="JsonSerializer"/> enables you to control how objects are encoded into Json.
	/// </summary>
	public class JsonDeserializer
	{
        private int _level;
        private JsonConverterCollection _converters;
        private static JsonDeserializer _singleInstance = new JsonDeserializer();

		/// <summary>
		/// Initializes a new instance of the <see cref="JsonSerializer"/> class.
		/// </summary>
		static JsonDeserializer()
		{
		}

        public JsonConverterCollection Converters
        {
            get
            {
                if (_converters == null)
                    _converters = new JsonConverterCollection();

                return _converters;
            }
        }

        private bool HasMatchingConverter(Type type, out JsonConverter matchingConverter)
        {
            if (_converters != null)
            {
                for (int i = 0; i < _converters.Count; i++)
                {
                    JsonConverter converter = _converters[i];

                    if (converter.CanConvert(type))
                    {
                        matchingConverter = converter;
                        return true;
                    }
                }
            }

            matchingConverter = null;
            return false;
        }

        #region Deserialize

        /// <summary>
        /// Deserializes the specified object to a Json object.
        /// </summary>
        /// <param name="value">The object to deserialize.</param>
        /// <param name="type">The <see cref="Type"/> of object being deserialized.</param>
        /// <returns>The deserialized object from the Json string.</returns>
        public static object Deserialize(string value, Type type)
        {
            return _singleInstance.DeserializeObject(value, type);
        }

        /// <summary>
        /// Deserializes the specified object to a Json object.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize.</typeparam>
        /// <param name="value">The object to deserialize.</param>
        /// <returns>The deserialized object from the Json string.</returns>
        /*public static T Deserialize<T>(string value)
        {
            return (T)_singleInstance.DeserializeObject(value, typeof(T));
        }*/

        /// <summary>
        /// Deserializes the specified object to a Json object.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize.</typeparam>
        /// <param name="value">The object to deserialize.</param>
        /// <returns>The deserialized object from the Json string.</returns>
        private object DeserializeObject(string value, Type type)
        {
            StringReader sr = new StringReader(value);
            object deserializedValue;

            using (JsonReader jsonReader = new JsonReader(sr))
            {
                deserializedValue = Deserialize(jsonReader, type);
            }

            return deserializedValue;
        }

        /// <summary>
        /// Deserializes the Json structure contained by the specified <see cref="JsonReader"/>
        /// into an instance of the specified type.
        /// </summary>
        /// <param name="reader">The type of object to create.</param>
        /// <param name="objectType">The <see cref="Type"/> of object being deserialized.</param>
        /// <returns>The instance of <paramref name="objectType"/> being deserialized.</returns>
        /*public object DeserializeFull(JsonReader reader, Type objectType)
        {
            if (reader == null)
                throw new ArgumentNullException("reader");

            if (!reader.Read())
                return null;

            return GetObject(reader, objectType);
        }*/

        /// <summary>
        /// Deserializes the Json structure contained by the specified <see cref="JsonReader"/>
        /// into an instance of the specified type.
        /// </summary>
        /// <param name="reader">The type of object to create.</param>
        /// <param name="objectType">The <see cref="Type"/> of object being deserialized.</param>
        /// <returns>The instance of <paramref name="objectType"/> being deserialized.</returns>
        public object Deserialize(JsonReader reader, Type objectType)
        {
            if (reader == null)
                throw new ArgumentNullException("reader");

            if (!reader.Read())
                return null;

            return GetObject(reader, objectType);
        }

        private JavaScriptArray PopulateJavaScriptArray(JsonReader reader)
        {
            JavaScriptArray jsArray = new JavaScriptArray();

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.EndArray:
                        return jsArray;
                    case JsonToken.Comment:
                        break;
                    default:
                        object value = GetObject(reader, null);

                        jsArray.Add(value);
                        break;
                }
            }

            throw new JsonSerializationException("Unexpected end while deserializing array.");
        }

        private JavaScriptObject PopulateJavaScriptObject(JsonReader reader)
        {
            JavaScriptObject jsObject = new JavaScriptObject();

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        string memberName = reader.Value.ToString();

                        // move to the value token. skip comments
                        do
                        {
                            if (!reader.Read())
                                throw new JsonSerializationException("Unexpected end while deserializing object.");
                        } while (reader.TokenType == JsonToken.Comment);

                        object value = GetObject(reader, null);

                        jsObject[memberName] = value;
                        break;
                    case JsonToken.EndObject:
                        return jsObject;
                    case JsonToken.Comment:
                        break;
                    default:
                        throw new JsonSerializationException("Unexpected token while deserializing object: " + reader.TokenType);
                }
            }

            throw new JsonSerializationException("Unexpected end while deserializing object.");
        }

        private object GetObjectFull(JsonReader reader, Type objectType)
        {
            _level++;

            object value;
            JsonConverter converter;

            if (HasMatchingConverter(objectType, out converter))
            {
                return converter.ReadJson(reader, objectType);
            }
            else
            {
                switch (reader.TokenType)
                {
                    // populate a typed object or generic dictionary/array
                    // depending upon whether an objectType was supplied
                    case JsonToken.StartObject:
                        value = (objectType != null) ? PopulateObject(reader, objectType) : PopulateJavaScriptObject(reader);
                        break;
                    case JsonToken.StartArray:
                        value = (objectType != null) ? PopulateList(reader, objectType) : PopulateJavaScriptArray(reader);
                        break;
                    case JsonToken.Integer:
                    case JsonToken.Float:
                    case JsonToken.String:
                    case JsonToken.Boolean:
                    case JsonToken.Date:
                        value = EnsureType(reader.Value, objectType);
                        break;
                    case JsonToken.Constructor:
                        value = reader.Value.ToString();
                        break;
                    case JsonToken.Null:
                    case JsonToken.Undefined:
                        value = null;
                        break;
                    default:
                        throw new JsonSerializationException("Unexpected token whil deserializing object: " + reader.TokenType);
                }
            }

            _level--;

            return value;
        }

        private static object GetObject(JsonReader reader, Type objectType)
        {
            object value = null;

            switch (reader.TokenType)
            {
                // populate a typed object or generic dictionary/array
                // depending upon whether an objectType was supplied
                case JsonToken.StartObject:
                    if (objectType != null)
                        value = PopulateObject(reader, objectType);
                    break;
                case JsonToken.StartArray:
                    if (objectType != null)
                        value = PopulateList(reader, objectType);
                    break;
                case JsonToken.Integer:
                case JsonToken.Float:
                case JsonToken.String:
                case JsonToken.Boolean:
                case JsonToken.Date:
                    value = EnsureType(reader.Value, objectType);
                    break;
                case JsonToken.Constructor:
                    value = reader.Value.ToString();
                    break;
                case JsonToken.Null:
                case JsonToken.Undefined:
                    value = null;
                    break;
                default:
                    throw new JsonSerializationException("Unexpected token whil deserializing object: " + reader.TokenType);
            }

            return value;
        }

        /*private object EnsureTypeFull(object value, Type targetType)
        {
            // do something about null value when the targetType is a valuetype?
            if (value == null)
                return null;

            if (targetType == null)
                return value;

            Type valueType = value.GetType();

            // type of value and type of target don't match
            // attempt to convert value's type to target's type
            if (valueType != targetType)
            {
                TypeConverter targetConverter = TypeDescriptor.GetConverter(targetType);

                if (!targetConverter.CanConvertFrom(valueType))
                {
                    if (targetConverter.CanConvertFrom(typeof(string)))
                    {
                        string valueString = TypeDescriptor.GetConverter(value).ConvertToInvariantString(value);

                        return targetConverter.ConvertFromInvariantString(valueString);
                    }

                    if (!targetType.IsAssignableFrom(valueType))
                        throw new InvalidOperationException(string.Format("Cannot convert object of type '{0}' to type '{1}'", value.GetType(), targetType));

                    return value;
                }

                return targetConverter.ConvertFrom(value);
            }
            else
            {
                return value;
            }
        }*/

        private static object EnsureType(object value, Type targetType)
        {
            // do something about null value when the targetType is a valuetype?
            if (value == null)
                return null;

            if (targetType == null)
                return value;

            Type valueType = value.GetType();

            // type of value and type of target don't match
            // attempt to convert value's type to target's type
            if (valueType != targetType)
            {
                TypeConverter targetConverter = TypeDescriptor.GetConverter(targetType);

                if (!targetConverter.CanConvertFrom(valueType))
                {
                    if (targetConverter.CanConvertFrom(typeof(string)))
                    {
                        string valueString = TypeDescriptor.GetConverter(value).ConvertToInvariantString(value);

                        return targetConverter.ConvertFromInvariantString(valueString);
                    }

                    if (!targetType.IsAssignableFrom(valueType))
                        throw new InvalidOperationException(string.Format("Cannot convert object of type '{0}' to type '{1}'", value.GetType(), targetType));

                    return value;
                }

                return targetConverter.ConvertFrom(value);
            }
            else
            {
                return value;
            }
        }

        /*private void SetObjectMemberFull(JsonReader reader, object target, Type targetType, string memberName)
        {
            if (!reader.Read())
                throw new JsonSerializationException(string.Format("Unexpected end when setting {0}'s value.", memberName));

            MemberInfo[] memberCollection = targetType.GetMember(memberName);
            Type memberType;
            object value;

            // test if a member with memberName exists on the type
            // otherwise test if target is a dictionary and assign value with the key if it is
            if (!CollectionUtils.IsNullOrEmpty<MemberInfo>(memberCollection))
            {
                MemberInfo member = targetType.GetMember(memberName)[0];

                // ignore member if it is readonly
                if (!ReflectionUtils.CanSetMemberValue(member))
                    return;

                if (member.IsDefined(typeof(JsonIgnoreAttribute), true))
                    return;

                // get the member's underlying type
                memberType = ReflectionUtils.GetMemberUnderlyingType(member);

                value = GetObject(reader, memberType);

                ReflectionUtils.SetMemberValue(member, target, value);
            }
            else if (typeof(IDictionary).IsAssignableFrom(targetType))
            {
                // attempt to get the IDictionary's type
                memberType = ReflectionUtils.GetTypedDictionaryValueType(target.GetType());

                value = GetObject(reader, memberType);

                ((IDictionary)target).Add(memberName, value);
            }
            else
            {
                throw new JsonSerializationException(string.Format("Could not find member '{0}' on object of type '{1}'", memberName, targetType.GetType().Name));
            }
        }*/

        private static void SetObjectMember(JsonReader reader, object target, Type targetType, string memberName)
        {
            if (!reader.Read())
                throw new JsonSerializationException(string.Format("Unexpected end when setting {0}'s value.", memberName));

            Type memberType;
            object value;

            if (0 != String.CompareOrdinal(memberName, "List"))
            {
                MemberInfo[] memberCollection = targetType.GetMember(memberName);

                // test if a member with memberName exists on the type
                // otherwise test if target is a dictionary and assign value with the key if it is
                if (!CollectionUtils.IsNullOrEmpty<MemberInfo>(memberCollection))
                {
                    MemberInfo member = targetType.GetMember(memberName)[0];

                    // ignore member if it is readonly
                    if (!ReflectionUtils.CanSetMemberValue(member))
                        return;

                    if (member.IsDefined(typeof(System.Xml.Serialization.XmlIgnoreAttribute), true))
                        return;

                    // get the member's underlying type
                    memberType = ReflectionUtils.GetMemberUnderlyingType(member);

                    value = GetObject(reader, memberType);

                    ReflectionUtils.SetMemberValue(member, target, value);
                }
                else if (typeof(IDictionary).IsAssignableFrom(targetType))
                {
                    // attempt to get the IDictionary's type
                    memberType = ReflectionUtils.GetTypedDictionaryValueType(target.GetType());

                    value = GetObject(reader, memberType);

                    ((IDictionary)target).Add(memberName, value);
                }
                else
                {
                    throw new JsonSerializationException(string.Format("Could not find member '{0}' on object of type '{1}'", memberName, targetType.GetType().Name));
                }
            }
            else
            {
                if (targetType.IsInstanceOfType(typeof(IList)))
                {
                    if (reader.TokenType == JsonToken.StartObject)
                    {
                        value = PopulateList(reader, targetType);
                    }
                    else
                    {
                        value = GetObject(reader, targetType);
                    }
                }
                else if (ReflectionUtils.IsSubClass(targetType, typeof(List<>)))
                {
                    if (reader.TokenType == JsonToken.StartObject)
                    {
                        value = PopulateList(reader, targetType.UnderlyingSystemType.BaseType);
                    }
                    else
                    {
                        value = GetObject(reader, targetType.UnderlyingSystemType.BaseType);
                    }
                }
                else 
                    value = null;

                if (value != null && value is IEnumerable)
                {
                    foreach (object item in (IEnumerable)value)
                    {
                        ((IList)target).Add(item);
                    }
                }
            }

        }

        /*private object PopulateListFull(JsonReader reader, Type objectType)
        {
            IList list;
            Type elementType = ReflectionUtils.GetTypedListItemType(objectType);

            if (objectType.IsArray || objectType == typeof(ArrayList) || objectType == typeof(object))
                // array or arraylist.
                // have to use an arraylist when creating array because there is no way to know the size until it is finised
                list = new ArrayList();
            else if (ReflectionUtils.IsInstantiatableType(objectType) && typeof(IList).IsAssignableFrom(objectType))
                // non-generic typed list
                list = (IList)Activator.CreateInstance(objectType);
            else if (ReflectionUtils.IsSubClass(objectType, typeof(List<>)))
                // generic list
                list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
            else
                throw new JsonSerializationException(string.Format("Deserializing list type '{0}' not supported.", objectType.GetType().Name));

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.EndArray:
                        ArrayList arrayList = list as ArrayList;
                        if (arrayList != null)
                        {
                            // convert back into array now that it is finised
                            if (objectType.IsArray)
                                list = arrayList.ToArray(elementType);
                            else if (objectType == typeof(object))
                                list = arrayList.ToArray();
                        }

                        return list;
                    case JsonToken.Comment:
                        break;
                    default:
                        object value = GetObject(reader, elementType);

                        list.Add(value);
                        break;
                }
            }

            throw new JsonSerializationException("Unexpected end when deserializing array.");
        }*/

        private static object PopulateList(JsonReader reader, Type objectType)
        {
            IList list;
            Type elementType = ReflectionUtils.GetTypedListItemType(objectType);

            if (objectType.IsArray || objectType == typeof(ArrayList) || objectType == typeof(object))
                // array or arraylist.
                // have to use an arraylist when creating array because there is no way to know the size until it is finised
                list = new ArrayList();
            else if (ReflectionUtils.IsInstantiatableType(objectType) && typeof(IList).IsAssignableFrom(objectType))
                // non-generic typed list
                list = (IList)Activator.CreateInstance(objectType);
            else if (ReflectionUtils.IsSubClass(objectType, typeof(List<>)))
                // generic list
                list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
            else
                throw new JsonSerializationException(string.Format("Deserializing list type '{0}' not supported.", objectType.GetType().Name));

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.EndObject:
                    case JsonToken.EndArray:
                        ArrayList arrayList = list as ArrayList;
                        if (arrayList != null)
                        {
                            // convert back into array now that it is finised
                            if (objectType.IsArray)
                                list = arrayList.ToArray(elementType);
                            else if (objectType == typeof(object))
                                list = arrayList.ToArray();
                        }
                        return list;

                    case JsonToken.Comment:
                        break;

                    default:
                        object value = GetObject(reader, elementType);
                        list.Add(value);
                        break;
                }
            }

            throw new JsonSerializationException("Unexpected end when deserializing array.");
        }

        /*private object PopulateObjectFull(JsonReader reader, Type objectType)
        {
            object newObject = Activator.CreateInstance(objectType);

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        string memberName = reader.Value.ToString();

                        SetObjectMember(reader, newObject, objectType, memberName);
                        break;
                    case JsonToken.EndObject:
                        return newObject;
                    default:
                        throw new JsonSerializationException("Unexpected token when deserializing object: " + reader.TokenType);
                }
            }

            throw new JsonSerializationException("Unexpected end when deserializing object.");
        }*/

        private static object PopulateObject(JsonReader reader, Type objectType)
        {
            object newObject = Activator.CreateInstance(objectType);

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        string memberName = reader.Value.ToString();
                        SetObjectMember(reader, newObject, objectType, memberName);
                        break;

                    case JsonToken.EndObject:
                        return newObject;

                    default:
                        throw new JsonSerializationException("Unexpected token when deserializing object: " + reader.TokenType);
                }
            }

            throw new JsonSerializationException("Unexpected end when deserializing object.");
        }
        #endregion
	}
}
