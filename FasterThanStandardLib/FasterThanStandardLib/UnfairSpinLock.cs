using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace FasterThanStandardLib {

    public struct UnFairSpinLock{
        private const int TIGHT_LOOP_COUNT = 32;

        private int isHeld;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enter(ref bool lockTaken) {
            Debug.Assert(lockTaken == false, "lockTaken must be initialized to false prior to calling.");

            for (int i = 0; true; i++) {
                try { } finally {
                    if (Interlocked.CompareExchange(ref isHeld, 1, 0) == 0)
                        lockTaken = true;
                }
                if (lockTaken) return;

                if (i >= TIGHT_LOOP_COUNT) Thread.Sleep(i);
            }
        }

        /// <summary>
        /// Acquires the lock.
        /// Must be called in a finally block to ensure that the internal lock is not left permenantly locked in the case of a thread abort.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnsafeEnter() {
            for (int i = 0; Interlocked.CompareExchange(ref isHeld, 1, 0) != 0; i++)
                if (i >= TIGHT_LOOP_COUNT) Thread.Sleep(i);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void TryEnter(ref bool lockTaken) {
            Debug.Assert(lockTaken == false, "lockTaken must be initialized to false prior to calling.");

            try { } finally {
                if (Interlocked.CompareExchange(ref isHeld, 1, 0) == 0)
                    lockTaken = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Exit() {
            Debug.Assert(Volatile.Read(ref isHeld) != 0, "Trying to exit unowned lock.");
            Volatile.Write(ref isHeld, 0);
        }

    }

}
