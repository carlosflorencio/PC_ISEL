using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ServidorTAP {

    internal class LoggerThread {

        // Thread safe ConcurrentQueue with blocking functionality
        private BlockingCollection<string> _queue;
        private TextWriter _writer;
        private Thread _thread;
        private CancellationTokenSource _cts;

        public LoggerThread(TextWriter w) {
            this._queue = new BlockingCollection<string>(new ConcurrentQueue<string>());
            this._writer = w;
            this._cts = new CancellationTokenSource();
        }

        public LoggerThread() : this(Console.Out) {
        }

        public void Start() {
            // Foreground thread because we want to modify it's priority
            // if not, this should run on a worker thread in the ThreadPool
            _thread = new Thread(LogProcessor);
            _thread.Name = "Logger Thread";
            _thread.Priority = ThreadPriority.Lowest;
            _thread.Start();
        }

        /*
        |--------------------------------------------------------------------------
        | Add Messages
        |--------------------------------------------------------------------------
        */

        public void Add(string msg) {
            if (_thread.IsAlive)
                _queue.Add(msg); // thread safe & non-blocking!
        }


        /*
        |--------------------------------------------------------------------------
        | Thread Logic
        |--------------------------------------------------------------------------
        */

        public void LogProcessor() {
            this.Log("Logger Thread is working in the background!");
            while (true) {
                try {
                    string msg = _queue.Take(); // may block until an item is available!
                    this.Log(msg);
                } catch (ThreadInterruptedException e) {
                    if (_cts.IsCancellationRequested) // shutdown
                        break;
                }
            }
        }

        private void Log(string msg) {
            _writer.WriteLine("[Log] " + msg);
        }

        /*
        |--------------------------------------------------------------------------
        | Shutdown
        |--------------------------------------------------------------------------
        */

        public void shutdown() {
            _cts.Cancel();
            _thread.Interrupt();
        }

    }

}