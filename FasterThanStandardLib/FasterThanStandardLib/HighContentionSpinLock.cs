using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Text;
using System.Threading;

namespace FasterThanStandardLib {

    public class HighContentionSpinLock{
        private const int SLEEP_ONE_FREQUENCY = 63;//needs to be 1 less than a pow of 2!
        private const int SLEEP_ZERO_FREQUENCY = 15;//needs to be 1 less than a pow of 2!
        private const int TIGHT_SPIN_COUNT = 64;

        private volatile int isHeld;

        public void Enter(ref bool lockTaken) {//todo: can wrap this method in another one and pass timeout condition here
            Debug.Assert(lockTaken == false, "lockTaken must be initialized to false prior to calling.");

            int i = 0;

            for (; i < TIGHT_SPIN_COUNT; i++) {
                try { } finally {
                    if (Interlocked.CompareExchange(ref isHeld, 1, 0) == 0)
                        lockTaken = true;
                }
                if (lockTaken) return;
            }

            while(true) {
                if ((i & SLEEP_ONE_FREQUENCY) == 0) {
                    Thread.Sleep(1);
                } else if ((i & SLEEP_ZERO_FREQUENCY) == 0) {
                    Thread.Sleep(0);
                } else {
                    Thread.Yield();
                }

                if (isHeld == 0) {
                    try { } finally {
                        if (Interlocked.CompareExchange(ref isHeld, 1, 0) == 0)
                            lockTaken = true;
                    }
                    if (lockTaken) return;
                }

                i++;
            }
        }

        public void Exit() {
            Debug.Assert(isHeld == 1, "Trying to exit unowned lock.");
            isHeld = 0;//todo: memory barrier?
        }


    }


    /// <summary>
    /// A helper class to get the number of processors, it updates the numbers of processors every sampling interval.
    /// </summary>
    internal static class PlatformHelper {
        private const int PROCESSOR_COUNT_REFRESH_INTERVAL_MS = 30000; // How often to refresh the count, in milliseconds.
        private static volatile int s_processorCount; // The last count seen.
        private static volatile int s_lastProcessorCountRefreshTicks; // The last time we refreshed.

        /// <summary>
        /// Gets the number of available processors
        /// </summary>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
        internal static int ProcessorCount {
            get {
                int now = Environment.TickCount;
                int procCount = s_processorCount;
                if(procCount == 0 || (now - s_lastProcessorCountRefreshTicks) >= PROCESSOR_COUNT_REFRESH_INTERVAL_MS) {
                    s_processorCount = procCount = Environment.ProcessorCount;
                    s_lastProcessorCountRefreshTicks = now;
                }

                Contract.Assert(procCount > 0 && procCount <= 64,
                    "Processor count not within the expected range (1 - 64).");

                return procCount;
            }
        }

        /// <summary>
        /// Gets whether the current machine has only a single processor.
        /// </summary>
        internal static bool IsSingleProcessor {
            get { return ProcessorCount == 1; }
        }
    }

}
