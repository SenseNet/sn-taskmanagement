using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.TaskManagement.TaskAgent;

namespace Tests
{
    [TestClass]
    public class ExecutorNameTests
    {
        [TestMethod]
        public void TestMethod1()
        {
            // anything.v11.222.333.444

            Assert.AreEqual("executorname", Tools.GetExecutorExeName("executorname"));
            Assert.AreEqual("executorname.", Tools.GetExecutorExeName("executorname."));
            Assert.AreEqual("executorname.1", Tools.GetExecutorExeName("executorname.1"));
            Assert.AreEqual("executorname.111", Tools.GetExecutorExeName("executorname.111"));
            Assert.AreEqual("executorname.111.222", Tools.GetExecutorExeName("executorname.111.222"));
            Assert.AreEqual("executorname.extension", Tools.GetExecutorExeName("executorname.extension"));
            Assert.AreEqual("executorname.v", Tools.GetExecutorExeName("executorname.v"));
            Assert.AreEqual("executorname.V", Tools.GetExecutorExeName("executorname.V"));
            Assert.AreEqual("executorname.VV", Tools.GetExecutorExeName("executorname.VV"));
            Assert.AreEqual("executorname.VV1", Tools.GetExecutorExeName("executorname.VV1"));
            Assert.AreEqual("executorname.VV1.2", Tools.GetExecutorExeName("executorname.VV1.2"));
            Assert.AreEqual("executorname.V.extension", Tools.GetExecutorExeName("executorname.V.extension"));
            Assert.AreEqual("executorname.v.extension", Tools.GetExecutorExeName("executorname.v.extension"));

            Assert.AreEqual("executorname", Tools.GetExecutorExeName("executorname.V1"));
            Assert.AreEqual("executorname", Tools.GetExecutorExeName("executorname.V111"));
            Assert.AreEqual("executorname", Tools.GetExecutorExeName("executorname.V111.2"));
            Assert.AreEqual("executorname", Tools.GetExecutorExeName("executorname.V111.222"));
            Assert.AreEqual("executorname", Tools.GetExecutorExeName("executorname.V111.222.3"));
            Assert.AreEqual("executorname", Tools.GetExecutorExeName("executorname.V111.222.333"));
            Assert.AreEqual("executorname", Tools.GetExecutorExeName("executorname.V111.222.333.4"));
            Assert.AreEqual("executorname", Tools.GetExecutorExeName("executorname.V111.222.333.444"));
            Assert.AreEqual("executorname.V111.222.333.444.5", Tools.GetExecutorExeName("executorname.V111.222.333.444.5"));
            Assert.AreEqual("executorname", Tools.GetExecutorExeName("executorname.v1"));
            Assert.AreEqual("executorname", Tools.GetExecutorExeName("executorname.v111"));
            Assert.AreEqual("executorname", Tools.GetExecutorExeName("executorname.v111.2"));
            Assert.AreEqual("executorname", Tools.GetExecutorExeName("executorname.v111.222"));
            Assert.AreEqual("executorname", Tools.GetExecutorExeName("executorname.v111.222.3"));
            Assert.AreEqual("executorname", Tools.GetExecutorExeName("executorname.v111.222.333"));
            Assert.AreEqual("executorname", Tools.GetExecutorExeName("executorname.v111.222.333.4"));
            Assert.AreEqual("executorname", Tools.GetExecutorExeName("executorname.v111.222.333.444"));

            Assert.AreEqual("executorname.V111.222.333.444.5", Tools.GetExecutorExeName("executorname.V111.222.333.444.5"));
            Assert.AreEqual("executorname.v111.222.333.444.5", Tools.GetExecutorExeName("executorname.v111.222.333.444.5"));

            Assert.AreEqual("executorname.V", Tools.GetExecutorExeName("executorname.V.V1.2"));
            Assert.AreEqual("executorname.V", Tools.GetExecutorExeName("executorname.V.V1.2"));
            Assert.AreEqual("executorname.V1.2", Tools.GetExecutorExeName("executorname.V1.2.V1.2.3.4"));
            Assert.AreEqual("executorname.V", Tools.GetExecutorExeName("executorname.V.v1.2"));
            Assert.AreEqual("executorname.V", Tools.GetExecutorExeName("executorname.V.v1.2"));
            Assert.AreEqual("executorname.V1.2", Tools.GetExecutorExeName("executorname.V1.2.v1.2.3.4"));
            Assert.AreEqual("executorname.v", Tools.GetExecutorExeName("executorname.v.V1.2"));
            Assert.AreEqual("executorname.v", Tools.GetExecutorExeName("executorname.v.V1.2"));
            Assert.AreEqual("executorname.v1.2", Tools.GetExecutorExeName("executorname.v1.2.V1.2.3.4"));
            Assert.AreEqual("executorname.v", Tools.GetExecutorExeName("executorname.v.v1.2"));
            Assert.AreEqual("executorname.v", Tools.GetExecutorExeName("executorname.v.v1.2"));
            Assert.AreEqual("executorname.v1.2", Tools.GetExecutorExeName("executorname.v1.2.v1.2.3.4"));
        }
    }
}
