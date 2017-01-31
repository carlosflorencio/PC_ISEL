using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serie1;

namespace Serie1Tests
{
    [TestClass]
    public class BusTests
    {
        /*
		|--------------------------------------------------------------------------
		| Test Happy Path
		|--------------------------------------------------------------------------
		*/

        [TestMethod]
        public void Test_HappyPath()
        {
            var bus = new Bus(1);
            var msgCompare = "Teste";

            Thread t1 = new Thread(() =>
            {
                bus.SubscribeEvent<String>((msg) =>
                {
                    Assert.AreEqual(msgCompare, msg);
                });
            });

            t1.Start();
            Thread.Sleep(100); // wait to subscribe
            bus.PublishEvent(msgCompare);

            bus.Shutdown();
            t1.Join();
        }

        class TestType {

            public int value;

            public TestType(int val) {
                this.value = val;
            }

        }

        [TestMethod]
        public void Test_MultipleTypes()
        {
            var bus = new Bus(1);
            var msgCompare = "Teste";
            var typeCompare = new TestType(2);
            int run = 0;
            Thread t1 = new Thread(() =>
            {
                bus.SubscribeEvent<String>((msg) =>
                {
                    Assert.AreEqual(msgCompare, msg);
                    run++;
                });
            });

            Thread t2 = new Thread(() =>
            {
                bus.SubscribeEvent<TestType>((type) =>
                {
                    Assert.AreEqual(2, type.value);
                    run++;
                });
            });

            t1.Start();
            t2.Start();
            Thread.Sleep(100); // wait for subscribes
            bus.PublishEvent(msgCompare);
            bus.PublishEvent(typeCompare);

            bus.Shutdown();
            t1.Join();
            t2.Join();

            Assert.AreEqual(2, run);
        }
    }
}
