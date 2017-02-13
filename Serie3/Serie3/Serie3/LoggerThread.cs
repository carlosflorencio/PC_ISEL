using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Serie3
{
    internal class LoggerThread
    {
        // Thread safe ConcurrentQueue with blocking functionality
        private BlockingCollection<string> _queue;
        private TextWriter _writer;
        private Thread _thread;

        public LoggerThread(TextWriter w)
        {
            this._queue = new BlockingCollection<string>(new ConcurrentQueue<string>());
            this._writer = w;
        }

        public LoggerThread() : this(Console.Out) { }

        public void Start() {
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

        public void Add(string msg)
        {
            if(_thread.IsAlive)
                _queue.Add(msg); // thread safe & non-blocking!
        }


        /*
        |--------------------------------------------------------------------------
        | Thread Logic
        |--------------------------------------------------------------------------
        */

        public void LogProcessor()
        {
            _writer.WriteLine("Logger Thread is working in the background!");
            while (true) {
                // may block until an item is available!
                string msg = _queue.Take(); 
                _writer.WriteLine("[Log] " + msg);
            }
        }
    }
}