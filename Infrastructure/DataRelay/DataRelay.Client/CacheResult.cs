using System;

namespace MySpace.DataRelay.Client
{
    /// <summary>
    /// Represents a result from a Get operation.
    /// </summary>
    /// <typeparam name="T">The data type to be returned.</typeparam>
    /// <remarks>When the result is a miss, IsMiss will be false.</remarks>
    public struct CacheResult<T>
    {
        private readonly bool _hasResult;
        private readonly T _value;

        public bool IsMiss { get { return !_hasResult;  } }
        public T Value { get { return _value;  }}

        public static implicit operator T(CacheResult<T> cr)
        {
            if(cr.IsMiss) throw new InvalidOperationException("CacheResult is a miss, and cannot return a value.");
            return cr.Value;
        }

        public CacheResult(T from)
        {
            _hasResult = true;
            _value = from;
        }
    }
}
