using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serie2 {

    public class ConcurrentQueue<T> {

        private class Node<T> {

            internal readonly T item;
            internal volatile Node<T> next;

            public Node(T item, Node<T> next) {
                this.item = item;
                this.next = next;
            }

        }

        private readonly Node<T> dummy = new Node<T>(default(T), null);
        private volatile Node<T> head;
        private volatile Node<T> tail;


        public ConcurrentQueue() {
            // head and tail pointing to the same object
            this.head = dummy;
            this.tail = dummy;
        }

        /*
        |--------------------------------------------------------------------------
        | Put
        |--------------------------------------------------------------------------
        */

        public void Put(T item) {
            Node<T> newNode = new Node<T>(item, null);

            while (true) {
                Node<T> curTail = tail;
                Node<T> tailNext = curTail.next;

                if (curTail == tail) { // Are tail and next consistent?
                    if (tailNext != null) { // queue in intermediate state...
                        Interlocked.CompareExchange(ref tail, tailNext, curTail); //advance tail
                    } else {
                        if (Interlocked.CompareExchange(ref curTail.next, newNode, null) == null) {
                            // insertion succeeded, try advancing tail
                            Interlocked.CompareExchange(ref tail, newNode, curTail);
                            return;
                        }
                    }
                }
            }
        }

        /*
        |--------------------------------------------------------------------------
        | TryTake
        |--------------------------------------------------------------------------
        */

        public T TryTake() {
            while (true) {
                Node<T> headCur = head;
                Node<T> tailCur = tail;
                Node<T> headNext = headCur.next;

                if (headCur == head) { // Are head, tail, and next consistent?
                    if (headCur == tailCur) { // Is queue empty or Tail falling behind?
                        if (headNext == null) // Is queue empty?
                            return default(T); // Queue is empty, couldn’t dequeue

                        // Tail is falling behind. Try to advance it
                        Interlocked.CompareExchange(ref tail, headNext, tailCur);
                    } else { // No need to deal with Tail
                        // Read value before CAS, otherwise another dequeue might free the next node
                        T val = headNext.item;


                        // Try to swing Head to the next node
                        if (Interlocked.CompareExchange(ref head, headNext, headCur) == headCur)
                            return val; // Dequeue is done. Exit loop
                    }
                }
            }
        }


        // Take an item - spinning if necessary
        public T Take() {
            T v;
            while ((v = TryTake()) == null) {
                Thread.Sleep(0);
            }

            return v;
        }

        public bool IsEmpty() {
            return head == tail;
        }

    }

}