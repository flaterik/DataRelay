using System;
using System.Collections.Generic;
using System.Text;

// Based on Justin Burtch testing framework 

namespace MySpace.Common.UnitTests
{
    public abstract class TestDecorator
    {
        public virtual void BeforeTestRun()
        {
        }

        public virtual void AfterTestRun()
        {
        }
    }
}
