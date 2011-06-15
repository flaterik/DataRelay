using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MySpace.Common.Threading
{
	public class TaskScheduler : IDisposable
	{
		private readonly SortedDictionary<ActionKey, Task> _actions = new SortedDictionary<ActionKey, Task>(new ActionKeyComparer());
		private readonly LinkedList<Task> _addQueue = new LinkedList<Task>();
		private readonly LinkedList<ActionKey> _removeQueue = new LinkedList<ActionKey>();
		private readonly Timer _timer;
		private bool _polling;

		public TaskScheduler(TimeSpan pollInterval)
		{
			_timer = new Timer(Poll);
			_timer.Change(pollInterval, pollInterval);
		}

		private void Poll(object obj)
		{
			var now = DateTime.UtcNow;
			var removeList = new List<ActionKey>();
			lock (_actions)
			{
				if (_polling) return;
				_polling = true;
			}

			try
			{
				foreach (var item in _actions)
				{
					if (item.Key.DueTime > now) break;

					removeList.Add(item.Key);
					item.Value.Execute();
				}
			}
			finally
			{
				if (removeList.Count > 0)
				{
					foreach (var key in removeList) _actions.Remove(key);
				}

				lock (_actions)
				{
					try
					{
						for (var node = _addQueue.First; node != null; node = _addQueue.First)
						{
							_actions[node.Value.Key] = node.Value;
							_addQueue.RemoveFirst();
						}

						for (var nodeToRemove = _removeQueue.First; nodeToRemove != null; nodeToRemove = _removeQueue.First)
						{
							_actions.Remove(nodeToRemove.Value);
							_removeQueue.RemoveFirst();
						}
					}
					finally
					{
						_polling = false;
					}
				}
			}
		}

		public IDisposable QueueTask(Action action, TimeSpan dueTime)
		{
			return QueueTask(o => action(), dueTime);
		}

		public IDisposable QueueTask(WaitCallback action, TimeSpan dueTime)
		{
			var key = new ActionKey(dueTime);
			var task = new Task(this, key, action);
			return QueueTask(task);
		}

		private IDisposable QueueTask(Task task)
		{
			lock (_actions)
			{
				if (_polling)
				{
					_addQueue.AddFirst(task);
				}
				else
				{
					_actions[task.Key] = task;
				}
			}
			return task;
		}

		private void RemoveTask(ActionKey key)
		{
			lock (_actions)
			{
				if (_polling)
				{
					_removeQueue.AddFirst(key);
				}
				else
				{
					_actions.Remove(key);
				}
			}
		}

		private class Task : IDisposable
		{
			readonly TaskScheduler _parent;
			WaitCallback _callback;

			public Task(TaskScheduler parent, ActionKey key, WaitCallback callback)
			{
				_parent = parent;
				Key = key;
				_callback = callback;
			}

			public ActionKey Key { get; private set; }

			public void Execute()
			{
				var callback = _callback;
				if (callback != null)
				{
					ThreadPool.QueueUserWorkItem(callback);
				}
			}

			public void Dispose()
			{
				_callback = null;
				_parent.RemoveTask(Key);
			}
		}

		private struct ActionKey
		{
			private static int _nextKey;

			public ActionKey(TimeSpan dueTime)
			{
				Key = Interlocked.Increment(ref _nextKey);
				DueTime = DateTime.UtcNow.Add(dueTime);
			}

			public readonly int Key;
			public readonly DateTime DueTime;
		}

		private class ActionKeyComparer : IComparer<ActionKey>
		{
			public int Compare(ActionKey x, ActionKey y)
			{
				var val = x.DueTime.CompareTo(y.DueTime);
				return val == 0
					? x.Key.CompareTo(y.Key)
					: val;
			}
		}

		public void Dispose()
		{
			_timer.Dispose();
		}
	}
}
