

namespace System.Collections.Generic
{
    [Flags]
    public enum SortedArrayOptions
    {
        None = 0,
        DisallowDuplicates = 1,
        SkipDuplicates = 2,
        DisallowResize = 4,
    }

    public class SortedArray<T> : IReadOnlyList<T>, IReadOnlyCollection<T>
    {
        private enum ShiftOperation
        {
            MoveRight = 0,
            MoveLeft = 1,
            Center = 2,
            Resize = 3,
        }

        public int Capacity => _storage.Length;
        public int Count => _count;

        private int LeftBuffer => _zeroIndex;
        private int RightBuffer => Capacity - _zeroIndex - _count;
        private bool CanMoveLeft => LeftBuffer > 0;
        private bool CanMoveRight => RightBuffer > 0;

        private int VIndex(int storageIndex) => storageIndex - _zeroIndex;
        private int SIndex(int virtualIndex) => virtualIndex + _zeroIndex;

        private const int DefaultCapacity = 4;
        private T[] _storage;
        private int _zeroIndex;
        private int _count;
        private int _version;
        private readonly IComparer<T> _comparer;

        public SortedArray(int capacity = DefaultCapacity, IComparer<T> comparer = null)
        {
            _storage = new T[capacity];
            _zeroIndex = CalculateZeroIndex(capacity, 0);
            _comparer = comparer;
        }

        public SortedArray(IEnumerable<T> initialValues, SortedArrayOptions options = SortedArrayOptions.None, IComparer<T> comparer = null)
        {
            _storage = new T[DefaultCapacity];
            _zeroIndex = CalculateZeroIndex(DefaultCapacity, 0);
            _comparer = comparer;

            foreach (var item in initialValues)
            {
                Add(item, options);
            }
        }

        public SortedArray(IReadOnlyCollection<T> initialValues, SortedArrayOptions options = SortedArrayOptions.None, IComparer<T> comparer = null)
        {
            _storage = new T[initialValues.Count];
            _zeroIndex = CalculateZeroIndex(initialValues.Count, 0);
            _comparer = comparer;

            foreach (var item in initialValues)
            {
                Add(item, options);
            }
        }

        private int CalculateZeroIndex(int capacity, int count)
        {
            if (capacity < 0 || count < 0 || capacity < count)
            {
                throw new ArgumentOutOfRangeException(nameof(CalculateZeroIndex), "capacity < 0 || count < 0 || capacity < count");
            }

            return (capacity - count) / 2;
        }

        private static (int left, int right) GetPartLengths(int insertIndex, int length, bool remove = false)
        {
            int leftPart = insertIndex;
            int rightPart = length - insertIndex - (remove ? 1 : 0);

            return (leftPart, rightPart);
        }

        private int Compare(ref T currentItem, ref T item)
        {
            if (_comparer != null)
            {
                return _comparer.Compare(currentItem, item);
            }
            else if (currentItem is IComparable<T> comparable)
            {
                return comparable.CompareTo(item);
            }
            else
            {
                return Comparer<T>.Default.Compare(currentItem, item);
            }
        }

        private int FindInsertPlace(ref T item, int arrayStartIndex, int arrayEndIndex)
        {
            int lo = arrayStartIndex;
            int hi = arrayEndIndex;

            while (lo <= hi)
            {
                //// PERF: `lo` or `hi` will never be negative inside the loop,
                //       so computing median using uints is safe since we know
                //       `length <= int.MaxValue`, and indices are >= 0
                //       and thus cannot overflow an uint.
                //       Saves one subtraction per loop compared to
                //       `int i = lo + (hi - lo) / 2;`
                int i = (int)(((uint)hi + (uint)lo) >> 1);

                int compareResult = Compare(ref _storage[SIndex(i)], ref item);

                if (compareResult == 0)
                {
                    return i;
                }
                else if (compareResult > 0)
                {
                    hi = i - 1;
                }
                else
                {
                    lo = i + 1;
                }
            }

            return ~lo;
        }

