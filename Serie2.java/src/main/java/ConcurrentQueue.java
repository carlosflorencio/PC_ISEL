import java.util.concurrent.atomic.AtomicReference;

public class ConcurrentQueue<T> {

    private static class Node<T> {
        public final T item;
        public final AtomicReference<Node<T>> next;

        public Node(T item, Node<T> next) {
            this.item = item;
            this.next = new AtomicReference<Node<T>>(next);
        }
    }

    private final Node<T> dummy = new Node<T>(null, null);
    private final AtomicReference<Node<T>> head = new AtomicReference<>(dummy);
    private final AtomicReference<Node<T>> tail = new AtomicReference<>(dummy);
    // head and tail pointing to the same object

    public void put(T item) {
        Node<T> newNode = new Node<T>(item, null);

        while (true) {
            Node<T> curTail = tail.get();
            Node<T> tailNext = curTail.next.get();

            if (curTail == tail.get()) { // Are tail and next consistent?
                if (tailNext != null) { // queue in intermediate state...
                    tail.compareAndSet(curTail, tailNext); //advance tail
                } else {
                    if (curTail.next.compareAndSet(null, newNode)) {
                        // insertion succeeded, try advancing tail
                        tail.compareAndSet(curTail, newNode);
                        return;
                    }
                }
            }
        }
    }

    public T tryTake() {
        while(true) {
            Node<T> headCur = head.get();
            Node<T> tailCur = tail.get();
            Node<T> headNext = headCur.next.get();

            if(headCur == head.get()) { // Are head, tail, and next consistent?
                if(headCur == tailCur) { // Is queue empty or Tail falling behind?
                    if(headNext == null) // Is queue empty?
                        return null; // Queue is empty, couldn’t dequeue

                    tail.compareAndSet(tailCur, headNext); // Tail is falling behind. Try to advance it
                } else   { // No need to deal with Tail
                    // Read value before CAS, otherwise another dequeue might free the next node
                    T val = headNext.item;

                    if(head.compareAndSet(headCur, headNext)) // Try to swing Head to the next node
                        return val; // Dequeue is done. Exit loop
                }
            }
        }
    }

    // take an item - spinning if necessary
    public T take() throws InterruptedException {
        T v;
        while ((v = tryTake()) == null) {
            Thread.sleep(0);
        }
        return v;
    }

    public boolean isEmpty() {
        return head.get() == tail.get();
    }
}
