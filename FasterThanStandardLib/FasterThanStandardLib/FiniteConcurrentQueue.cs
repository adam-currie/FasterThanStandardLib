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
        private readonly ItemSlot[] slots;
        private readonly int capacity;
        private readonly int indexifier;
        private readonly bool isUnmanaged;
        private volatile int addCursor = 0;//todo: test these from int.minvalue
        private volatile int takeCursor = 0;
        private HighContentionSpinLock addCursorLock = new HighContentionSpinLock();
        private HighContentionSpinLock takeCursorLock = new HighContentionSpinLock();

        public FiniteConcurrentQueue(int capacity) : this(capacity, false) { }

        protected FiniteConcurrentQueue(int capacity, bool isUnmanaged) {
            if (capacity < 0) throw new ArgumentException("capacity cannot be negative");
            if (capacity > MAX_CAPACITY) throw new ArgumentException("capacity cannot be greater than " + MAX_CAPACITY);

            this.isUnmanaged = isUnmanaged;
            this.capacity = capacity;

            //need array size to be a power of 2 so we can do modulo with bitwise ANDing
            var arraySize = NextPositivePowerOf2(capacity);//todo: probably want a minimum size here to reduce contention
            indexifier = arraySize - 1;
            slots = new ItemSlot[arraySize];
            for (int i = 0; i < arraySize; i++) {
                slots[i] = new ItemSlot();
            }
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
            int localTakeCursor;

            if (takeCursor >= addCursor) {
                item = default;
                return false;
            }

            bool cursorLockTaken = false;
            try { 
                takeCursorLock.Enter(ref cursorLockTaken);

                if (takeCursor < addCursor) {
                    localTakeCursor = unchecked(++takeCursor);
                } else {
                    //queue is empty
                    item = default;
                    return false;// EARLY RETURN
                }
            } finally { 
                if(cursorLockTaken) takeCursorLock.Exit(); 
            }

            slots[localTakeCursor & indexifier].Take(out item);
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

        private class ItemSlot {
            private const int CAPACITY = 16;//must be pow of 2
            private const int INDEXIFIER = CAPACITY-1;

            private ItemLeaf[] leaves = new ItemLeaf[CAPACITY];

            public void Take(out T item) {//todo: true returning instead of ref
                int i = 0;
                item = default;

                do {
                    ref ItemLeaf leaf = ref leaves[i & INDEXIFIER];

                    bool done = false;

                    try { } finally {
                        if (Interlocked.CompareExchange(ref leaf.IsLocked, 1, 0) == 0) {
                            if (leaf.HasItem) {
                                item = leaf.Item;
                                leaf.HasItem = false;
                                done = true;
                            }
                            leaf.IsLocked = 0;
                        }
                    }

                    if (done) {
                        return;
                    }

                    i++;
                } while (true);
            }

            public void Add(T item) {
                int i = 0;
                do {
                    ref ItemLeaf leaf = ref leaves[i & INDEXIFIER];

                    bool done = false;

                    try { } finally {
                        if (Interlocked.CompareExchange(ref leaf.IsLocked, 1, 0) == 0) {
                            if (!leaf.HasItem) {
                                leaf.Item = item;
                                leaf.HasItem = true;
                                done = true;
                            }
                            leaf.IsLocked = 0;
                        }
                    }

                    if (done) {
                        return;
                    }

                    i++;
                } while(true);
            }
        }

        private struct ItemLeaf {
            public T Item;
            public volatile bool HasItem;
            public volatile int IsLocked;
        }

    }
}