        private void CheckIndex(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index out of range");
            }
        }

        private void MoveLeft(int startndex, int length)
        {
            Array.Copy(_storage, SIndex(startndex), _storage, SIndex(startndex - 1), length);
        }

        private void MoveRight(int startIndex, int length)
        {
            Array.Copy(_storage, SIndex(startIndex), _storage, SIndex(startIndex + 1), length);
        }

        private void Move(T[] srcArray, int srcIndex, int length, T[] destArray, int destIndex)
        {
            Array.Copy(srcArray, SIndex(srcIndex), destArray, SIndex(destIndex), length);
        }

        private void Center(int insertPlace, (int left, int right) parts)
        {
            int newCount = Count + 1;
            int newZeroIndex = CalculateZeroIndex(Capacity, newCount);

            int delta = newZeroIndex - _zeroIndex;
            ShiftOperation shiftDirection = delta >= 0 ? ShiftOperation.MoveRight : ShiftOperation.MoveLeft;

            if (shiftDirection == ShiftOperation.MoveRight)
            {
                // Move right part of the array to the right
                Move(_storage, insertPlace, parts.right, _storage, insertPlace + delta + /*reserve 1 place for new item*/ 1);
                // Move left part of the array to the right
                Move(_storage, 0, parts.left, _storage, 0 + delta);
            }
            else
            {
                // Move left part of the array to the left
                Move(_storage, 0, parts.left, _storage, 0 + delta);
                // Move right part of the array to the left
                Move(_storage, insertPlace, parts.right, _storage, insertPlace + delta + /*reserve 1 place for new item*/ 1);
            }

            _zeroIndex = newZeroIndex;
        }

        private void Resize(int insertPlace, (int left, int right) parts, int newCapacity)
        {
            // for first insertion
            newCapacity = newCapacity == 0 ? DefaultCapacity : newCapacity;

            T[] oldStorage = _storage;
            _storage = new T[newCapacity];

            int newCount = Count + 1;
            int newZeroIndex = CalculateZeroIndex(newCapacity, newCount);

            int delta = newZeroIndex - _zeroIndex;
            ShiftOperation shiftDirection = delta >= 0 ? ShiftOperation.MoveRight : ShiftOperation.MoveLeft;

            if (shiftDirection == ShiftOperation.MoveRight)
            {
                // Move right part of the array to the right
                Move(oldStorage, insertPlace, parts.right, _storage, insertPlace + delta + /*reserve 1 place for new item*/ 1);
                // Move left part of the array to the right
                Move(oldStorage, 0, parts.left, _storage, 0 + delta);
            }
            else
            {
                // Move left part of the array to the left
                Move(oldStorage, 0, parts.left, _storage, 0 + delta);
                // Move right part of the array to the left
                Move(oldStorage, insertPlace, parts.right, _storage, insertPlace + delta + /*reserve 1 place for new item*/ 1);
            }

            _zeroIndex = newZeroIndex;
        }

        public int Find(T item) => Find(ref item);

        public int Find(ref T item)
        {
            if (_count == 0)
                return ~_count;

            if (_count == 1)
                return FindInsertPlace(ref item, 0, 0);

            int findResult;
            int currentIndex;

            // Try find item in the very low edge
            currentIndex = 0;
            findResult = FindInsertPlace(ref item, currentIndex, currentIndex);

            if (findResult > -1 || findResult == ~currentIndex)
            {
                return findResult;
            }

            // Try find item in the very high edge
            currentIndex = Count - 1;
            findResult = FindInsertPlace(ref item, currentIndex, currentIndex);

            if (findResult > -1 || findResult == ~(currentIndex + 1))
            {
                return findResult;
            }

            // Try find item in the remains
            return FindInsertPlace(ref item, 1, Count - 2);
        }

