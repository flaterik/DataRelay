using System;
using System.Diagnostics;

namespace MySpace.Metrics
{
    /// <summary>
    /// Tracks a sliding window of time for statistics gathering.
    /// </summary>
    public class SlidingWindowAverage
    {
        #region private fields
        private TimeSpan _windowSize;
        private TimeSpan _granularity;
        private Averager[] _averagers;
        private int _currentAverageIndex;
        private TimeSpan _currentIndexStartTime;
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly object _syncRoot = new object();
        #pragma warning disable 649  // Averager is a struct.
        private readonly Averager _recentAverage = new Averager();
        #pragma warning restore 649
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the <see cref="SlidingWindowAverage"/> class with a default granularity of 1 second.
        /// </summary>
        /// <param name="windowSize">Size of the sliding time window.  Larger values consider more data points in the computations.  This value cannot be less than 2 seconds.</param>
        public SlidingWindowAverage(TimeSpan windowSize)
            : this(windowSize, TimeSpan.FromSeconds(1))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SlidingWindowAverage"/>.
        /// </summary>
        /// <param name="windowSize">Size of the sliding time window.  Larger values consider more data points in the computations.  This value cannot be zero.</param>
        /// <param name="granularity">The granularity used in average computations, the smaller this value, the less stable the average. This parameter must be less than 1/2 of the windowSize.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if windowSize or granularity are out of range.</exception>
        public SlidingWindowAverage(TimeSpan windowSize, TimeSpan granularity)
        {
            _stopwatch.Start();
            Resize(windowSize, granularity);
        }
        #endregion

        #region long Average
        /// <summary>
        /// Gets the average for all datapoints collected within the specified window of time.
        /// </summary>
        /// <value>The average.</value>
        public long Average
        {
            get
            {
                UpdateStatistics();
                return _recentAverage.Average;
            }
        }
        #endregion

        /// <summary>
        /// Adds a data point the statistics collection.
        /// </summary>
        /// <param name="value">The data point.</param>
        public void AddStatistic(long value)
        {
            lock (_syncRoot)
            {
                UpdateStatistics();

                _averagers[_currentAverageIndex].Add(value);
                _recentAverage.Add(value);
            }
        }

        /// <summary>
        /// Resets the statistics to 0.
        /// </summary>
        public void Reset()
        {
            lock (_syncRoot)
            {
                for (int i = 0; i < _averagers.Length; ++i) _averagers[i].Reset();
                _currentAverageIndex = 0;
                _recentAverage.Reset();
            }
        }

        /// <summary>
        /// Resizes the window size for gathering latency statistics.
        /// </summary>
        /// <param name="windowSize">Size of the sliding time window.  Larger values consider more data points in the computations.  This value cannot be zero.</param>
        /// <param name="granularity">The granularity used in average computations, the smaller this value, the less stable the average. This parameter must be less than 1/2 of the windowSize.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if windowSize or granularity are out of range.</exception>
        /// <remarks>Calling Resize() will reset any previously accumulated statistics.</remarks>
        public void Resize(TimeSpan windowSize, TimeSpan granularity)
        {
            if(windowSize.Ticks == 0) throw new ArgumentOutOfRangeException("windowSize", "Window size cannot be zero.");
            if (granularity.Ticks > windowSize.Ticks / 2) throw new ArgumentOutOfRangeException("granularity", "granularity must be at most 1/2 the window size.");
            _windowSize = windowSize;
            _granularity = granularity;
            _averagers = new Averager[_windowSize.Ticks / _granularity.Ticks];
            for(var i=0;i<_averagers.Length; ++i) _averagers[i] = new Averager();
            Reset();
        }

        #region TimeSpan WindowSize
        /// <summary>
        /// Gets the size of the sliding time window. 
        /// </summary>
        /// <value>The size of the window.</value>
        public TimeSpan WindowSize { get { return _windowSize; } }
        #endregion

        #region TimeSpan Granularity
        /// <summary>
        /// Gets the granularity.
        /// </summary>
        /// <value>The granularity.</value>
        public TimeSpan Granularity { get { return _granularity; } }
        #endregion

        #region private UpdateStatistics
        private void UpdateStatistics()
        {
            var now = _stopwatch.Elapsed;
            lock (_syncRoot)
            {
                var indexDelta = (now.Ticks - _currentIndexStartTime.Ticks)/_granularity.Ticks;
                if (indexDelta >= _averagers.Length)
                {
                    Reset();
                    indexDelta = 0;
                }
                while (indexDelta > 0)
                {
                    --indexDelta;
                    _currentAverageIndex = (_currentAverageIndex + 1)%_averagers.Length;
                    _recentAverage.Subtract(_averagers[_currentAverageIndex]);
                    _averagers[_currentAverageIndex].Reset();
                    _currentIndexStartTime += _granularity;
                }
            }
        }
        #endregion

        #region private struct Averager
        [DebuggerDisplay("Sum = {_sum} Count = {_count}")]
        private class Averager
        {
            private long _sum;
            private int _count;

            public void Reset()
            {
                _sum = 0;
                _count = 0;
            }

            /// <summary>
            /// Gets the average.
            /// </summary>
            /// <value>The average.</value>
            /// <exception cref="DivideByZeroException">Thrown if this property is accessed before Add() is called.</exception>
            public long Average
            {
                get
                {
                    var count = _count;
                    return count != 0 ? _sum / count : 0; 
                }
            }

            public void Add(long value)
            {
                _sum += value;
                ++_count;
            }

            public void Subtract(Averager averager)
            {
                _sum -= averager._sum;
                _count -= averager._count;
            }
        }
        #endregion
    }
}
