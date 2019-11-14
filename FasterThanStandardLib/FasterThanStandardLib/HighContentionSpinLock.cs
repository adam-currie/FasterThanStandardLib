using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace FasterThanStandardLib {
    public struct HighContentionSpinLock {
        private const int SLEEP_ONE_FREQUENCY = 63;//needs to be 1 less than a pow of 2!
        private const int SLEEP_ZERO_FREQUENCY = 15;//needs to be 1 less than a pow of 2!

        private int isHeld;
        //todo private volatile int averageIterationCount;

        public void Enter(ref bool lockTaken) {
            int i = 0;
            while (true) {
                if(isHeld == 0) {//pre check that it's unowned
                    Thread.BeginCriticalRegion();
                    try { } finally {
                        if(0 == Interlocked.CompareExchange(ref isHeld, 1, 0)) {
                            lockTaken = true;
                        }
                    }
                    if(lockTaken) {
                        //todo: averageIterationCount += (i - averageIterationCount)/8;
                        return;
                    }
                    Thread.EndCriticalRegion();
                }

                if ((i & SLEEP_ONE_FREQUENCY) == 0) {
                    Thread.Sleep(1);
                } else if ((i & SLEEP_ZERO_FREQUENCY) == 0) {
                    Thread.Sleep(0);
                } else {
                    if(i < 1000) {
                        Thread.SpinWait(i);
                    } else {
                        Thread.Yield();
                    }
                }

                i++;
            }
        }

        public void Exit() {
            isHeld = 0;
            Thread.EndCriticalRegion();
        }

    }
}
