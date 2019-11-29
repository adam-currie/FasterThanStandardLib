using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace FasterThanStandardLib {

    public class FiniteConcurrentQueue<T> : IProducerConsumerCollection<T> {
        private static readonly int MAX_CAPACITY = 1073741824;//2^30, largest capacity we can allow because next highest power of 2 would wrap
        private static readonly int MIN_CAPACITY = 1024;// we want a minimum size to reduce contention when looping back around
        private static readonly int MIN_CAPACITY_SMALL = 256;

        private readonly ItemSlot[] slots;
        private readonly int capacity;
        private readonly int indexifier;
        private volatile int addCursor = 0;//todo: test these from int.minvalue
        private volatile int takeCursor = 0;
        private UnFairSpinLock addCursorLock;
        private UnFairSpinLock takeCursorLock;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="capacity"></param>
        /// <param name="small"> 
        ///     Smaller memory footprint in short queues at the expense of performance under very high concurrency.
        /// </param>
        public FiniteConcurrentQueue(int capacity, bool small = false) {
            if (capacity < 0) throw new ArgumentException("capacity cannot be negative");
            if (capacity > MAX_CAPACITY) throw new ArgumentException("capacity cannot be greater than " + MAX_CAPACITY);

            this.capacity = capacity;

            //need array size to be a power of 2 so we can do modulo with bitwise ANDing
            var arraySize = NextPositivePowerOf2(capacity + (small ? MIN_CAPACITY_SMALL : MIN_CAPACITY));

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

            bool full = false;
            try { } finally {
                addCursorLock.UnsafeEnter();
                if (addCursor < addCurLimit) {
                    localAddCursor = unchecked(++addCursor);
                } else {
                    full = true;
                }
                addCursorLock.Exit();
            }

            if (full) {
                return false;
            }

            slots[localAddCursor & indexifier].Add(item);
            return true;
        }

        public bool TryTake(out T item) {
            item = default;
            int localTakeCursor = 0;

            if (takeCursor >= addCursor) {
                return false;
            }

            bool empty = false;
            try { } finally {
                takeCursorLock.UnsafeEnter();
                if (takeCursor < addCursor) {
                    localTakeCursor = unchecked(++takeCursor);
                } else {
                    empty = true;
                }
                takeCursorLock.Exit();
            }

            if (empty) {
                return false;
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
            private const int STATE_EMPTY = 0;
            private const int STATE_FULL = 1;
            private const int STATE_LOCKED_EMPTYING = 2;
            private const int STATE_LOCKED_FILLING = 3;

            private T item;
            private int state;
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Take() {
                T result;
                try { } finally {
                    LockForEmptying();
                    result = item;
                    item = default;
                    Volatile.Write(ref state, STATE_EMPTY);
                }
                return result;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(T item) {
                try { } finally {
                    LockForFilling();
                    this.item = item;
                    Volatile.Write(ref state, STATE_FULL);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void LockForEmptying() => Lock(STATE_FULL, STATE_LOCKED_EMPTYING, STATE_LOCKED_FILLING);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void LockForFilling() => Lock(STATE_EMPTY, STATE_LOCKED_FILLING, STATE_LOCKED_EMPTYING);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void Lock(int preLockState, int intraLockState, int oppositeIntraLockState) {
                int testedState;
                while (preLockState != (testedState = Interlocked.CompareExchange(ref state, intraLockState, preLockState))) {
                    //if we are in the opposite lock them we must be close to having the chance to grab it so try twice
                    if (testedState == oppositeIntraLockState &&
                        preLockState == Interlocked.CompareExchange(ref state, intraLockState, preLockState))
                        return;

                    Thread.Sleep(1);
                }
            }

        }

    }

}