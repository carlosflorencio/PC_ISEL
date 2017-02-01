import java.util.LinkedList;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.ReentrantLock;

public class ThreadPool<T> {

    private final int _maxThreads;
    private final int _keepAlive;
    private final ReentrantLock _lock;

    private LinkedList<Item<T>> _waitingItems = new LinkedList<>();
    private LinkedList<WorkThread> _threadList = new LinkedList<WorkThread>();
    private int _waitingThreads = 0;
    private Condition _waitingThreadsCondition;
    private boolean _shutdown = false;
    private Condition _shutdownCondition = null;

    public ThreadPool(int maxThreads, int keepAlive, ReentrantLock lock, Condition _shutdownCondition) {
        this._maxThreads = maxThreads;
        this._keepAlive = keepAlive;
        this._lock = lock;
        this._waitingThreadsCondition = lock.newCondition();
        this._shutdownCondition = _shutdownCondition;
    }

    /**
     * Called with lock!
     *
     * @param item
     */
    public void resolveItem(Item<T> item) {
        _waitingItems.addLast(item);

        if (_waitingThreads > 0) {
            _waitingThreadsCondition.signal();
            return;
        }

        if (_threadList.size() < _maxThreads) {
            WorkThread thread = new WorkThread();
            _threadList.add(thread);
            thread.start();
        }

        // no space? all threads are full?!?!
        // no problem, when a thread finishes is job
        // will look for more jobs before blocking
    }

    /**
     * Called with lock!
     *
     * @param item
     */
    public void cancelItem(Item<T> item) {
        _waitingItems.remove(item);
    }

    /**
     * Called with lock!
     */
    public void shutdown() {
        this._shutdown = true;

        if (_waitingThreads > 0) { // wake sleeping threads
            _waitingThreadsCondition.signalAll();
        }

        // all running threads will verify shutdown condition after doing the work
    }

    /**
     * Called with lock!
     *
     * @return
     */
    public boolean isFinished() {
        return _waitingItems.size() == 0 && _threadList.size() == 0;
    }

    private class WorkThread extends Thread {

        @Override
        public void run() {
            _lock.lock();
            try {
                worker:
                do {
                    long nanos = _keepAlive;

                    while (_waitingItems.size() > 0) {
                        Item<T> item = _waitingItems.removeFirst();
                        item.resolving();

                        _lock.unlock();

                        T result = null;
                        Exception ex = null;
                        try { // execute the function outside of the lock
                            result = item.function().call();
                        } catch (Exception e) {
                            ex = e;
                        }

                        _lock.lock();
                        if (ex != null)
                            item.complete(ex);
                        else
                            item.complete(result);

                        item.condition().signal();
                    }

                    if (_shutdown) { // do not sleep if we have to shutdown
                        this.submitFinish();
                        break;
                    }

                    do {
                        // no more waiting items? lets wait
                        try {
                            _waitingThreads++;
                            nanos = _waitingThreadsCondition.awaitNanos(nanos);
                        } catch (InterruptedException e) {
                            _waitingThreads--;

                            interrupt();
                        }

                        // Wake up
                        --_waitingThreads;

                        if (_waitingItems.size() > 0)
                            break; // Go to worker loop to do some work

                        if (nanos <= 0L) {
                            break worker; // finish this thread by breaking the worker loop
                        }

                        if (_shutdown) { // prevent another sleep if we have to shutdown
                            this.submitFinish();
                            break;
                        }
                    } while (true);

                } while (true);

                _threadList.remove(this); // remove me before exit
                if (_shutdown) {
                    this.submitFinish();
                }
            } finally {
                _lock.unlock();
            }
        }

        private void submitFinish() {
            if (_threadList.size() == 0) {
                _shutdownCondition.signal();
            }
        }
    }
}