        private ShiftOperation GetOperation(int insertPlace, (int left, int right) parts, SortedArrayOptions options)
        {
            ShiftOperation operation;
            bool couldWeCenter = (Capacity - Count) >= DefaultCapacity;

            if (parts.left < parts.right)
            {
                // We should move the left part of the array to the edge
                operation = ShiftOperation.MoveLeft;

                if (!CanMoveLeft)
                {
                    if (couldWeCenter && (parts.right / (parts.left + 1)) >= 2)
                    {
                        // If we can't move left part and right part anyway is huge - then center all array                        
                        operation = ShiftOperation.Center;
                    }
                    else if (!CanMoveRight)
                    {
                        // We have no options
                        operation = ShiftOperation.Resize;
                    }
                    else
                    {
                        // in other way we can change the direction of the shift
                        operation = ShiftOperation.MoveRight;
                    }
                }
            }
            else
            {
                // We should move the right part of the array to the edge
                operation = ShiftOperation.MoveRight;

                if (!CanMoveRight)
                {
                    if (couldWeCenter && (parts.left / (parts.right + 1)) >= 2)
                    {
                        // If we can't move right part and left part anyway is huge - then center all array
                        operation = ShiftOperation.Center;
                    }
                    else if (!CanMoveLeft)
                    {
                        // We have no options
                        operation = ShiftOperation.Resize;
                    }
                    else
                    {
                        // in other way we can change the direction of the shift
                        operation = ShiftOperation.MoveLeft;
                    }
                }
            }

            return operation;
        }

        public int Add(T item, SortedArrayOptions options = SortedArrayOptions.None) => Add(ref item, options);

        public int Add(ref T item, SortedArrayOptions options = SortedArrayOptions.None)
        {
            _version++;
            int currentVersion = _version;
            int findResult = Find(ref item);

            if (findResult > -1)
            {
                if (options.HasFlag(SortedArrayOptions.DisallowDuplicates))
                    throw new ArgumentException("Item already exists in the array", nameof(item));
                else if (options.HasFlag(SortedArrayOptions.SkipDuplicates))
                    return ~findResult;
            }

            int insertPlace = findResult < 0 ? ~findResult : findResult;

            var parts = GetPartLengths(insertPlace, Count);

            var operation = GetOperation(insertPlace, parts, options);

            switch (operation)
            {
                case ShiftOperation.MoveLeft:
                    MoveLeft(0, parts.left);
                    _zeroIndex--;
                    break;
                case ShiftOperation.MoveRight:
                    MoveRight(insertPlace, parts.right);
                    break;
                case ShiftOperation.Center:
                    Center(insertPlace, parts);
                    break;
                case ShiftOperation.Resize:
                    if (options.HasFlag(SortedArrayOptions.DisallowResize))
                        throw new InvalidOperationException("Can't resize array");
                    else
                        Resize(insertPlace, parts, Capacity * 2);
                    break;
            }

            _count++;
            _storage[SIndex(insertPlace)] = item;

            if (currentVersion != _version)
                throw new Exception("SortedArray was changed");

            return findResult;
        }

        public T this[int index]
        {
            get
            {
                CheckIndex(index);
                return _storage[SIndex(index)];
            }
        }

        public void Remove(int index)
        {
            _version++;

            CheckIndex(index);

            var parts = GetPartLengths(index, _count, true);

            ShiftOperation shiftDirection;

            if (parts.left < parts.right)
            {
                shiftDirection = ShiftOperation.MoveRight;
            }
            else
            {
                shiftDirection = ShiftOperation.MoveLeft;
            }

            switch (shiftDirection)
            {
                case ShiftOperation.MoveLeft:
                    MoveLeft(index + 1, parts.right);
                    break;
                case ShiftOperation.MoveRight:
                    MoveRight(0, parts.left);
                    _zeroIndex++;
                    break;
            }

            _count--;
        }

        public IEnumerator<T> GetEnumerator()
        {
            int currentVersion = _version;
            for (int i = 0; i < Count; i++)
            {
                if (currentVersion == _version)
                    yield return _storage[SIndex(i)];
                else
                    throw new Exception("SortedArray was changed");
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
