using System.Text;
using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class TagSort : IVersionSerializable
    {
        #region Data Members

        public string TagName { get; set; }

        public bool IsTag { get; set; }

        public SortOrder SortOrder { get; set; }

        #endregion

        #region Ctors
        public TagSort()
        {
            Init(null, true, null);
        }

        public TagSort(string tagName, SortOrder sortOrder)
        {
            Init(tagName, true, sortOrder);
        }

        public TagSort(string tagName, bool isTag, SortOrder sortOrder)
        {
            Init(tagName, isTag, sortOrder);
        }

        private void Init(string tagName, bool isTag, SortOrder sortOrder)
        {
            this.TagName = tagName;
            this.IsTag = isTag;
            this.SortOrder = sortOrder;
        }
        #endregion

        #region Methods

        public override string ToString()
        {
            var stb = new StringBuilder();
            stb.Append("(").Append("TagName: ").Append(TagName).Append("),");
            stb.Append("(").Append("IsTag: ").Append(IsTag).Append("),");
            stb.Append("(").Append("SortOrder: ").Append(SortOrder.ToString()).Append("),");
            return stb.ToString();
        }

        #endregion

        #region IVersionSerializable Members
        public void Serialize(IPrimitiveWriter writer)
        {
            //TagName
            writer.Write(TagName);

            //IsTag
            writer.Write(IsTag);

            //SortOrder
            SortOrder.Serialize(writer);
        }

        public void Deserialize(IPrimitiveReader reader, int version)
        {
            Deserialize(reader);
        }

        public int CurrentVersion
        {
            get
            {
                return 1;
            }
        }

        public bool Volatile
        {
            get
            {
                return false;
            }
        }
        #endregion

        #region ICustomSerializable Members

        public void Deserialize(IPrimitiveReader reader)
        {
            //TagName
            TagName = reader.ReadString();

            //IsTag
            IsTag = reader.ReadBoolean();

            //SortOrder
            SortOrder = new SortOrder();
            SortOrder.Deserialize(reader);
        }

        #endregion
    }
}
