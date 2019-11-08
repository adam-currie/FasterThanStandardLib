using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace FasterThanStandardLib {

    public class FiniteConcurrentQueueUnmanaged<T> : FiniteConcurrentQueue<T> where T : unmanaged {
        public FiniteConcurrentQueueUnmanaged(int capacity) : base(capacity, true) { }
    }

    public class FiniteConcurrentQueue<T> : IProducerConsumerCollection<T> {
        private static readonly int MAX_CAPACITY = 1073741824;//2^30, largest capacity we can allow because next highest power of 2 would wrap
        private readonly T[] items;
        private readonly int capacity;
        private readonly int indexifier;
        private volatile int addCursor = int.MinValue;
        private volatile int takeCursor = int.MinValue;
        private readonly bool isUnmanaged;

        public FiniteConcurrentQueue(int capacity) : this(capacity, false) {}

        protected FiniteConcurrentQueue(int capacity, bool isUnmanaged) {
            if (capacity < 0) throw new ArgumentException("capacity cannot be negative");
            if (capacity > MAX_CAPACITY) throw new ArgumentException("capacity cannot be greater than " + MAX_CAPACITY);

            this.isUnmanaged = isUnmanaged;
            this.capacity = capacity;

            //need array size to be a power of 2 so we can do modulo with bitwise ANDing
            var arraySize = NextPositivePowerOf2(capacity);
            indexifier = arraySize - 1;
            items = new T[arraySize];
        }

        public int Count => Math.Max(0, addCursor-takeCursor);

        public bool IsSynchronized => false;//although this code is thread safe it doesn't use lock synchronization

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
        }

        public bool TryAdd(T item) {
            unchecked {
                if (addCursor - takeCursor < capacity) {
                    int stashedAddCursor = Interlocked.Increment(ref addCursor);
                    if (stashedAddCursor - takeCursor <= capacity) {
                        //success
                        items[stashedAddCursor & indexifier] = item;
                        return true;
                    } else {
                        //fix it
                        Interlocked.Decrement(ref addCursor);
                    }
                }
            }

            return false;
        }

        public bool TryTake(out T item) {
            unchecked {
                if (takeCursor < addCursor) {
                    int stashedTakeCursor = Interlocked.Increment(ref takeCursor);
                    if (stashedTakeCursor <= addCursor) {
                        //success

                        if (isUnmanaged) {
                            //don't need to get rid of old value because it can't leak references
                            item = items[stashedTakeCursor & indexifier];
                        } else {
                            //using ref into array to avoid double lookup for take and remove(actually tested this don't @ me)
                            ref T itemRef = ref items[stashedTakeCursor & indexifier];
                            item = itemRef;
                            itemRef = default;
                        }

                        return true;
                    } else {
                        //fix it
                        Interlocked.Decrement(ref takeCursor);
                    }
                }
            }

            item = default;
            return false;
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

    }
}
