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
        private const int SPINNING_FACTOR = 100;
        private const int MAXIMUM_WAITERS = 1024;//concurrent access can allow waiter count to exceed this but only a bit

        private int waiterCount = 1;
        private volatile int isHeld;

        public void Enter(ref bool lockTaken) {
            Debug.Assert(lockTaken == false, "lockTaken must be initialized to false prior to calling.");

            while(isHeld == 0) {
                Thread.BeginCriticalRegion();
                if(Interlocked.CompareExchange(ref isHeld, 1, 0) == 0) {
                    lockTaken = true;
                    return;
                }
                Thread.EndCriticalRegion();
            }

            int i = 0;
            int turn = (waiterCount > MAXIMUM_WAITERS) ? MAXIMUM_WAITERS : Interlocked.Increment(ref waiterCount);

            //fast checking
            int processorCount = PlatformHelper.ProcessorCount;//todo: replace
            if(turn < processorCount) {
                int processFactor = 1;
                while(i <= turn * SPINNING_FACTOR) {
                    i++;

                    Thread.SpinWait((turn + i) * SPINNING_FACTOR * processFactor);

                    if(processFactor < processorCount) processFactor++;

                    if(isHeld == 0) {
                        Thread.BeginCriticalRegion();
                        if (Interlocked.CompareExchange(ref isHeld, 1, 0) == 0){
                            lockTaken = true;
                            return;
                        }
                        Thread.EndCriticalRegion();
                    }
                }
            }

            //slow checking
            while(true) {
                //todo: use turn down here too
                if(i % SLEEP_ONE_FREQUENCY == 0) {
                    Thread.Sleep(1);
                } else if(i % SLEEP_ZERO_FREQUENCY == 0) {
                    Thread.Sleep(0);
                } else {
                    Thread.Yield();
                }

                if(isHeld == 0) {
                    Thread.BeginCriticalRegion();
                    if(Interlocked.CompareExchange(ref isHeld, 1, 0) == 0) {
                        lockTaken = true;
                        return;
                    }
                    Thread.EndCriticalRegion();
                }

                i++;
            }
        }

        public void Exit() {
            Debug.Assert(isHeld == 1, "Trying to exit unowned lock.");
            isHeld = 0;//todo: memory barrier?
            Thread.EndCriticalRegion();
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
