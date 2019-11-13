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
        private readonly ItemSlot[] items;
        private readonly int capacity;
        private readonly int indexifier;
        private volatile int addCursor = int.MinValue;
        private volatile int takeCursor = int.MinValue;
        private readonly bool isUnmanaged;
        private SpinLock addCursorLock = new SpinLock(false);
        private SpinLock takeCursorLock = new SpinLock(false);

        public FiniteConcurrentQueue(int capacity) : this(capacity, false) { }

        protected FiniteConcurrentQueue(int capacity, bool isUnmanaged) {
            if (capacity < 0) throw new ArgumentException("capacity cannot be negative");
            if (capacity > MAX_CAPACITY) throw new ArgumentException("capacity cannot be greater than " + MAX_CAPACITY);

            this.isUnmanaged = isUnmanaged;
            this.capacity = capacity;

            //need array size to be a power of 2 so we can do modulo with bitwise ANDing
            var arraySize = NextPositivePowerOf2(capacity);//todo: probably want a minimum size here to reduce contention
            indexifier = arraySize - 1;
            items = new ItemSlot[arraySize];
            for (int i = 0; i < arraySize; i++) {
                items[i] = new ItemSlot();
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

            T[] clone = (T[])items.Clone();

            int take = takeCursor;


        }

        public bool TryAdd(T item) {
            int localAddCursor;
            {
                int addCurLimit = capacity + takeCursor;
                bool cursorLockTaken = false;
                try { addCursorLock.Enter(ref cursorLockTaken);
                    if(addCursor < addCurLimit) {
                        localAddCursor = unchecked(++addCursor);
                    } else {
                        return false;// EARLY RETURN
                    }
                } finally { if(cursorLockTaken) addCursorLock.Exit(false); }
            }

            ItemSlot slot = items[localAddCursor & indexifier];

            bool adderLockTaken = false;
            try { slot.adderLock.Enter(ref adderLockTaken);

                while(slot.hasItem == true) {
                    //waiting for item to be taken
                    Thread.SpinWait(1);
                }

                Debug.Assert(!slot.hasItem);
                slot.Item = item;
                slot.hasItem = true;

                return true;

            } finally { if(adderLockTaken) slot.adderLock.Exit(false); }//todo: try removing false on all these calls
        }

        public bool TryTake(out T item) {
            int cur;
            bool cursorLockTaken = false;
            try { takeCursorLock.Enter(ref cursorLockTaken);

                if (takeCursor < addCursor) {
                    cur = unchecked(++takeCursor);
                } else {
                    //queue is empty
                    item = default;
                    return false;// EARLY RETURN
                }
            } finally { if(cursorLockTaken) takeCursorLock.Exit(false); }

            ItemSlot slot = items[cur & indexifier];

            bool takerLockTaken = false;
            try { slot.takerLock.Enter(ref takerLockTaken);

                while (slot.hasItem == false) {
                    //waiting for item to be added
                    Thread.SpinWait(1);
                }

                Debug.Assert(slot.hasItem);
                item = slot.Item;
                slot.hasItem = false;
                if (isUnmanaged) {
                    slot.Item = default;
                }

                return true;

            } finally { if(takerLockTaken) slot.takerLock.Exit(false); }
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
            public T Item;
            public volatile bool hasItem;
            public SpinLock adderLock = new SpinLock(false);
            public SpinLock takerLock = new SpinLock(false);
        }

    }
}