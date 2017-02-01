import org.junit.Assert;
import org.junit.Test;

import java.util.Random;

public class ConcurrentQueueTests {

    //
    // Provided test method
    //

    @Test
    public void testMichaelScottQueue() {

        final int CONSUMER_THREADS = 2;
        final int PRODUCER_THREADS = 1;
        final int MAX_PRODUCE_INTERVAL = 100;
        final int MAX_CONSUME_TIME = 25;
        final int FAILURE_PERCENT = 5;
        final int JOIN_TIMEOUT = 100;
        final int RUN_TIME = 5 * 1000;
        final int POLL_INTERVAL = 20;


        Thread[] consumers = new Thread[CONSUMER_THREADS];
        Thread[] producers = new Thread[PRODUCER_THREADS];
        final ConcurrentQueue<String> msqueue = new ConcurrentQueue<String>();
        final int[] productions = new int[PRODUCER_THREADS];
        final int[] consumptions = new int[CONSUMER_THREADS];
        final int[] failuresInjected = new int[PRODUCER_THREADS];
        final int[] failuresDetected = new int[CONSUMER_THREADS];

        System.out.printf("%n%n--> Start test of Michael-Scott queue in producer/consumer context...%n%n");

        // create and start the consumer threads.
        for (int i = 0; i < CONSUMER_THREADS; i++) {
            final int tid = i;
            consumers[i] = new Thread(() -> {
                Random rnd = new Random(Thread.currentThread().getId());
                int count = 0;

                System.out.printf("-->c#%d starts...%n", tid);
                do {
                    try {
                        String data = msqueue.take();
                        if (!data.equals("hello")) {
                            failuresDetected[tid]++;
                            System.out.printf("[f#%d]", tid);
                        }

                        if (++count % 10 == 0)
                            System.out.printf("[c#%d]", tid);

                        // Simulate the time needed to process the data.

                        if (MAX_CONSUME_TIME > 0)
                            Thread.sleep(rnd.nextInt(MAX_CONSUME_TIME));

                    } catch (InterruptedException ie) {
                        //do {} while (tid == 0);
                        break;
                    }
                } while (true);

                // display the consumer thread's results.
                System.out.printf("%n<--c#%d exits, consumed: %d, failures: %d",
                        tid, count, failuresDetected[tid]);
                consumptions[tid] = count;
            });
            consumers[i].setDaemon(true);
            consumers[i].start();
        }

        // create and start the producer threads.
        for (int i = 0; i < PRODUCER_THREADS; i++) {
            final int tid = i;
            producers[i] = new Thread( () -> {
                Random rnd = new Random(Thread.currentThread().getId());
                int count = 0;

                System.out.printf("-->p#%d starts...%n", tid);
                do {
                    String data;

                    if (rnd.nextInt(100) >= FAILURE_PERCENT) {
                        data = "hello";
                    } else {
                        data = "HELLO";
                        failuresInjected[tid]++;
                    }

                    // put a data item
                    msqueue.put(data);

                    // increment request count and periodically display the "alive" menssage.
                    if (++count % 10 == 0)
                        System.out.printf("[p#%d]", tid);

                    // production interval.

                    try {
                        Thread.sleep(rnd.nextInt(MAX_PRODUCE_INTERVAL));
                    } catch (InterruptedException ie) {
                        //do {} while (tid == 0);
                        break;
                    }
                } while (true);

                // display the producer thread's results
                System.out.printf("%n<--p#%d exits, produced: %d, failures: %d",
                        tid, count, failuresInjected[tid]);
                productions[tid] = count;
            });
            producers[i].setDaemon(true);
            producers[i].start();
        }

        // run the test RUN_TIME milliseconds.

        sleepUninterruptibly(RUN_TIME);

        // interrupt all producer threads and wait for for until each one finished.
        int stillRunning = 0;
        for (int i = 0; i < PRODUCER_THREADS; i++) {
            producers[i].interrupt();
            if (!joinUninterruptibly(producers[i], JOIN_TIMEOUT))
                stillRunning++;

        }

        // wait until the queue is empty
        while (!msqueue.isEmpty()) {
            sleepUninterruptibly(POLL_INTERVAL);
        }

        // interrupt each consumer thread and wait for a while until each one finished.
        for (int i = 0; i < CONSUMER_THREADS; i++) {
            consumers[i].interrupt();
            if (!joinUninterruptibly(consumers[i], JOIN_TIMEOUT))
                stillRunning++;
        }

        // if any thread failed to fisnish, something is wrong.
        if (stillRunning > 0) {
            System.out.printf("%n*** failure: %d thread(s) did answer to interrupt%n", stillRunning);
            Assert.fail();
        }

        // compute and display the results.

        long sumProductions = 0, sumFailuresInjected = 0;
        for (int i = 0; i < PRODUCER_THREADS; i++) {
            sumProductions += productions[i];
            sumFailuresInjected += failuresInjected[i];
        }
        long sumConsumptions = 0, sumFailuresDetected = 0;
        for (int i = 0; i < CONSUMER_THREADS; i++) {
            sumConsumptions += consumptions[i];
            sumFailuresDetected += failuresDetected[i];
        }
        System.out.printf("%n%n<-- successful: %d/%d, failed: %d/%d%n",
                sumProductions, sumConsumptions, sumFailuresInjected, sumFailuresDetected);

        Assert.assertTrue(sumProductions == sumConsumptions && sumFailuresInjected == sumFailuresDetected);
    }

    //
    // Test code
    //

    // Auxiliary methods
    private static void sleepUninterruptibly(long milliseconds) {
        long expiresAt = System.currentTimeMillis() + milliseconds;
        do {
            try {
                Thread.sleep(milliseconds);
                break;
            } catch (InterruptedException ie) {}
            milliseconds = expiresAt - System.currentTimeMillis();
        } while (milliseconds > 0);
    }

    private static boolean joinUninterruptibly(Thread toJoin, long timeout) {
        do {
            try {
                toJoin.join(timeout);
                return !toJoin.isAlive();
            } catch (InterruptedException ie) {}
        } while (true);
    }
}
