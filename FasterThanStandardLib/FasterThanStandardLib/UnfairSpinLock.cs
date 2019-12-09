using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FasterThanStandardLib {

    public struct UnFairSpinLock {
        private const int WAIT_FACTOR = 8;
        private const int INVERSE_WAIT_WEIGHT = 8;

        private int isHeld;
        private int avgIterations;

        /// <summary>
        /// Acquires the lock.
        /// Lock taken will reflect if 
        /// </summary
        public void Enter(ref bool lockTaken) { 
            Debug.Assert(lockTaken == false, "lockTaken must be initialized to false prior to calling.");

            int cachedAvgIterations;
            int i = 0;
            while (true) {
                try { } finally {
                    if (Interlocked.CompareExchange(ref isHeld, 1, 0) == 0)
                        lockTaken = true;
                }
                if (lockTaken)
                    break;

                i++;
                cachedAvgIterations = Volatile.Read(ref avgIterations);
                Thread.Sleep(cachedAvgIterations * WAIT_FACTOR);
            }

            cachedAvgIterations = Volatile.Read(ref avgIterations);
            Volatile.Write(ref avgIterations, cachedAvgIterations + ((i - cachedAvgIterations) / INVERSE_WAIT_WEIGHT));
        }

        /// <summary>
        /// Acquires the lock.
        /// In the event of <see cref="System.Threading.ThreadAbortException"/> it's impossible to tell if the lock was acquired!
        /// Calling this method from within a finally block will prevent the thread abort from 
        /// interrupting control flow until after this method returns and the lock is known to be acquired.
        /// </summary
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnsafeEnter() {
            //simple case performance when uncontested
            if (0 == Interlocked.CompareExchange(ref isHeld, 1, 0))
                return;

            int cachedAvgIterations;
            int i = 0;
            while (0 != Interlocked.CompareExchange(ref isHeld, 1, 0)) {
                i++;
                cachedAvgIterations = Volatile.Read(ref avgIterations);
                Thread.Sleep(cachedAvgIterations * WAIT_FACTOR);
            }

            cachedAvgIterations = Volatile.Read(ref avgIterations);
            Volatile.Write(ref avgIterations, cachedAvgIterations + ((i - cachedAvgIterations) / INVERSE_WAIT_WEIGHT));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void TryEnter(ref bool lockTaken) {
            Debug.Assert(lockTaken == false, "lockTaken must be initialized to false prior to calling.");

            try { } finally {
                if (Interlocked.CompareExchange(ref isHeld, 1, 0) == 0)
                    lockTaken = true;
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
            Debug.Assert(Volatile.Read(ref isHeld) != 0, "Trying to exit unowned lock.");
            Volatile.Write(ref isHeld, 0);
        }

    }

}
