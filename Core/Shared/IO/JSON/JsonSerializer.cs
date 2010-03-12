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
    /// Specifies reference loop handling options for the <see cref="JsonWriter"/>.
    /// </summary>
    public enum ReferenceLoopHandling
    {
        /// <summary>
        /// Throw a <see cref="JsonSerializationException"/> when a loop is encountered.
        /// </summary>
        Error = 0,
        /// <summary>
        /// Ignore loop references and do not serialize.
        /// </summary>
        Ignore = 1,
        /// <summary>
        /// Serialize loop references.
        /// </summary>
        Serialize = 2
    }

    /// <summary>
    /// Serializes and deserializes objects into and from the Json format.
    /// The <see cref="JsonSerializer"/> enables you to control how objects are encoded into Json.
    /// </summary>
    public class JsonSerializer
    {
        private ReferenceLoopHandling _referenceLoopHandling;
        private static bool _serializeFullObject;
        private JsonConverterCollection _converters;
        private static JsonSerializer _singleInstance = new JsonSerializer();

        /// <summary>
        /// Get or set how reference loops (e.g. a class referencing itself) is handled.
        /// </summary>
        public ReferenceLoopHandling ReferenceLoopHandling
        {
            get { return _referenceLoopHandling; }
            set
            {
                if (value < ReferenceLoopHandling.Error || value > ReferenceLoopHandling.Serialize)
                {
                    throw new ArgumentOutOfRangeException("value");
                }
                _referenceLoopHandling = value;
            }
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

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSerializer"/> class.
        /// </summary>
        public JsonSerializer()
        {
            _referenceLoopHandling = ReferenceLoopHandling.Error;
        }

        #region Serialize
        /// <summary>
        /// Static method that serializes the specified <see cref="Object"/> and writes the Json structure
        /// and outputs the result as a <see cref="string"/>.  Only properties are serialized, fields are
        /// ignored.
        /// </summary>
        /// <param name="value">The <see cref="Object"/> to serialize.</param>
        public static string Serialize(object value)
        {
            return Serialize(value, false);
        }

        public static string Serialize(object value, bool serializeFull)
        {
            if (value == null)
            {
                return String.Empty;
            }

            _serializeFullObject = serializeFull;

            using (System.IO.StringWriter sw = new System.IO.StringWriter())
            {
                using (JsonWriter jsonWriter = new JsonWriter(sw))
                {
                    _singleInstance.SerializeValue(jsonWriter, value);
                }
                return sw.ToString();
            }
        }

        /// <summary>
        /// Serializes the specified <see cref="Object"/> and writes the Json structure
        /// to a <c>Stream</c> using the specified <see cref="TextWriter"/>. 
        /// </summary>
        /// <param name="textWriter">The <see cref="TextWriter"/> used to write the Json structure.</param>
        /// <param name="value">The <see cref="Object"/> to serialize.</param>
        public void Serialize(TextWriter textWriter, object value)
        {
            _serializeFullObject = true;
            Serialize(new JsonWriter(textWriter), value);
        }

        /// <summary>
        /// Serializes the specified <see cref="Object"/> and writes the Json structure
        /// to a <c>Stream</c> using the specified <see cref="JsonWriter"/>. 
        /// </summary>
        /// <param name="jsonWriter">The <see cref="JsonWriter"/> used to write the Json structure.</param>
        /// <param name="value">The <see cref="Object"/> to serialize.</param>
        public void Serialize(JsonWriter jsonWriter, object value)
        {
            _serializeFullObject = true;
            if (jsonWriter == null)
                throw new ArgumentNullException("jsonWriter");

            if (value == null)
                throw new ArgumentNullException("value");

            SerializeValue(jsonWriter, value);
        }


        private void SerializeValue(JsonWriter writer, object value)
        {
            JsonConverter converter;

            if (value == null)
            {
                writer.WriteNull();
            }
            else if (HasMatchingConverter(value.GetType(), out converter))
            {
                converter.WriteJson(writer, value);
            }
            else if (value is IConvertible)
            {
                IConvertible convertible = value as IConvertible;

                switch (convertible.GetTypeCode())
                {
                    case TypeCode.String:
                        writer.WriteValue((string)convertible);
                        break;
                    case TypeCode.Char:
                        writer.WriteValue((char)convertible);
                        break;
                    case TypeCode.Boolean:
                        writer.WriteValue((bool)convertible);
                        break;
                    case TypeCode.SByte:
                        writer.WriteValue((sbyte)convertible);
                        break;
                    case TypeCode.Int16:
                        writer.WriteValue((short)convertible);
                        break;
                    case TypeCode.UInt16:
                        writer.WriteValue((ushort)convertible);
                        break;
                    case TypeCode.Int32:
                        writer.WriteValue((int)convertible);
                        break;
                    case TypeCode.Byte:
                        writer.WriteValue((byte)convertible);
                        break;
                    case TypeCode.UInt32:
                        writer.WriteValue((uint)convertible);
                        break;
                    case TypeCode.Int64:
                        writer.WriteValue((long)convertible);
                        break;
                    case TypeCode.UInt64:
                        writer.WriteValue((ulong)convertible);
                        break;
                    case TypeCode.Single:
                        writer.WriteValue((float)convertible);
                        break;
                    case TypeCode.Double:
                        writer.WriteValue((double)convertible);
                        break;
                    case TypeCode.DateTime:
                        //if (_serializeFullObject)
                        //writer.WriteValue((DateTime)convertible);
                        //else
                        writer.WriteValue(((DateTime)convertible).ToString("s"));
                        break;
                    case TypeCode.Decimal:
                        writer.WriteValue((decimal)convertible);
                        break;
                    default:
                        SerializeObject(writer, value);
                        break;
                }
            }
            else if (value is IList)
            {
                if (_serializeFullObject)
                {
                    SerializeList(writer, (IList)value);
                }
                else
                {
                    writer.SerializeStack.Add(value);
                    writer.WriteStartObject();

                    Type instanceType = value.GetType().UnderlyingSystemType.DeclaringType;
                    if (value.GetType().UnderlyingSystemType.DeclaringType != typeof(IList))
                    {
                        List<PropertyInfo> members = ReflectionUtils.GetProperties(value.GetType(), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        if (members.Count > 0)
                        {
                            foreach (PropertyInfo member in members)
                            {
                                if (member.CanRead &&
                                    !member.IsDefined(typeof(System.Xml.Serialization.XmlIgnoreAttribute), true))
                                {
                                    WritePropertyInfo(writer, value, member);
                                }
                            }
                        }
                    }
                    if (!(value is ArrayList))
                    {
                        writer.WritePropertyName("List");
                    }
                    SerializeList(writer, (IList)value);
                    writer.WriteEndObject();
                    writer.SerializeStack.Remove(value);
                }
            }
            else if (value is IDictionary)
            {
                SerializeDictionary(writer, (IDictionary)value);
            }
            else if (value is ICollection)
            {
                SerializeCollection(writer, (ICollection)value);
            }
            else if (value is Identifier)
            {
                writer.WriteRaw(value.ToString());
            }
            else
            {
                //Type valueType = value.GetType();

                SerializeObject(writer, value);
            }
            writer.Flush();
        }

        private void WriteMemberInfoProperty(JsonWriter writer, object value, MemberInfo member)
        {
            if (!ReflectionUtils.IsIndexedProperty(member))
            {
                object memberValue = ReflectionUtils.GetMemberValue(member, value);

                if (writer.SerializeStack.IndexOf(memberValue) != -1)
                {
                    switch (_referenceLoopHandling)
                    {
                        case ReferenceLoopHandling.Error:
                            throw new JsonSerializationException("Self referencing loop");
                        case ReferenceLoopHandling.Ignore:
                            // return from method
                            return;
                        case ReferenceLoopHandling.Serialize:
                            // continue
                            break;
                        default:
                            throw new InvalidOperationException(string.Format("Unexpected ReferenceLoopHandling value: '{0}'", _referenceLoopHandling));
                    }
                }

                writer.WritePropertyName(member.Name);
                SerializeValue(writer, memberValue);
            }
        }

        private void WritePropertyInfo(JsonWriter writer, object value, PropertyInfo member)
        {
            if (!ReflectionUtils.IsIndexedProperty(member))
            {
                object memberValue = ((PropertyInfo)member).GetValue(value, null);

                if (writer.SerializeStack.IndexOf(memberValue) != -1)
                {
                    switch (_referenceLoopHandling)
                    {
                        case ReferenceLoopHandling.Error:
                            throw new Exception("Self referencing loop");
                        case ReferenceLoopHandling.Ignore:
                            // return from method
                            return;
                        case ReferenceLoopHandling.Serialize:
                            // continue
                            break;
                        default:
                            throw new InvalidOperationException(string.Format("Unexpected ReferenceLoopHandling value: '{0}'", _referenceLoopHandling));
                    }
                }
                writer.WritePropertyName(member.Name);
                SerializeValue(writer, memberValue);
            }
        }

        private void SerializeObject(JsonWriter writer, object value)
        {
            Type objectType = value.GetType();

            TypeConverter converter = TypeDescriptor.GetConverter(objectType);

            // use the objectType's TypeConverter if it has one and can convert to a string
            if (converter != null && !(converter is ComponentConverter) && converter.GetType() != typeof(TypeConverter))
            {
                if (converter.CanConvertTo(typeof(string)))
                {
                    writer.WriteValue(converter.ConvertToInvariantString(value));
                    return;
                }
            }

            writer.SerializeStack.Add(value);

            writer.WriteStartObject();

            if (_serializeFullObject)
            {
                List<MemberInfo> members = ReflectionUtils.GetFieldsAndProperties(objectType, BindingFlags.Public | BindingFlags.Instance);

                foreach (MemberInfo member in members)
                {
                    if (ReflectionUtils.CanReadMemberValue(member) && !member.IsDefined(typeof(JsonIgnoreAttribute), true))
                        WriteMemberInfoProperty(writer, value, member);
                }
            }
            else
            {
                List<PropertyInfo> members = ReflectionUtils.GetProperties(objectType, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

                foreach (PropertyInfo member in members)
                {
                    if (ReflectionUtils.CanReadMemberValue(member) &&
                    !member.IsDefined(typeof(System.Xml.Serialization.XmlIgnoreAttribute), true))
                        WritePropertyInfo(writer, value, member);
                }
            }

            writer.WriteEndObject();

            writer.SerializeStack.Remove(value);
        }

        private void SerializeCollection(JsonWriter writer, ICollection values)
        {
            object[] collectionValues = new object[values.Count];
            values.CopyTo(collectionValues, 0);

            SerializeList(writer, collectionValues);
        }

        private void SerializeList(JsonWriter writer, IList values)
        {
            writer.WriteStartArray();

            for (int i = 0; i < values.Count; i++)
            {
                SerializeValue(writer, values[i]);
            }

            writer.WriteEndArray();
        }

        private void SerializeDictionary(JsonWriter writer, IDictionary values)
        {
            writer.WriteStartObject();

            foreach (DictionaryEntry entry in values)
            {
                writer.WritePropertyName(entry.Key.ToString());
                SerializeValue(writer, entry.Value);
            }

            writer.WriteEndObject();
        }
        #endregion

    }
}
