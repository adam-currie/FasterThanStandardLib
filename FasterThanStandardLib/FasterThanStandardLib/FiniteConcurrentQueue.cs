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
            var arraySize = NextPositivePowerOf2(capacity+512);//debug//todo: probably want a minimum size here to reduce contention
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

            ref var slot = ref slots[localAddCursor & indexifier];
            bool adderLockTaken = false;
            try {
                slot.adderLock.Enter(ref adderLockTaken);
                while (slot.HasItem) { }
                slot.Item = item;
                slot.HasItem = true;
            } finally {
                if (adderLockTaken) slot.adderLock.Exit();
            }

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

            ref var slot = ref slots[localTakeCursor & indexifier];
            bool takerLockTaken = false;
            try {
                slot.takerLock.Enter(ref takerLockTaken);
                while (slot.HasItem == false) { }
                item = slot.Item;
                if (!isUnmanaged) {
                    slot.Item = default;
                }
                slot.HasItem = false;
            } finally {
                if (takerLockTaken) slot.takerLock.Exit();
            }

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
            public T Item;
            public volatile bool HasItem;
            public HighContentionSpinLock adderLock;
            public HighContentionSpinLock takerLock;
        }

    }
}