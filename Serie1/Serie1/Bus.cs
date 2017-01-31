using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serie1 {

    public class Bus {

        private readonly int _maxPending;
        private bool shutdown = false;
        private readonly object shutdownLock = new object();
        private Dictionary<Type, LinkedList<object>> map = new Dictionary<Type, LinkedList<object>>();
        private readonly object _lock = new object();

        public Bus(int maxPending) {
            this._maxPending = maxPending;
        }

        /*
		|--------------------------------------------------------------------------
		| Subscribe
		|--------------------------------------------------------------------------
		*/

        public void SubscribeEvent<T>(Action<T> handler) where T : class {
            bool lockTaken = false;
            try {
                Monitor.Enter(_lock, ref lockTaken);
                LinkedListNode<object> node = GetOrCreateEventHandler(handler);

                do {
                    try {
                        SyncUtils.Wait(_lock, map[typeof(T)]);

                        var myEvent = node.Value as Event<T>;

                        while (myEvent.Messages.First != null) {
                            var message = myEvent.Messages.First.Value;
                            lockTaken = false;
                            Monitor.Exit(_lock);

                            myEvent.Handler(message); // execute outside of the lock

                            Monitor.Enter(_lock, ref lockTaken);

                            myEvent.Messages.RemoveFirst();
                        }

                        if (shutdown) {
                            RemoveHandlerFromEventsList<T>(typeof(T), node.Value);

                            if (map.Count == 0) { // last handler?
                                SyncUtils.Notify(_lock, shutdownLock);
                            }

                            break;
                        }

                    } catch (ThreadInterruptedException e) {
                        // remove handler from list
                        RemoveHandlerFromEventsList<T>(typeof(T), node.Value);

                        if (shutdown && map.Count == 0)
                        { // last handler?
                            SyncUtils.Notify(_lock, shutdownLock);
                        }

                        throw;
                    }
                } while (true);
            } finally {
                if (lockTaken) Monitor.Exit(_lock);
            }

        }

        /*
		|--------------------------------------------------------------------------
		| Publish
		|--------------------------------------------------------------------------
		*/

        public void PublishEvent<T>(T message) where T : class {
            lock (_lock) {
                if (shutdown)
                    throw new InvalidOperationException();

                LinkedList<object> events = null;
                if (map.TryGetValue(typeof(T), out events) == false)
                    return; // we dont have handlers yet, ignore

                bool notified = false;
                foreach (var e in events)
                {
                    var singleEvent = e as Event<T>;

                    if (singleEvent.Messages.Count > _maxPending)
                        continue; // no space to save the message

                    singleEvent.Messages.AddLast(message);
                    notified = true;
                }

                if(notified) SyncUtils.Broadcast(_lock, map[typeof(T)]);
            }
        }

        /*
		|--------------------------------------------------------------------------
		| Shutdown
		|--------------------------------------------------------------------------
		*/

        public void Shutdown() {
            lock (_lock) {
                if(shutdown) return;

                shutdown = true;

                foreach (KeyValuePair<Type, LinkedList<object>> entry in map)
                {
                    SyncUtils.Broadcast(_lock, entry.Value); // notify all thread handlers
                }

                try {
                    if(map.Count > 0)
                        SyncUtils.Wait(_lock, shutdownLock);
                } catch (ThreadInterruptedException e) {
                    throw e;
                }
            }
        }

        private LinkedListNode<object> GetOrCreateEventHandler<T>(Action<T> action)
        {
            LinkedList<object> events = null;
            if (map.TryGetValue(typeof(T), out events) == false)
            { // create the Event
                events = new LinkedList<object>();
                map.Add(typeof(T), events);
                return events.AddLast(new Event<T>(action));
            }

            return events.AddLast(new Event<T>(action));
        }

        private void RemoveHandlerFromEventsList<T>(Type key, object handler) {
            map[key].Remove(handler);

            if (map[key].Count == 0)
                map.Remove(key);
        }

    }

    /*
    |--------------------------------------------------------------------------
    | Inner types
    |--------------------------------------------------------------------------
    */

    class Event<T> {

        public Event(Action<T> h) {
            this.Handler = h;
            this.Messages = new LinkedList<T>();
        }

        public Action<T> Handler;
        public LinkedList<T> Messages;

    }

}