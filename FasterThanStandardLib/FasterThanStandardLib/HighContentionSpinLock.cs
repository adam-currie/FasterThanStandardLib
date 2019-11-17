using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace FasterThanStandardLib {

    public struct HighContentionSpinLock{
        private const int YIELD_FREQUENCY = 15;//needs to be 1 less than a pow of 2!
        private const int SLEEP_ONE_FREQUENCY = 7;//needs to be 1 less than a pow of 2!

        private int isHeld;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enter(ref bool lockTaken) {
            Debug.Assert(lockTaken == false, "lockTaken must be initialized to false prior to calling.");

            int i = 0;
            while (true) {
                try { } finally {
                    if (Interlocked.CompareExchange(ref isHeld, 1, 0) == 0)
                        lockTaken = true;
                }
                if (lockTaken) return;

                i++;
                if ((i & YIELD_FREQUENCY) == 0) {
                    Thread.Yield();
                } else if ((i & SLEEP_ONE_FREQUENCY) == 0) {
                    Thread.Sleep(1);
                }
            }
        }

        internal void TryEnter(ref bool lockTaken) {
            Debug.Assert(lockTaken == false, "lockTaken must be initialized to false prior to calling.");

            try { } finally {
                if (Interlocked.CompareExchange(ref isHeld, 1, 0) == 0)
                    lockTaken = true;
            }
        }

        public void Exit() {
            Debug.Assert(Volatile.Read(ref isHeld) != 0, "Trying to exit unowned lock.");
            Volatile.Write(ref isHeld, 0);
        }

    }

}
