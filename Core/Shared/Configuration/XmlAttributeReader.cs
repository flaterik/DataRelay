using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Configuration;

namespace MySpace.Common.Configuration
{
    public class XmlAttributeReader
    {
        #region To Bool

        private static bool BoolConverter(string input) { return Convert.ToBoolean(input); }

        public static bool ReadBool(XmlNode parentNode, string attributeName)
        {
            return Read(parentNode, attributeName, BoolConverter, false);
        }

        public static bool ReadBool(XmlNode parentNode, string attributeName, bool defaultValue)
        {
            return Read(parentNode, attributeName, BoolConverter, defaultValue);
        }

        public static bool ReadBool(XmlNode parentNode, string attributeName, bool defaultValue, bool throwExceptionIfMissing)
        {
            return Read(parentNode, attributeName, BoolConverter, defaultValue, throwExceptionIfMissing);
        }

        public static bool TryReadBool(XmlNode parentNode, string attributeName, ref bool value)
        {
            return TryRead(parentNode, attributeName, BoolConverter, ref value);
        }
        #endregion

        #region To Int32

        private static int Int32Converter(string input) { return Convert.ToInt32(input); }

        public static int ReadInt32(XmlNode parentNode, string attributeName)
        {
            return Read(parentNode, attributeName, Int32Converter, int.MinValue);
        }

        public static int ReadInt32(XmlNode parentNode, string attributeName, int defaultValue)
        {
            return Read(parentNode, attributeName, Int32Converter, defaultValue);
        }

        public static int ReadInt32(XmlNode parentNode, string attributeName, int defaultValue, bool throwExceptionIfMissing)
        {
            return Read(parentNode, attributeName, Int32Converter, defaultValue, throwExceptionIfMissing);
        }

        public static bool TryReadInt32(XmlNode parentNode, string attributeName, ref int value)
        {
            return TryRead(parentNode, attributeName, Int32Converter, ref value);
        }
        #endregion

        #region To Int16
        private static short Int16Converter(string input) { return Convert.ToInt16(input); }

        public static short ReadInt16(XmlNode parentNode, string attributeName)
        {
            return Read(parentNode, attributeName, Int16Converter, short.MinValue);
        }

        public static short ReadInt16(XmlNode parentNode, string attributeName, short defaultValue)
        {
            return Read(parentNode, attributeName, Int16Converter, defaultValue);
        }

        public static short ReadInt16(XmlNode parentNode, string attributeName, short defaultValue, bool throwExceptionIfMissing)
        {
            return Read(parentNode, attributeName, Int16Converter, defaultValue, throwExceptionIfMissing);
        }

        public static bool TryReadInt16(XmlNode parentNode, string attributeName, ref short value)
        {
            return TryRead(parentNode, attributeName, Int16Converter, ref value);
        }
        #endregion

        #region To Byte

        private static byte ByteConverter(string input) { return Convert.ToByte(input); }

        public static byte ReadByte(XmlNode parentNode, string attributeName)
        {
            return Read(parentNode, attributeName, ByteConverter, byte.MinValue);
        }

        public static byte ReadByte(XmlNode parentNode, string attributeName, byte defaultValue)
        {
            return Read(parentNode, attributeName, ByteConverter, defaultValue);
        }

        public static byte ReadByte(XmlNode parentNode, string attributeName, byte defaultValue, bool throwExceptionIfMissing)
        {
            return Read(parentNode, attributeName, ByteConverter, defaultValue, throwExceptionIfMissing);
        }

        public static bool TryReadByte(XmlNode parentNode, string attributeName, ref byte value)
        {
            return TryRead(parentNode, attributeName, ByteConverter, ref value);
        }

        #endregion

        #region To String
        private static string StringConverter(string input) { return input; }

        public static string ReadString(XmlNode parentNode, string attributeName)
        {
            return Read(parentNode, attributeName, StringConverter, string.Empty);
        }

        public static string ReadString(XmlNode parentNode, string attributeName, string defaultValue)
        {
            return Read(parentNode, attributeName, StringConverter, defaultValue);
        }

        public static string ReadString(XmlNode parentNode, string attributeName, string defaultValue, bool throwExceptionIfMissing)
        {
            return Read(parentNode, attributeName, StringConverter, defaultValue, throwExceptionIfMissing);
        }

        public static bool TryReadString(XmlNode parentNode, string attributeName, ref string value)
        {
            return TryRead(parentNode, attributeName, StringConverter, ref value);
        }
        #endregion

        #region Generic
        public static T Read<T>(XmlNode parentNode, string attributeName, Converter<string, T> converter, T defaultValue)
        {
            return Read(parentNode, attributeName, converter, defaultValue, false);
        }

        public static T Read<T>(XmlNode parentNode, string attributeName, Converter<string, T> converter, T defaultValue, bool throwExceptionIfMissing)
        {
            ArgumentHelper.AssertNotNull<XmlNode>(parentNode, "parentNode");
            ArgumentHelper.AssertNotEmpty(attributeName, "attributeName");
            ArgumentHelper.AssertNotNull<Converter<string, T>>(converter, "converter");

            XmlAttribute attr = parentNode.Attributes[attributeName];

            if (null == attr)
            {
                if (throwExceptionIfMissing)
                    throw new ConfigurationErrorsException("Missing @" + attributeName + " attribute. This value should is required.", parentNode);
                else
                    return defaultValue;
            }
            else
                return converter(attr.Value);
        }

        public static bool TryRead<T>(XmlNode parentNode, string attributeName, Converter<string, T> converter, ref T value)
        {
            ArgumentHelper.AssertNotNull<XmlNode>(parentNode, "parentNode");
            ArgumentHelper.AssertNotEmpty(attributeName, "attributeName");
            ArgumentHelper.AssertNotNull<Converter<string, T>>(converter, "converter");

            XmlAttribute attr = parentNode.Attributes[attributeName];

            if (null == attr)
            {
                return false;
            }
            else
            {
                value = converter(attr.Value);
                return true;
            }
        }
        #endregion
    }
}
