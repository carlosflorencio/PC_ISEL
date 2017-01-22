using System;
using System.Threading;

namespace Serie1 {

    class SyncUtils {

        public static int AdjustTimeout(ref int lastTime, ref int timeout) {
            if (timeout == Timeout.Infinite) return timeout;

            int now = Environment.TickCount;
            int elapsed = (now == lastTime) ? 1 : now - lastTime;
            if (elapsed >= timeout) {
                timeout = 0;
            } else {
                timeout -= elapsed;
                lastTime = now;
            }
            return timeout;
        }

    }

}