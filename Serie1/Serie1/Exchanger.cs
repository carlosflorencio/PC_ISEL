using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serie1 {

    class ItemContainer<T> {

        public T First;
        public T Second;
        public bool Completed;

    }

   public class Exchanger<T> {

        private ItemContainer<T> _lastItemRef;
        private bool _isWaiting = false;
        private readonly object _lock = new object();

        public T Exchange(T mine, int timeout) {
            lock (_lock) {
                if (_isWaiting) {
                    _lastItemRef.Second = mine;
                    _lastItemRef.Completed = true;
                    _isWaiting = false;
                    Monitor.PulseAll(_lock);
                    return _lastItemRef.First;
                }

                if (timeout == 0)
                    throw new TimeoutException();

                int lastTime = (timeout != Timeout.Infinite) ? Environment.TickCount : 0;

                var item = new ItemContainer<T>(); //save the item ref in the thread stack
                _lastItemRef = item; // replace the global last item ref
                item.First = mine;
                item.Second = default(T); // should be replaced in the future
                _isWaiting = true;


                do {
                    try {
                        Monitor.Wait(_lock, timeout);
                    } catch (ThreadInterruptedException) {

                        if (item.Completed) {
                            // Should we propagate the interrupt()?
                            return item.Second;
                        } else {
                            _isWaiting = false;
                            throw;
                        }
                    }

                    if (item.Completed) {
                        return item.Second;
                    }

                    if (SyncUtils.AdjustTimeout(ref lastTime, ref timeout) == 0) {
                        _isWaiting = false;
                        throw new TimeoutException();
                    }
                } while (true);
            }
        }

    }

}