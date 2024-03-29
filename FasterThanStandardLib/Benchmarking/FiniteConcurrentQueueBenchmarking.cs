﻿using FasterThanStandardLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Benchmarking {
    public class FiniteConcurrentQueueBenchmarking {
        public static void Benchmark() {
            var capacity = 256;
            var iterationsPerThread = 10000000;
            var threadCount = 32;
            Stopwatch stopwatch = new Stopwatch();
            int numOfQueues = 10;

            Console.WriteLine();
            Console.WriteLine("queues have a capacity of " + capacity);
            Console.WriteLine("each queue runs on " + threadCount + " threads, so if there are 3 queues being tested concurrently that's " + 3*threadCount + " total");
            Console.WriteLine("each thread does " + iterationsPerThread + " operations on it's target queue(2 adds for each take)");
            Console.WriteLine("the reason to test multiple queues at once is to simulate programs where a queue isn't the only thing being hit, so it reduced concurrent access");
            
            while(true) {
                Console.WriteLine();

                //{
                //    stopwatch.Start();
                //    Task[] tasks = new Task[numOfQueues];
                //    for (int i = 0; i < numOfQueues; i++) {
                //        tasks[i] = TestConcurrentQueueAsync(capacity, iterationsPerThread, threadCount);
                //    }
                //    for (int i = 0; i < numOfQueues; i++) {
                //        tasks[i].Wait();
                //    }
                //    stopwatch.Stop();
                //    Console.WriteLine("ConcurrentQueue x" + numOfQueues + ": " + stopwatch.ElapsedMilliseconds + "ms");
                //    stopwatch.Reset();
                //}

                //{
                //    stopwatch.Start();
                //    Task[] tasks = new Task[numOfQueues];
                //    for (int i = 0; i < numOfQueues; i++) {
                //        tasks[i] = TestConcurrentLoopAsync(capacity, iterationsPerThread, threadCount);
                //    }
                //    for (int i = 0; i < numOfQueues; i++) {
                //        tasks[i].Wait();
                //    }
                //    stopwatch.Stop();
                //    Console.WriteLine("TweakedConcurrentQueue x" + numOfQueues + ": " + stopwatch.ElapsedMilliseconds + "ms");
                //    stopwatch.Reset();
                //}

                {
                    stopwatch.Start();
                    Task[] tasks = new Task[numOfQueues];
                    for(int i = 0; i < numOfQueues; i++) {
                        tasks[i] = TestFiniteConcurrentQueueAsync(capacity, iterationsPerThread, threadCount);
                    }
                    for(int i = 0; i < numOfQueues; i++) {
                        tasks[i].Wait();
                    }
                    stopwatch.Stop();
                    Console.WriteLine("FiniteConcurrentQueue x" + numOfQueues + ": " + stopwatch.ElapsedMilliseconds + "ms");
                    stopwatch.Reset();
                }

                //numOfQueues++;
            }

        }

        private async static Task TestFiniteConcurrentQueueAsync(int capacity, int iterationsPerThread, int threadCount) {
            Thread[] threads = new Thread[threadCount];
            FiniteConcurrentQueue<int> littleQueue = new FiniteConcurrentQueue<int>(capacity);
            
            //debug int adds = 0;//debug
            //debug int takes = 0;//debug

            for (int i = 0; i < threads.Length; i++) {
                (threads[i] = new Thread(() => {
                    for(int j = 0; j < iterationsPerThread; j++) {
                        switch (j % 3) {
                            case 0:
                            case 1:
                                //debug if (littleQueue.TryAdd(1)) Interlocked.Increment(ref adds);//debug
                                littleQueue.TryAdd(1);
                                break;
                            default:
                                //debug if (littleQueue.TryTake(out _)) Interlocked.Increment(ref takes);//debug
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

            //debug Console.WriteLine("takes: " + takes);//debug
            //debug Console.WriteLine("adds: " + adds);//debug 

            return;
        }

        //private async static Task TestConcurrentLoopAsync(int capacity, int iterationsPerThread, int threadCount) {
        //    Thread[] threads = new Thread[threadCount];
        //    ConcurrentLoop<int> loop = new ConcurrentLoop<int>(capacity);

        //    for(int i = 0; i < threads.Length; i++) {
        //        (threads[i] = new Thread(() => {
        //            for(int j = 0; j < iterationsPerThread; j++) {
        //                switch (j % 3) {
        //                    case 0:
        //                    case 1:
        //                        //debug if (littleQueue.TryAdd(1)) Interlocked.Increment(ref adds);//debug
        //                        loop.TryAdd(1);
        //                        break;
        //                    default:
        //                        //debug if (littleQueue.TryTake(out _)) Interlocked.Increment(ref takes);//debug
        //                        loop.TryTake(out _);
        //                        break;
        //                }
        //            }
        //        })).Start();
        //    }

        //    await Task.Run(() => {
        //        for(int i = 0; i < threads.Length; i++) {
        //            threads[i].Join();
        //        }
        //    });
        //}

        private async static Task TestConcurrentQueueAsync(int capacity, int iterationsPerThread, int threadCount) {
            Thread[] threads = new Thread[threadCount];
            ConcurrentQueue<int> queue = new ConcurrentQueue<int>();

            int count = 0;

            for (int i = 0; i < threads.Length; i++) {
                (threads[i] = new Thread(() => {
                    for (int j = 0; j < iterationsPerThread; j++) {
                        switch (j % 3) {
                            case 0:
                            case 1:
                                if (count <= capacity) {
                                    //afaik this is the fastest way to limit queue growth, checking queue.Count is much slower
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

            await Task.Run(() => {
                for (int i = 0; i < threads.Length; i++) {
                    threads[i].Join();
                }
            });
        }

    }
}
