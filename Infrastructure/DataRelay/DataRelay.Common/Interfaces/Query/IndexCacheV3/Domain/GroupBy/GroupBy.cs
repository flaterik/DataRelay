using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class GroupBy : IVersionSerializable
    {
        #region Data Members

        /// <summary>
        /// Gets or sets the grouping field name list.
        /// </summary>
        /// <value>The grouping field name list.</value>
        public List<string> GroupByFieldNameList { get; set; }

        /// <summary>
        /// Gets or sets the name of the field.
        /// </summary>
        /// <value>The name of the field.</value>
        public string FieldName { get; set; }

        /// <summary>
        /// Gets or sets hash set of the field values.
        /// </summary>
        /// <value>The field value hash set.</value>
        public HashSet<byte[]> FieldValueSet { get; set; }

        /// <summary>
        /// Gets or sets the non grouping field name list.
        /// </summary>
        /// <value>The non grouping field name list.</value>
        public List<string> NonGroupByFieldNameList { get; set; }

        #endregion

        #region Methods

        public override string ToString()
        {
            var stb = new StringBuilder();
            stb.Append("(").Append("GroupByFieldNameList Count: ").Append(GroupByFieldNameList == null ? "Null" : GroupByFieldNameList.Count.ToString());
            if (GroupByFieldNameList != null && GroupByFieldNameList.Count > 0)
            {
                foreach (var groupByFieldName in GroupByFieldNameList)
                {
                    stb.Append("(").Append("GroupByFieldName: ").Append(groupByFieldName).Append("),");
                }
            }
            stb.Append("),");

            stb.Append("(").Append("FieldName: ").Append(String.IsNullOrEmpty(FieldName) ? "Null" : FieldName).Append("),");

            stb.Append("(").Append("FieldValueSet Count: ").Append(FieldValueSet == null ? "Null" : FieldValueSet.Count.ToString());
            if (FieldValueSet != null && FieldValueSet.Count > 0)
            {
                foreach (var fieldValue in FieldValueSet)
                {
                    stb.Append("(").Append("FieldValue: ").Append(fieldValue).Append("),");
                }
            }
            stb.Append("),");

            stb.Append("(").Append("NonGroupByFieldNameList Count: ").Append(NonGroupByFieldNameList == null ? "Null" : NonGroupByFieldNameList.Count.ToString());
            if (NonGroupByFieldNameList != null && NonGroupByFieldNameList.Count > 0)
            {
                foreach (var nonGroupByFieldName in NonGroupByFieldNameList)
                {
                    stb.Append("(").Append("NonGroupByFieldName: ").Append(nonGroupByFieldName).Append("),");
                }
            }
            stb.Append("),");

            return stb.ToString();
        }

        #endregion

        #region IVersionSerializable Members

        /// <summary>
        /// Serialize the class data to a stream.
        /// </summary>
        /// <param name="writer">The <see cref="T:MySpace.Common.IO.IPrimitiveWriter"/> that writes to the stream.</param>
        public void Serialize(IPrimitiveWriter writer)
        {
            using (writer.CreateRegion())
            {
                //GroupingFieldNameList
                if (GroupByFieldNameList == null || GroupByFieldNameList.Count == 0)
                {
                    writer.Write((ushort)0);
                }
                else
                {
                    writer.Write((ushort)GroupByFieldNameList.Count);
                    for (int i = 0; i < GroupByFieldNameList.Count; i++)
                    {
                        writer.Write(GroupByFieldNameList[i]);
                    }
                }

                //FieldName
                writer.Write(FieldName);

                //FieldValueSet
                if (FieldValueSet == null || FieldValueSet.Count == 0)
                {
                    writer.Write((ushort)0);
                }
                else
                {
                    writer.Write((ushort)FieldValueSet.Count);
                    foreach (byte[] fieldValue in FieldValueSet)
                    {
                        if (fieldValue == null || fieldValue.Length == 0)
                        {
                            writer.Write((ushort)0);
                        }
                        else
                        {
                            writer.Write((ushort)fieldValue.Length);
                            writer.Write(fieldValue);
                        }
                    }
                }

                //NonGroupingFieldNameList
                if (NonGroupByFieldNameList == null || NonGroupByFieldNameList.Count == 0)
                {
                    writer.Write((ushort)0);
                }
                else
                {
                    writer.Write((ushort)NonGroupByFieldNameList.Count);
                    for (int i = 0; i < NonGroupByFieldNameList.Count; i++)
                    {
                        writer.Write(NonGroupByFieldNameList[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Deserialize the class data from a stream.
        /// </summary>
        /// <param name="reader">The <see cref="T:MySpace.Common.IO.IPrimitiveReader"/> that extracts used to extra data from a stream.</param>
        /// <param name="version">The value of <see cref="P:MySpace.Common.IVersionSerializable.CurrentVersion"/> that was written to the stream when it was originally serialized to a stream;
        /// the version of the <paramref name="reader"/> data.</param>
        public void Deserialize(IPrimitiveReader reader, int version)
        {
            using (reader.CreateRegion())
            {
                //GroupByFieldNameList
                ushort count = reader.ReadUInt16();
                if (count > 0)
                {
                    GroupByFieldNameList = new List<string>(count);
                    for (ushort i = 0; i < count; i++)
                    {
                        GroupByFieldNameList.Add(reader.ReadString());
                    }
                }

                //FieldName
                FieldName = reader.ReadString();

                //FieldValueList
                count = reader.ReadUInt16();
                if (count > 0)
                {
                    FieldValueSet = new HashSet<byte[]>(new ByteArrayEqualityComparer());
                    ushort len;
                    for (ushort i = 0; i < count; i++)
                    {
                        len = reader.ReadUInt16();
                        if (len > 0)
                        {
                            FieldValueSet.Add(reader.ReadBytes(len));
                        }
                    }
                }

                //NonGroupByFieldNameList
                count = reader.ReadUInt16();
                if (count > 0)
                {
                    NonGroupByFieldNameList = new List<string>(count);
                    for (ushort i = 0; i < count; i++)
                    {
                        NonGroupByFieldNameList.Add(reader.ReadString());
                    }
                }
            }
        }

        private const int CURRENT_VERSION = 1;
        /// <summary>
        /// Gets the current serialization data version of your object.  The <see cref="M:MySpace.Common.IVersionSerializable.Serialize(MySpace.Common.IO.IPrimitiveWriter)"/> method
        /// will write to the stream the correct format for this version.
        /// </summary>
        /// <value></value>
        public int CurrentVersion
        {
            get
            {
                return CURRENT_VERSION;
            }
        }

        /// <summary>
        /// Deprecated. Has no effect.
        /// </summary>
        /// <value></value>
        public bool Volatile
        {
            get
            {
                return false;
            }
        }

        #endregion

        #region ICustomSerializable Members

        /// <summary>
        /// Deserialize data from a stream
        /// </summary>
        /// <param name="reader"></param>
        public void Deserialize(IPrimitiveReader reader)
        {
            reader.Response = SerializationResponse.Unhandled;
        }

        #endregion

    }
}