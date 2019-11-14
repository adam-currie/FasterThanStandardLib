using FasterThanStandardLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benchmarking {
    class Program {
        static void Main() {
            Console.WriteLine("Press a key to start:");
            while(true) {
                Console.WriteLine();
                Console.WriteLine("1: FiniteConcurrentQueue");
                Console.WriteLine("2: HighContentionSpinLock");
                switch(Console.ReadKey(true).Key) {
                    case ConsoleKey.D1:
                        FiniteConcurrentQueueBenchmarking.Benchmark();
                        break;
                    case ConsoleKey.D2:
                        SpinLockBenchmarking.Benchmark();
                        break;
                }
            }
        }
    }
}
