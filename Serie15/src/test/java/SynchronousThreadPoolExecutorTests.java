import org.junit.Before;
import org.junit.Test;

import java.util.LinkedList;
import java.util.Queue;
import java.util.concurrent.atomic.AtomicLong;

import static org.junit.Assert.*;

public class SynchronousThreadPoolExecutorTests {

    private Queue<Exception> exceptionQueue;

    @Before
    public void setUp() throws Exception {
        exceptionQueue = new LinkedList<>();
    }

    @Test
    public void testHappyPath() throws Exception {
        SynchronousThreadPoolExecutor<String> executor = new SynchronousThreadPoolExecutor<>(1, 3600);

        assertEquals("Hello World", executor.execute(() -> "Hello World"));
    }

    @Test(expected = Exception.class)
    public void testResultException() throws Exception {
        SynchronousThreadPoolExecutor<String> executor = new SynchronousThreadPoolExecutor<>(1, 3600);

        executor.execute(() -> {
            throw new Exception();
        });
    }

    @Test()
    public void testShutdownWaitsForWorkToFinish() throws Exception {
        SynchronousThreadPoolExecutor<Integer> executor = new SynchronousThreadPoolExecutor<>(2, 3600);

        final AtomicLong exitTimeA = new AtomicLong();
        final AtomicLong exitTimeB = new AtomicLong();
        final AtomicLong shutdownExitTime = new AtomicLong();

        Thread tA = new Thread(() -> {
            try {
                Integer res = executor.execute(() -> {
                    Thread.sleep(1000);
                    exitTimeA.set(System.nanoTime());

                    return 1;
                });

                assertEquals(1, res.intValue());
            } catch (Exception e) {
                exceptionQueue.add(e);
            }
        });

        Thread tB = new Thread(() -> {
            try {
                Integer res = executor.execute(() -> {
                    Thread.sleep(1000);
                    exitTimeB.set(System.nanoTime());

                    return 2;
                });

                assertEquals(2, res.intValue());
            } catch (Exception e) {
                exceptionQueue.add(e);
            }
        });

        tA.start();
        tB.start();

        // give tA and tB some time to start doing their work
        Thread.sleep(5);

        // should block waiting for tA or tB to finish their work and then proceed
        executor.shutdown();
        shutdownExitTime.set(System.nanoTime());

        assertTrue(shutdownExitTime.get() > exitTimeA.get() && shutdownExitTime.get() > exitTimeB.get());
        assertEquals(0, exceptionQueue.size());
    }


}
