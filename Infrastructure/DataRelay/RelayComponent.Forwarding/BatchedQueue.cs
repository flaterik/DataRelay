using System;
using System.Collections.Generic;
using System.Threading;
using MySpace.Common.HelperObjects;
using MySpace.Logging;
using Action=System.Action;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// Responsible for providing a thread safe FIFO queue.
	/// </summary>
	/// <typeparam name="T">The datatype of the items in the queue, constrained to classes.</typeparam>
	internal class BatchedQueue<T> where T : class
	{
		private List<T> _list;
		private readonly Timer _timer;
		private TimeSpan _timeout;
		private int _batchSize;
		private readonly Action<List<T>> _processBatchMethod;
		private bool _processOnce = false;
		private static readonly LogWrapper _log = new LogWrapper();
		private readonly object _syncRoot = new object();

		/// <summary>
		/// 	<para>Initializes an instance of the <see cref="BatchedQueue{T}"/> class.</para>
		/// </summary>
		/// <param name="batchSize">The number of items in the list required before calling</param>
		/// <param name="processBatchMethod">The method to call when the queue is empty.  Must not be <lang ref="null" />.</param>
		/// <param name="timeout">How long to wait before processing the batch if the batch size is not reached..</param>
		/// <exception cref="ArgumentOutOfRangeException">
		/// 	<para>Thrown when the argument <paramref name="batchSize"/> is less than or equal to zero.</para>
		/// </exception>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="processBatchMethod"/> is null.</exception>
		public BatchedQueue(int batchSize, TimeSpan timeout, Action<List<T>> processBatchMethod)
		{
			if (batchSize <= 0) throw new ArgumentOutOfRangeException("batchSize", "Needs to be greater than or equal to 1");
			if (processBatchMethod == null) throw new ArgumentNullException("processBatchMethod");
			_processBatchMethod = processBatchMethod;
			_timer = new Timer((obj) => _processBatch(), null, Timeout.Infinite, Timeout.Infinite);
			_timeout = timeout;
			_batchSize = batchSize;
		}

		/// <summary>
		/// Gets or sets the size of the batches.
		/// </summary>
		public int BatchSize
		{
			get { return _batchSize; }
			set { _batchSize = value; }
		}

		/// <summary>
		/// Gest or sets the value indicating how long to wait to process a batch if the <see cref="BatchSize"/> has not been reached.
		/// </summary>
		public TimeSpan BatchTimeout
		{
			get { return _timeout; }
			set
			{
				if (value != _timeout)
				{
					lock(_syncRoot)
					{
						_timeout = value; //ensure that other processors cache get the corrected value
						_processBatch();
					}
				}
			}
		}

		/// <summary>
		/// The next item call to <see cref="Enqueue"/> will the batch to be processed.
		/// </summary>
		public void SetProcessOnce()
		{
			lock (_syncRoot)
			{
				_processOnce = true;
			}
		}

		/// <summary>
		/// Enqueue a new item onto the queue.
		/// </summary>
		/// <param name="newItem">The Item to push, can be null.</param>
		public void Enqueue(T newItem)
		{
			lock (_syncRoot)
			{
				if (_list == null)
				{
					_list = new List<T>(_batchSize);
				}
				
				_list.Add(newItem);

				if (_list.Count >= _batchSize || _processOnce)
				{
					_processBatch();
				}
				else if (_list.Count == 1)
				{
					_startTimer();
				}
				
				_processOnce = false;
			}
		}

		private void _processBatch()
		{
			try
			{
				List<T> list;
				lock (_syncRoot)
				{
					_stopTimer();
					list = _list;
					_list = null;
				}

				if (list != null)
				{
					_processBatchMethod(list); //list size should never be greater than _batchSize
				}
			}
			catch (Exception exc)
			{
				_log.Error(exc);
			}
		}

		private void _startTimer()
		{
			try
			{
				_timer.Change(_timeout, TimeSpan.FromMilliseconds(-1));
			}
			catch (Exception exc)
			{
				_log.Error(exc);
			}
		}

		private void _stopTimer()
		{
			try
			{
				_timer.Change(Timeout.Infinite, Timeout.Infinite);
			}
			catch (Exception exc)
			{
				_log.Error(exc);
			}
		}
	}
}
