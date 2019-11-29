using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace FasterThanStandardLib {
    public class ConditionalInterlocked {
        private const int SLEEP_ONE_FREQUENCY = 63;//needs to be 1 less than a pow of 2!
        private const int SLEEP_ZERO_FREQUENCY = 15;//needs to be 1 less than a pow of 2!
        private const int SPINNING_FACTOR = 100;
        private const int MAXIMUM_WAITERS = 1024;//concurrent access can allow waiter count to exceed this but only a bit

        public static void GreatertThanAdd(ref int location1, int value) {

        }

        //todo: other methods
    }
}
