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
        /// In the event of <see cref="System.Threading.ThreadAbortException"/> it's impossible to tell if the lock was acquired!
        /// Calling this method from within a finally block will prevent the thread abort from 
        /// interrupting control flow until after this method returns and the lock is known to be acquired.
        /// </summary
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnsafeEnter() {
            int self = Interlocked.Increment(ref next)-1;

            int diff;
            int prevDiff = int.MaxValue;
            int sleepDuration = 0;

            while (0 != (diff = (self - Volatile.Read(ref owner)))) {
                int improvement = prevDiff - diff + 1;
                prevDiff = diff;

                sleepDuration *= diff;
                sleepDuration /= improvement * 4;
                sleepDuration += diff;

                Thread.Sleep(sleepDuration);
            }
        }

        /// <summary>
        /// Releases the lock.
        /// </summary>
        /// <remarks>
        /// Calling this when the caller does not own the lock results in undefined behavior.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Exit() {
            Volatile.Write(ref owner, owner + 1);
        }

    }

}
