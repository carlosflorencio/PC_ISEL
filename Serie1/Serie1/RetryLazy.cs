using System;
using System.Collections.Generic;
using System.Threading;

namespace Serie1 {

    public class RetryLazy<T> where T : class {

        private readonly Func<T> _provider;
        private int _maxRetries;
        private T _result;

        //signaling
        private readonly object _lock = new object();
        private readonly LinkedList<int> _queue = new LinkedList<int>();
        private bool _isBeingResolved = false;

        public RetryLazy(Func<T> provider, int maxRetries) {
            this._provider = provider;
            this._maxRetries = maxRetries;
        }

        public T Value {
            get {
                lock (_lock) {
                    if (_result != null)
                        return _result;

                    if (_maxRetries == 0)
                        throw new InvalidOperationException();


                    if (_isBeingResolved) {
                        var node = _queue.AddLast(Thread.CurrentThread.ManagedThreadId);

                        do {
                            try {
                                Monitor.Wait(_lock);

                                if (node == _queue.First && _isBeingResolved == false) {
                                    _queue.Remove(node);
                                    break;
                                }
                            } catch (ThreadInterruptedException) {
                                _queue.Remove(node);
                                ConditionalNotifyAll();
                                throw;
                            }
                        } while (true);


                        if (_result != null) {
                            ConditionalNotifyAll();
                            return _result;
                        }


                        if (_maxRetries == 0) {
                            ConditionalNotifyAll();
                            throw new InvalidOperationException();
                        }
                    }

                    _isBeingResolved = true;
                }

                T temp;
                try {
                    temp = _provider();
                } catch (Exception) {
                    lock (_lock) {
                        _maxRetries--;
                        _isBeingResolved = false;
                        ConditionalNotifyAll();
                        throw;
                    }
                }

                lock (_lock) {
                    _result = temp;
                    _isBeingResolved = false;

                    ConditionalNotifyAll();

                    return _result;
                }
            }
            set { }
        }

        private void ConditionalNotifyAll() {
            if (_queue.Count > 0)
                Monitor.PulseAll(_lock);
        }

    }

}