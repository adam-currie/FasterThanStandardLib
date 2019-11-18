using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace FasterThanStandardLib {

    public class FiniteConcurrentQueueUnmanaged<T> : FiniteConcurrentQueue<T> where T : unmanaged {
        public FiniteConcurrentQueueUnmanaged(int capacity) : base(capacity, true) { }
    }

    public class FiniteConcurrentQueue<T> : IProducerConsumerCollection<T> {
        private static readonly int MAX_CAPACITY = 1073741824;//2^30, largest capacity we can allow because next highest power of 2 would wrap
        private static readonly int MIN_CAPACITY = 2048;// we want a minimum size to reduce contention when looping back around
        private static readonly int MIN_CAPACITY_SMALL = 64;

        private readonly ItemSlot[] slots;
        private readonly int capacity;
        private readonly int indexifier;
        private volatile int addCursor = 0;//todo: test these from int.minvalue
        private volatile int takeCursor = 0;
        private HighContentionSpinLock addCursorLock = new HighContentionSpinLock();
        private HighContentionSpinLock takeCursorLock = new HighContentionSpinLock();

        public FiniteConcurrentQueue(int capacity) : this(capacity, false) { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="capacity"></param>
        /// <param name="small"> 
        ///     Smaller memory footprint in short queues at the expense of performance under very high concurrency.
        /// </param>
        protected FiniteConcurrentQueue(int capacity, bool small = false) {
            if (capacity < 0) throw new ArgumentException("capacity cannot be negative");
            if (capacity > MAX_CAPACITY) throw new ArgumentException("capacity cannot be greater than " + MAX_CAPACITY);

            this.capacity = capacity;

            //need array size to be a power of 2 so we can do modulo with bitwise ANDing
            var arraySize = NextPositivePowerOf2(capacity + (small? MIN_CAPACITY_SMALL : MIN_CAPACITY));

            indexifier = arraySize - 1;
            slots = new ItemSlot[arraySize];
        }

        public int Count => Math.Max(0, addCursor - takeCursor);

        public bool IsSynchronized => false;//although this code is thread safe it doesn't use full lock synchronization

        public object SyncRoot => throw new NotSupportedException();

        public void CopyTo(T[] array, int index) {
            throw new NotImplementedException();
        }

        public void CopyTo(Array array, int index) {
            throw new NotImplementedException();
        }

        public IEnumerator<T> GetEnumerator() {
            throw new NotImplementedException();
        }

        public T[] ToArray() {
            throw new NotImplementedException();
            //todo: we need to stop these statements from being swapped by using a memory barrier or something

            int add = addCursor;//need to stash BEFORE the snapshot to have a conservatively low 

            T[] clone = (T[])slots.Clone();

            int take = takeCursor;


        }

        public bool TryAdd(T item) {
            int localAddCursor = 0;
            int addCurLimit = capacity + takeCursor;

            if (addCursor >= addCurLimit) {
                return false;
            }

            bool cursorLockTaken = false;
            try {
                addCursorLock.Enter(ref cursorLockTaken);

                if(addCursor < addCurLimit) {
                    localAddCursor = unchecked(++addCursor);
                } else {
                    return false;// EARLY RETURN
                }
            } finally { 
                if(cursorLockTaken) addCursorLock.Exit(); 
            }

            slots[localAddCursor & indexifier].Add(item);
            return true;
        }

        public bool TryTake(out T item) {
            item = default;
            int localTakeCursor;

            if (takeCursor >= addCursor) {
                return false;
            }

            bool cursorLockTaken = false;
            try { 
                takeCursorLock.Enter(ref cursorLockTaken);

                if (takeCursor < addCursor) {
                    localTakeCursor = unchecked(++takeCursor);
                } else {
                    //queue is empty
                    return false;// EARLY RETURN
                }
            } finally { 
                if(cursorLockTaken) takeCursorLock.Exit(); 
            }

            item = slots[localTakeCursor & indexifier].Take();
            return true;
        }

        IEnumerator IEnumerable.GetEnumerator() {
            throw new NotImplementedException();
        }

        private static int NextPositivePowerOf2(int n) {
            if (n < 2) return 2;
            n--;
            n |= n >> 1;
            n |= n >> 2;
            n |= n >> 4;
            n |= n >> 8;
            n |= n >> 16;
            return checked(++n);
        }

        private struct ItemSlot {
            private T item;
            private volatile bool hasItem;//todo: replace volatile
            private int takerLock;
            private int adderLock;

            public T Take() {
                T item = default;
                bool done = false;
                while (true) {
                    try { } finally {
                        if (Interlocked.CompareExchange(ref takerLock, 1, 0) == 0) {
                            //wait for the adder to finish, we want to do a tight loop here because we are holding a lock
                            while (hasItem == false) { }
                            item = this.item;
                            this.item = default;
                            hasItem = false;
                            Volatile.Write(ref takerLock, 0);
                            done = true;
                        }
                    }
                    if (done) {
                        return item;
                    }
                    //if there is contention here then things must be really backed up, so just cede control completely
                    Thread.Sleep(1);
                }
            }

            public T Add(T item) {
                bool done = false;
                while (true) {
                    try { } finally {
                        if (Interlocked.CompareExchange(ref adderLock, 1, 0) == 0) {
                            //wait for the taker to finish, we want to do a tight loop here because we are holding a lock
                            while (hasItem) { }
                            this.item = item;
                            hasItem = true;
                            Volatile.Write(ref adderLock, 0);
                            done = true;
                        }
                    }
                    if (done) {
                        return item;
                    }
                    //if there is contention here then things must be really backed up, so just cede control completely
                    Thread.Sleep(1);
                }
            }

        }

    }
}