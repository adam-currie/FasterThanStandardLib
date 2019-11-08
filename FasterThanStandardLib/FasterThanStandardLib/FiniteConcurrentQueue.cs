using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace FasterThanStandardLib {

    public class FiniteConcurrentQueue<T> : IProducerConsumerCollection<T> {
        private static readonly int MAX_CAPACITY = 1073741824;//2^30, largest capacity we can allow because next highest power of 2 would wrap
        private readonly T[] items;
        private readonly int capacity;
        private readonly int indexifier;
        private readonly bool isUnmanaged = CheckIfUnmanaged(typeof(T));
        private volatile int addCursor = int.MinValue;
        private volatile int takeCursor = int.MinValue;

        class RequiresUnmanaged<U> where U : unmanaged { }

        private static bool CheckIfUnmanaged(Type type) {
            try { 
                typeof(RequiresUnmanaged<>).MakeGenericType(type); 
                return true; 
            } catch (Exception) { 
                return false; 
            }
        }

        public FiniteConcurrentQueue(int capacity) {
            if (capacity < 0) throw new ArgumentException("capacity cannot be negative");
            if (capacity > MAX_CAPACITY) throw new ArgumentException("capacity cannot be greater than " + MAX_CAPACITY);

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
                        //success (EARLY RETURN)
                        item = items[stashedTakeCursor & indexifier];
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
