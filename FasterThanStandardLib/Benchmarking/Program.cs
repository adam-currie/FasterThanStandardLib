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

            var capacity = 64;
            var iterationsPerThread =50000000;
            var threadCount = 10;

            Stopwatch stopwatch = Stopwatch.StartNew();
            TestConcurrentQueue(capacity, iterationsPerThread, threadCount);
            stopwatch.Stop();
            Console.WriteLine("queue: " + stopwatch.ElapsedMilliseconds + "ms");
            stopwatch.Reset();

            stopwatch.Start();
            TestFiniteConcurrentQueueAsync(capacity, iterationsPerThread, threadCount).Wait();
            stopwatch.Stop();
            Console.WriteLine("fcq: " + stopwatch.ElapsedMilliseconds + "ms");

            stopwatch.Start();
            var numOfQueues = 3;
            Task[] tasks = new Task[numOfQueues];
            for(int i = 0; i < numOfQueues; i++) {
                tasks[i] = TestFiniteConcurrentQueueAsync(capacity, iterationsPerThread, threadCount);
            }
            for(int i = 0; i < numOfQueues; i++) {
                tasks[i].Wait();
            }
            stopwatch.Stop();
            Console.WriteLine("fqc x" + numOfQueues + ": " + stopwatch.ElapsedMilliseconds + "ms");

            while(Console.KeyAvailable) Console.ReadKey(true);
            Console.ReadKey();
        }

        private async static Task TestFiniteConcurrentQueueAsync(int capacity, int iterationsPerThread, int threadCount) {
            Thread[] threads = new Thread[threadCount];
            FiniteConcurrentQueue<int> littleQueue = new FiniteConcurrentQueue<int>(capacity);

            for(int i = 0; i < threads.Length; i++) {
                (threads[i] = new Thread(() => {
                    for(int j = 0; j < iterationsPerThread; j++) {
                        switch(j % 3) {
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

            await Task.Run(() => {
                for(int i = 0; i < threads.Length; i++) {
                    threads[i].Join();
                }
            });
        }

        private static void TestConcurrentQueue(int capacity, int iterationsPerThread, int threadCount) {
            Thread[] threads = new Thread[threadCount];
            ConcurrentQueue<int> queue = new ConcurrentQueue<int>();
            int count = 0;

            for(int i = 0; i < threads.Length; i++) {
                (threads[i] = new Thread(() => {
                    for(int j = 0; j < iterationsPerThread; j++) {
                        switch(j % 3) {
                            case 0:
                            case 1:
                                if(count <= capacity) {
                                    Interlocked.Increment(ref count);
                                    queue.Enqueue(1);
                                }
                                break;
                            default:
                                if(queue.TryDequeue(out _)) {
                                    Interlocked.Decrement(ref count);
                                }
                                break;
                        }
                    }
                })).Start();
            }

            for(int i = 0; i < threads.Length; i++) {
                threads[i].Join();
            }
        }
    }
}
