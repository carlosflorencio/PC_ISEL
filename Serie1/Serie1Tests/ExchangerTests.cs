using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serie1;

namespace Serie1Tests {

    [TestClass]
    public class ExchangerTests {

        private Exchanger<string> exchanger;

        [TestInitialize]
        public void Setup() {
            exchanger = new Exchanger<string>();
        }

        /*
		|--------------------------------------------------------------------------
		| Test Happy Path for one exchange
		|--------------------------------------------------------------------------
		*/

        [TestMethod]
        public void Test_BasicExchangeHappyPath() {
            string template = "I'm from thread";
            string message1 = null, message2 = null;

            Thread thread1 = new Thread(() => { message1 = exchanger.Exchange(template + 1, 2000); });

            Thread thread2 = new Thread(() => { message2 = exchanger.Exchange(template + 2, 2000); });

            thread1.Start();
            thread2.Start();

            thread1.Join();
            thread2.Join();

            Assert.AreEqual(template + 2, message1);
            Assert.AreEqual(template + 1, message2);
        }

        /*
		|--------------------------------------------------------------------------
		| Test Multiple threads exchanging & 1 timeout
		|--------------------------------------------------------------------------
		*/

        [TestMethod]
        public void Test_MultipleThreadsExchangingAnd1Timeout() {
            string template = "I'm from thread";
            string message1 = null;
            string message2 = null;
            string message3 = null;

            Thread t1 = new Thread(() => { message1 = exchanger.Exchange(template + 1, 2000); });
            Thread t2 = new Thread(() => { message2 = exchanger.Exchange(template + 2, 2000); });
            Thread t3 = new Thread(() => {
                try {
                    Thread.Sleep(1000); //wait for the other threads
                    message3 = exchanger.Exchange(template + 3, 1000);
                    Assert.Fail();
                } catch (TimeoutException) {}
            });

            t1.Start();
            t2.Start();
            t3.Start();
            t1.Join();
            t2.Join();
            t3.Join();

            Assert.AreEqual(template + 2, message1);
            Assert.AreEqual(template + 1, message2);
        }

    }

}