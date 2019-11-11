using FasterThanStandardLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Benchmarking {
    class Program {
        static void Main() {

            Console.ReadKey();

            var capacity = 5000;
            var threadIterations = 10000000;
            Thread[] threads = new Thread[10];
            int count = 0;

            ConcurrentQueue<int> queue = new ConcurrentQueue<int>();

            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < threads.Length; i++) {
                (threads[i] = new Thread(() => {
                    for (int j = 0; j < threadIterations; j++) {
                        switch (j % 3) {
                            case 0:
                            case 1:
                                if (count <= capacity) {
                                    Interlocked.Increment(ref count);
                                    queue.Enqueue(1);
                                }
                                break;
                            default:
                                if (queue.TryDequeue(out _)) {
                                    Interlocked.Decrement(ref count);
                                }
                                break;
                        }
                    }
                })).Start();
            }
            for (int i = 0; i < threads.Length; i++) {
                threads[i].Join();
            }

            stopwatch.Stop();
            Console.WriteLine("queue: " + stopwatch.ElapsedMilliseconds + "ms");

            FiniteConcurrentQueue<int> littleQueue = new FiniteConcurrentQueue<int>(capacity);

            stopwatch.Reset();
            stopwatch.Start();

            for (int i = 0; i < threads.Length; i++) {
                (threads[i] = new Thread(() => {
                    for (int j = 0; j < threadIterations; j++) {
                        switch (j % 3) {
                            case 0:
                            case 1:
                                littleQueue.TryAdd(1);
                                break;
                            default:
                                littleQueue.TryTake(out _);
                                break;
                        }
                    }
                })).Start();
            }
            for (int i = 0; i < threads.Length; i++) {
                threads[i].Join();
            }

            stopwatch.Stop();
            Console.WriteLine("managed: " + stopwatch.ElapsedMilliseconds + "ms");

            FiniteConcurrentQueueUnmanaged<int> unmanaged = new FiniteConcurrentQueueUnmanaged<int>(capacity);

            stopwatch.Reset();
            stopwatch.Start();

            for (int i = 0; i < threads.Length; i++) {
                (threads[i] = new Thread(() => {
                    for (int j = 0; j < threadIterations; j++) {
                        switch (j % 3) {
                            case 0:
                            case 1:
                                unmanaged.TryAdd(1);
                                break;
                            default:
                                unmanaged.TryTake(out _);
                                break;
                        }
                    }
                })).Start();
            }
            for (int i = 0; i < threads.Length; i++) {
                threads[i].Join();
            }

            stopwatch.Stop();
            Console.WriteLine("unmanaged: " + stopwatch.ElapsedMilliseconds + "ms");
            Console.ReadKey();
        }
    }
}
