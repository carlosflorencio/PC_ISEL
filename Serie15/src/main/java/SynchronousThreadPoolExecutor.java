import java.util.concurrent.Callable;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.ReentrantLock;

public class SynchronousThreadPoolExecutor<T> {

    private ReentrantLock _lock = new ReentrantLock();

    private ThreadPool<T> _threadPool;
    private boolean shutdown = false;
    private Condition _shutdownCondition = _lock.newCondition();

    public SynchronousThreadPoolExecutor(int maxPoolSize, int keepAliveTime) {
        this._threadPool = new ThreadPool<T>(maxPoolSize, keepAliveTime, _lock, _shutdownCondition);
    }

    public T execute(Callable<T> toCall) throws Exception {
        _lock.lock();

        try {
            if (shutdown)
                throw new IllegalStateException();

            Condition condition = _lock.newCondition();

            Item<T> item = new Item<T>(toCall, condition);
            _threadPool.resolveItem(item);

            do {
                try {
                    condition.await();
                } catch (InterruptedException e) {

                    if (item.canBeCanceled()) {
                        _threadPool.cancelItem(item);
                    }

                    throw e;
                }

                if (item.isCompleted()) {
                    return item.result();
                }

            } while (true);

        } finally {
            _lock.unlock();
        }
    }

    public void shutdown() {
        _lock.lock();
        try {
            shutdown = true;

            _threadPool.shutdown();

            if (_threadPool.isFinished()) // can be already finished
                return;

            do {
                try {
                    _shutdownCondition.await();
                } catch (InterruptedException e) {
                    // ignore interrupt?!
                }

                if (_threadPool.isFinished())
                    return;
            } while (true);

        } finally {
            _lock.unlock();
        }
    }
}
