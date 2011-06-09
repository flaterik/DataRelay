using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

// Based on Justin Burtch testing framework 

namespace MySpace.Common.UnitTests
{
    public delegate void UnitTestLogger(string format, params object[] args);

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public abstract class TestAttributeBase : Attribute
    {
        public abstract TestDecorator GetTestDecorator();

        private MethodInfo _method;

        protected MethodInfo MethodInfo
        {
            get { return _method; }
        }

        object testClassInstance;

        public object TestClassInstance
        {
            get { return testClassInstance; }
            internal set { testClassInstance = value; }
        }

		public int Priority { get; set; }

        public virtual void WriteLineToUnitTest(string message, params object[] args)
        {
            UnitTestBase currentUnitTestClass = testClassInstance as UnitTestBase;
            if ( currentUnitTestClass == null ) return;

            TestContext currentContext = currentUnitTestClass.TestContext;
            if ( currentContext == null ) return;

            currentContext.WriteLine(message, args);
        }

        internal void SetMethodInfo(MethodInfo method)
        {
            _method = method;
        }
    }
}
