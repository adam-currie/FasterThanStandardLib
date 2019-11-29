using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace FasterThanStandardLib {
    public struct FairSpinLock {

        private int next;
        private int owner;

        /// <summary>
        /// Acquires the lock.
        /// Must be called in a finally block to ensure that the internal lock is not left permenantly locked in the case of a thread abort.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void UnsafeEnter() {
            int self = Interlocked.Increment(ref next)-1;

            int diff;
            int prevDiff = int.MaxValue;
            int sleepDuration = 0;

            while (0 != (diff = (self - Volatile.Read(ref owner)))) {
                int improvement = prevDiff - diff + 1;
                prevDiff = diff;

                sleepDuration *= diff;
                sleepDuration /= improvement * 8;
                sleepDuration += diff;

                Thread.Sleep(1);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Exit() {
            Volatile.Write(ref owner, owner+1);
        }

    }

}
