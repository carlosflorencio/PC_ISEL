using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serie1;

namespace Serie1Tests {


    [TestClass]
    public class RetryLazyTests {

        private LinkedList<Exception> _exList;
        private RetryLazy<string> _lazy;

        [TestInitialize]
        public void Setup() {
            _exList = new LinkedList<Exception>();
        }

        /*
		|--------------------------------------------------------------------------
		| Test Happy Path for one fetch
		|--------------------------------------------------------------------------
		*/

        [TestMethod]
        public void Test_SingleFetch() {
            _lazy = new RetryLazy<string>(() => "done", 1);

            Assert.AreSame("done", _lazy.Value);
        }

        /*
		|--------------------------------------------------------------------------
		| Test No more retries to fetch, should throw exceptions
		|--------------------------------------------------------------------------
		*/

        [TestMethod]
        public void Test_NoMoreRetries() {
            int retries = 3;
            _lazy = new RetryLazy<string>(() => { throw new Exception("Yo, i'm out."); }, retries);

            for (int i = 0; i < retries; i++) {
                try {
                    var res = _lazy.Value;
                } catch (Exception e) {
                    Assert.AreEqual(e.Message, "Yo, i'm out.");
                    _exList.AddLast(e);
                }
            }

            Assert.AreEqual(retries, _exList.Count);

            try {
                var r = _lazy.Value;
                Assert.Fail("InvalidOperationException expected!");
            } catch (Exception e) {
                Assert.AreEqual(typeof(InvalidOperationException), e.GetType());
            }
        }

        /*
		|--------------------------------------------------------------------------
		| Test Multiple Threads (with delay)
		|--------------------------------------------------------------------------
		*/

        [TestMethod]
        public void Test_MultipleThreadsWithSomeDelay() {
            int threads = 50;
            int count = 0;
            int retries = 10;
            _lazy = new RetryLazy<string>(() => {
                Thread.Sleep(200);

                if (count++ < retries) // first tries should fail
                    throw new Exception("I'm mad..");

                return "done";
            }, retries + 1);


            List<Thread> thList = new List<Thread>(threads);
            for (int i = 0; i < threads; i++) {
                thList.Add(new Thread(() => {
                    try {
                        Assert.AreEqual("done", _lazy.Value);
                    } catch (Exception e) {
                        _exList.AddLast(e);
                    }
                }));
            }

            thList.ForEach((t) => t.Start());
            thList.ForEach((t) => t.Join());

            Assert.AreEqual(retries, _exList.Count);
        }

    }

}