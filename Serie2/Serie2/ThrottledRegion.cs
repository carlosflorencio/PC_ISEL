using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serie2 {

    /*
    |--------------------------------------------------------------------------
    | Region for each key
    |--------------------------------------------------------------------------
    */

    internal class ThrottledRegionForKey {

        private readonly int _maxWaiting;
        private readonly int _waitTimeout;
        private readonly int _maxInside;

        // observed and changed outside the lock
        private volatile int _insideCount = 0;

        // changed inside the lock but observed outside of it
        private volatile int _waitingCount = 0;
        private readonly object _lock = new object();

        // changed and observed inside the lock
        private readonly LinkedList<bool> _waitingQueue = new LinkedList<bool>();

        public ThrottledRegionForKey(int maxInside, int maxWaiting, int waitTimeout) {
            this._maxInside = maxInside;
            this._maxWaiting = maxWaiting;
            this._waitTimeout = waitTimeout;
        }

        /*
        |--------------------------------------------------------------------------
        | TryEnter
        |--------------------------------------------------------------------------
        */

        public bool TryEnter() {
            // fast path
            if (_waitingCount == 0 && tryAcquireInside()) {
                return true;
            }

            // slow path
            lock (_lock) {
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
                LinkedListNode<bool> node = _waitingQueue.AddLast(false);
                int timeout = _waitTimeout;
                int lastTime = (timeout != Timeout.Infinite) ? Environment.TickCount : 0;

                do {
                    try {
                        SyncUtils.Wait(_lock, node, timeout);
                    } catch (ThreadInterruptedException e) {
                        _waitingCount -= 1;
                        _waitingQueue.Remove(node);

                        if (node.Value) {
                            Thread.CurrentThread.Interrupt();
                            return true;
                        }

                        throw;
                    }

                    if (node.Value) {
                        return true;
                    }

                    if (SyncUtils.AdjustTimeout(ref lastTime, ref timeout) == 0) {
                        _waitingCount -= 1;
                        _waitingQueue.Remove(node);
                        return false;
                    }
                } while (true);
            }
        }

        /*
        |--------------------------------------------------------------------------
        | Leave
        |--------------------------------------------------------------------------
        */

        public void leave() {
            bool alreadyDecremented = false;
            if (_waitingCount == 0) {
                Interlocked.Decrement(ref _insideCount);
                if (_waitingCount == 0) {
                    return;
                }

                alreadyDecremented = true;
            }

            lock (_lock) {
                // check again if no one is waiting
                LinkedListNode<bool> first = _waitingQueue.First;
                if (first == null) {
                    _insideCount--;
                    return;
                }

                // someone is waiting but no space
                if (alreadyDecremented && !tryAcquireInside()) {
                    return;
                }

                first.Value = true;
                _waitingCount -= 1;
                _waitingQueue.Remove(first);
                SyncUtils.Notify(_lock, first);
            }
        }

        private bool tryAcquireInside() {
            do {
                int observed = _insideCount;
                if (observed >= _maxInside) {
                    return false;
                }

                if (Interlocked.CompareExchange(ref _insideCount, observed + 1, observed) == observed) {
                    return true;
                }
            } while (true);
        }

    }


    public class ThrottledRegion {

        private readonly int _maxInside;
        private readonly int _maxWaiting;
        private readonly int _waitTimeout;
        private readonly ConcurrentDictionary<int, ThrottledRegionForKey> _keyToRegion;

        public ThrottledRegion(int maxInside, int maxWaiting, int waitTimeout) {
            this._maxInside = maxInside;
            this._maxWaiting = maxWaiting;
            this._waitTimeout = waitTimeout;
            this._keyToRegion = new ConcurrentDictionary<int, ThrottledRegionForKey>();
        }

        public bool TryEnter(int key) {
            var region = _keyToRegion.GetOrAdd(key,
                new ThrottledRegionForKey(this._maxInside, this._maxWaiting, this._waitTimeout));

            return region.TryEnter();
        }


        public void Leave(int key) {
            _keyToRegion[key].leave();
        }

    }

}