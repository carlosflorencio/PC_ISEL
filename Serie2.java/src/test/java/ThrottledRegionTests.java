import org.junit.Assert;
import org.junit.Test;

public class ThrottledRegionTests {

    public void TryEnterSuccess(ThrottledRegion region, int key) {
        try {
            Assert.assertTrue(region.tryEnter(key));
        } catch (InterruptedException e) {
            Assert.fail();
        }
    }

    public void TryEnterFail(ThrottledRegion region, int key) {
        try {
            Assert.assertFalse(region.tryEnter(key));
        } catch (InterruptedException e) {
            Assert.fail();
        }
    }

    /*
    |--------------------------------------------------------------------------
    | Test Happy Path
    |--------------------------------------------------------------------------
    */
    @Test
    public void Test_HappyPath() throws InterruptedException {
        ThrottledRegion region = new ThrottledRegion(2, 2, 1 << 30);
        int key = 1;
        Thread t1 = new Thread(() -> TryEnterSuccess(region, key)); // enter
        Thread t2 = new Thread(() -> TryEnterSuccess(region, key)); // enter
        Thread t3 = new Thread(() -> TryEnterSuccess(region, key)); // wait

        t1.start();
        t2.start();
        t1.join();
        t2.join();
        t3.start();
        Thread.sleep(1000); // make sure t3 is waiting
        region.leave(key); // t3 can enter
        t3.join();
    }

    /*
    |--------------------------------------------------------------------------
    | Test Timeout return
    |--------------------------------------------------------------------------
    */
    @Test
    public void Test_Timeout() throws InterruptedException {
        ThrottledRegion region = new ThrottledRegion(2, 2, 1000);
        int key = 1;
        Thread t1 = new Thread(() -> TryEnterSuccess(region, key)); // enter
        Thread t2 = new Thread(() -> TryEnterSuccess(region, key)); // enter
        Thread t3 = new Thread(() -> TryEnterFail(region, key)); // wait

        t1.start();
        t2.start();
        t1.join();
        t2.join();
        t3.start();
        Thread.sleep(3000); // make sure t3 gets the timeout
        t3.join();
    }

    /*
    |--------------------------------------------------------------------------
    | Test Max inside in two regions
    |--------------------------------------------------------------------------
    */
    @Test
    public void Test_MaxInsideInTwoRegions() throws InterruptedException {
        ThrottledRegion region = new ThrottledRegion(1, 1, 1 << 30);
        int key = 1;
        int key2 = 2;
        Thread t1 = new Thread(() -> TryEnterSuccess(region, key)); // enter region1
        Thread t2 = new Thread(() -> TryEnterSuccess(region, key)); // wait region1
        Thread t3 = new Thread(() -> TryEnterFail(region, key)); // Fail region1

        Thread t4 = new Thread(() -> TryEnterSuccess(region, key2)); // enter region2
        Thread t5 = new Thread(() -> TryEnterSuccess(region, key2)); // wait region2
        Thread t6 = new Thread(() -> TryEnterFail(region, key2)); // fail region2

        t1.start();
        t1.join();
        t2.start(); // will wait
        Thread.sleep(100); //give some time to run before the t3
        t3.start();
        t3.join();
        region.leave(key);
        t2.join();

        t4.start();
        t4.join();
        t5.start(); // will wait
        Thread.sleep(100); //give some time to run before the t6
        t6.start();
        t6.join();
        region.leave(key2);
        t5.join();
    }
}
