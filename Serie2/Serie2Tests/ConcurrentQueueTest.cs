using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serie2;

namespace Serie2Tests
{
    [TestClass]
    public class ConcurrentQueueTest
    {
        private static void sleepUninterruptibly(int milliseconds)
        {
            int expiresAt = Environment.TickCount + milliseconds;
            do
            {
                try
                {
                    Thread.Sleep(milliseconds);
                    break;
                }
                catch (ThreadInterruptedException ie)
                {
                }
                milliseconds = expiresAt - Environment.TickCount;
            } while (milliseconds > 0);
        }

        private static bool joinUninterruptibly(Thread toJoin, int timeout)
        {
            do
            {
                try
                {
                    toJoin.Join(timeout);
                    return !toJoin.IsAlive;
                }
                catch (ThreadInterruptedException ie)
                {
                }
            } while (true);
        }

        //
        // Test method.
        //

        public static bool testMichaelScottQueue()
        {
            int CONSUMER_THREADS = 2;
            int PRODUCER_THREADS = 1;
            int MAX_PRODUCE_INTERVAL = 100;
            int MAX_CONSUME_TIME = 25;
            int FAILURE_PERCENT = 5;
            int JOIN_TIMEOUT = 100;
            int RUN_TIME = 5 * 1000;
            int POLL_INTERVAL = 20;


            Thread[] consumers = new Thread[CONSUMER_THREADS];
            Thread[] producers = new Thread[PRODUCER_THREADS];
            ConcurrentQueue<String> msqueue = new ConcurrentQueue<String>();
            int[] productions = new int[PRODUCER_THREADS];
            int[] consumptions = new int[CONSUMER_THREADS];
            int[] failuresInjected = new int[PRODUCER_THREADS];
            int[] failuresDetected = new int[CONSUMER_THREADS];

            Trace.WriteLine("\n\n--> Start test of Michael-Scott queue in producer/consumer context...\n\n");
            Trace.WriteLine(default(string));

            // create and start the consumer threads.		
            for (int i = 0; i < CONSUMER_THREADS; i++)
            {
                int tid = i;
                consumers[i] = new Thread(() =>
                {
                    Random rnd = new Random(Thread.CurrentThread.ManagedThreadId);
                    int count = 0;

                    Trace.WriteLine("-->c#" + tid + " starts...\n");
                    do
                    {
                        try
                        {
                            String data = msqueue.Take();
                            if (!data.Equals("hello"))
                            {
                                failuresDetected[tid]++;
                                Trace.WriteLine("[f#" + tid + "]");
                            }

                            if (++count % 10 == 0)
                                Trace.WriteLine("[c#" + tid + "]");

                            // Simulate the time needed to process the data.

                            if (MAX_CONSUME_TIME > 0)
                                Thread.Sleep(rnd.Next(MAX_CONSUME_TIME));
                        }
                        catch (ThreadInterruptedException ie)
                        {
                            //do {} while (tid == 0);
                            break;
                        }
                    } while (true);

                    // display the consumer thread's results.				
                    Trace.WriteLine("\n<--c#" + tid + " exits, consumed: " + count + ", failures: " +
                                    failuresDetected[tid]);
                    consumptions[tid] = count;
                });
                //consumers[i].SetDaemon(true);
                consumers[i].Start();
            }

            // create and start the producer threads.		
            for (int i = 0; i < PRODUCER_THREADS; i++)
            {
                int tid = i;
                producers[i] = new Thread(() =>
                {
                    Random rnd = new Random(Thread.CurrentThread.ManagedThreadId);
                    int count = 0;

                    Trace.WriteLine("-->p#" + tid + " starts...\n");
                    do
                    {
                        String data;

                        if (rnd.Next(100) >= FAILURE_PERCENT)
                        {
                            data = "hello";
                        }
                        else
                        {
                            data = "HELLO";
                            failuresInjected[tid]++;
                        }

                        // enqueue a data item
                        msqueue.Put(data);

                        // increment request count and periodically display the "alive" menssage.
                        if (++count % 10 == 0)
                            Trace.WriteLine("[p#" + tid + "]");

                        // production interval.

                        try
                        {
                            Thread.Sleep(rnd.Next(MAX_PRODUCE_INTERVAL));
                        }
                        catch (ThreadInterruptedException ie)
                        {
                            //do {} while (tid == 0);
                            break;
                        }
                    } while (true);

                    // display the producer thread's results
                    Trace.WriteLine("\n<--p#" + tid + " exits, produced: " + count + ", failures: " +
                                    failuresInjected[tid]);
                    productions[tid] = count;
                });
                //producers[i].setDaemon(true);			
                producers[i].Start();
            }

            // run the test RUN_TIME milliseconds.

            sleepUninterruptibly(RUN_TIME);

            // interrupt all producer threads and wait for for until each one finished. 
            int stillRunning = 0;
            for (int i = 0; i < PRODUCER_THREADS; i++)
            {
                producers[i].Interrupt();
                if (!joinUninterruptibly(producers[i], JOIN_TIMEOUT))
                    stillRunning++;
            }

            // wait until the queue is empty 
            while (!msqueue.IsEmpty())
            {
                sleepUninterruptibly(POLL_INTERVAL);
            }

            // interrupt each consumer thread and wait for a while until each one finished.
            for (int i = 0; i < CONSUMER_THREADS; i++)
            {
                consumers[i].Interrupt();
                if (!joinUninterruptibly(consumers[i], JOIN_TIMEOUT))
                    stillRunning++;
            }

            // if any thread failed to fisnish, something is wrong.
            if (stillRunning > 0)
            {
                Trace.WriteLine("\n*** failure: " + stillRunning + " thread(s) did answer to interrupt\n");
                return false;
            }

            // compute and display the results.

            long sumProductions = 0, sumFailuresInjected = 0;
            for (int i = 0; i < PRODUCER_THREADS; i++)
            {
                sumProductions += productions[i];
                sumFailuresInjected += failuresInjected[i];
            }
            long sumConsumptions = 0, sumFailuresDetected = 0;
            for (int i = 0; i < CONSUMER_THREADS; i++)
            {
                sumConsumptions += consumptions[i];
                sumFailuresDetected += failuresDetected[i];
            }
            Trace.WriteLine("\n\n<-- successful: " + sumProductions + "/" + sumConsumptions + ", failed: " + sumFailuresInjected + "/" + sumFailuresDetected + "\n");
            Assert.AreEqual(sumProductions, sumConsumptions);
            Assert.AreEqual(sumFailuresInjected, sumFailuresDetected);

            return sumProductions == sumConsumptions && sumFailuresInjected == sumFailuresDetected;
        }

        [TestMethod]
        public void TestMichaelScottQueue()
        {
            Assert.IsTrue(testMichaelScottQueue());
        }
    }
}
