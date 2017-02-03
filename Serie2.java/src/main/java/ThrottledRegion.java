import java.util.LinkedList;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ConcurrentMap;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.locks.Condition;
import java.util.concurrent.locks.Lock;
import java.util.concurrent.locks.ReentrantLock;

public class ThrottledRegion {

    private static class Item {
        public final Condition condition;
        public boolean done = false;

        public Item(Condition condition) {
            this.condition = condition;
        }
    }

    private class ThrottledRegionForKey {

        private final AtomicInteger _insideCount = new AtomicInteger(0);

        // changed inside the lock but observed outside of it
        private volatile int _waitingCount = 0;
        private final Lock _lock = new ReentrantLock();

        // changed and observed inside the lock
        private final LinkedList<Item> _waitingQueue = new LinkedList<>();


        /*
        |--------------------------------------------------------------------------
        | Try Enter
        |--------------------------------------------------------------------------
        */
        public boolean tryEnter() throws InterruptedException {
            // fast path
            if (_waitingCount == 0 && tryAcquireInside()) {
                return true;
            }

            // slow path
            _lock.lock();
            try {
                // if full, quit immediately
                if (_waitingCount == _maxWaiting) {
                    return false;
                }

                // mark as waiting
                // until this point, this thread can be overtaken by another one in the fast path
                _waitingCount += 1;

                // If first in waiting line, check again if region is not full
                if (_waitingCount == 1 && tryAcquireInside()) {
                    _waitingCount -= 1;
                    return true;
                }

                // waiting..
                Item item = new Item(_lock.newCondition());
                _waitingQueue.addLast(item);
                long timeout = _waitTimeout;
                do {
                    try {
                        timeout = item.condition.awaitNanos(timeout);
                    } catch (InterruptedException e) {
                        if (item.done) {
                            Thread.currentThread().interrupt();
                            return true;
                        }

                        _waitingCount -= 1;
                        _waitingQueue.remove(item);
                        throw e;
                    }

                    if (item.done) {
                        return true;
                    }

                    if (timeout <= 0) {
                        _waitingCount -= 1;
                        _waitingQueue.remove(item);
                        return false;
                    }
                } while (true);
            } finally {
                _lock.unlock();
            }
        }

        /*
        |--------------------------------------------------------------------------
        | Leave
        |--------------------------------------------------------------------------
        */
        public void leave() {
            boolean alreadyDecremented = false;
            if (_waitingCount == 0) {
                _insideCount.decrementAndGet();
                if (_waitingCount == 0) {
                    return;
                }

                alreadyDecremented = true;
            }

            _lock.lock();
            try {
                // check again if no one is waiting
                Item first = _waitingQueue.peekFirst();
                if (first == null) {
                    _insideCount.decrementAndGet();
                    return;
                }

                // someone is waiting but no space
                if (alreadyDecremented && !tryAcquireInside()) {
                    return;
                }

                first.done = true;
                _waitingCount -= 1;
                _waitingQueue.remove(first);
                first.condition.signal();
            } finally {
                _lock.unlock();
            }
        }

        private boolean tryAcquireInside() {
            do {
                int observed = _insideCount.get();
                if (observed >= _maxInside) {
                    return false;
                }
                if (_insideCount.compareAndSet(observed, observed + 1)) {
                    return true;
                }
            } while (true);
        }

    }

    private final int _maxInside;
    private final int _maxWaiting;
    private final int _waitTimeout;
    private final ConcurrentMap<Integer, ThrottledRegionForKey> _keyToRegion = new ConcurrentHashMap<>();

    public ThrottledRegion(int maxInside, int maxWaiting, int waitTimeout) {
        this._maxInside = maxInside;
        this._maxWaiting = maxWaiting;
        this._waitTimeout = waitTimeout; // convert millis to seconds?
    }

    public boolean tryEnter(int key) throws InterruptedException {
        return _keyToRegion.computeIfAbsent(key, k -> new ThrottledRegionForKey()).tryEnter();
    }

    public void leave(int key) {
        _keyToRegion.get(key).leave();
    }
}
