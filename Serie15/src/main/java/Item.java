import java.util.concurrent.Callable;
import java.util.concurrent.locks.Condition;

public class Item<T> {
    private final Callable<T> function;
    private Condition condition;
    private boolean done;
    private boolean isResolving;
    private T result;
    private Exception exception;

    public Item(Callable<T> func, Condition condition) {
        this.function = func;
        this.condition = condition;
    }

    public boolean isCompleted() {
        return done;
    }

    public T result() throws Exception {
        if (this.exception != null)
            throw exception;

        return this.result;
    }

    public Condition condition() {
        return this.condition;
    }

    public void complete(T res) {
        this.result = res;
        this.done = true;
    }

    public void complete(Exception e) {
        this.exception = e;
        this.done = true;
    }

    public Callable<T> function() {
        return this.function;
    }

    public boolean canBeCanceled() {
        return !isResolving && !done;
    }

    public void resolving() {
        this.isResolving = true;
    }
}