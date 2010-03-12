using System;
using System.Collections.Generic;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    [Obsolete("This class is obsolete; use Filter class instead", true)]
    public class CriterionList : List<Criterion>, IVersionSerializable
    {
        #region Data Members
        private bool satisfyAny;
        /// <summary>
        /// If true will serve as OR operator; otherwise as AND operator
        /// </summary>
        public bool SatisfyAny
        {
            get
            {
                return satisfyAny;
            }
            set
            {
                satisfyAny = value;
            }
        }
        #endregion

        #region Ctors
        public CriterionList()
        {
            Init(true);
        }

        public CriterionList(bool satisfyAny)
        {
            Init(satisfyAny);
        }

        private void Init(bool satisfyAny)
        {
            this.satisfyAny = satisfyAny;
        }

        #endregion

        #region IVersionSerializable Members
        public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
        {
            //List
            if (Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)Count);
                foreach (Criterion criterion in this)
                {
                    criterion.Serialize(writer);
                }
                //SatisfyAny
                writer.Write(satisfyAny);
            }
        }

        public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
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

        public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
        {
            //List
            ushort count = reader.ReadUInt16();

            if (count > 0)
            {
                Criterion criteria;
                for (ushort i = 0; i < count; i++)
                {
                    criteria = new Criterion();
                    criteria.Deserialize(reader);
                    Add(criteria);
                }

                //SatisfyAny
                satisfyAny = reader.ReadBoolean();
            }
        }

        #endregion

    }
}