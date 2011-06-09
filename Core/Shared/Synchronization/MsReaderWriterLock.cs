using System;
using System.Security.Permissions;
using System.Threading;
using frameworkPolicy = System.Threading.LockRecursionPolicy;

namespace MySpace.Common.HelperObjects
{
	[Obsolete("Use System.Threading.LockRecursionPolicy instead", false)]
	/// <summary>Specifies whether a lock can be entered multiple times by the same thread.</summary>
	public enum LockRecursionPolicy
	{
		/// <summary>
		///	<para>Indicates that users may not attempt to aquire the lock more than once.</para>
		/// </summary>
		NoRecursion,

		/// <summary>
		///	<para>Indicates that users may aquire the lock more than once recursively.</para>
		/// </summary>
		SupportsRecursion
	}

	/// <summary>
	///	<para>A parameterless method signature.</para>
	/// </summary>
	public delegate void Action();

	/// <summary>
	/// 	<para>A lock that allows multiple readers and a single writer. This lock also protects
	/// 	against asynchronous exceptions like <see cref="ThreadAbortException"/>.</para>
	/// </summary>
	public class MsReaderWriterLock : IDisposable
	{
		#region :: Child Lock Classes ::
		class ReadLockCleanup : IDisposable
		{
			private readonly ReaderWriterLockSlim _lock;
			public ReadLockCleanup(ReaderWriterLockSlim _lock)
			{
				this._lock = _lock;
			}
			void IDisposable.Dispose()
			{
				_lock.ExitReadLock();
			}
		}

		class ReadUpgradableLockCleanup : IDisposable
		{
			private readonly ReaderWriterLockSlim _lock;
			public ReadUpgradableLockCleanup(ReaderWriterLockSlim _lock)
			{
				this._lock = _lock;
			}
			void IDisposable.Dispose()
			{
				_lock.ExitUpgradeableReadLock();
			}
		}

		class WriteLockCleanup : IDisposable
		{
			private readonly ReaderWriterLockSlim _lock;
			public WriteLockCleanup(ReaderWriterLockSlim _lock)
			{
				this._lock = _lock;
			}
			void IDisposable.Dispose()
			{
				_lock.ExitWriteLock();
			}
		}
		#endregion :: Child Lock Classes ::

		private readonly ReaderWriterLockSlim _lock;

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="ReaderWriterLock"/> class.</para>
		/// </summary>
		/// <param name="policy">The policy that this lock will enforce.</param>
		[Obsolete("Use System.Threading.LockRecursionPolicy overload instead", false)]
		public MsReaderWriterLock(LockRecursionPolicy policy) :
			this(Translate(policy))
		{
		}

#pragma warning disable 618,612
		private static frameworkPolicy Translate(LockRecursionPolicy policy)
		{
			switch(policy)
			{
				case LockRecursionPolicy.NoRecursion:
					return frameworkPolicy.NoRecursion;
				case LockRecursionPolicy.SupportsRecursion:
					return frameworkPolicy.SupportsRecursion;
				default:
					throw new ApplicationException(string.Format(
						"Policy {0} not supported", policy));
			}
		}
#pragma warning restore 618,612

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="ReaderWriterLock"/> class.</para>
		/// </summary>
		/// <param name="policy">The policy that this lock will enforce.</param>
		public MsReaderWriterLock(frameworkPolicy policy)
		{
			_lock = new ReaderWriterLockSlim(policy);
		}

		/// <summary>
		/// 	<para>Performs application-defined tasks associated with
		///		freeing, releasing, or resetting unmanaged resources.</para>
		/// </summary>
		public void Dispose()
		{
			_lock.Dispose();
		}

		/// <summary>
		/// 	<para>Runs the specified action safely inside a read context.</para>
		/// </summary>
		/// <param name="action">
		///	<para>The action to run.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		///	<para>Thrown when <paramref name="action"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <remarks>
		///	<para><paramref name="action"/> is protected from asynchronous
		///	exceptions like <see cref="ThreadAbortException"/>. If another thread
		///	attempts to perform an abort while <paramref name="action"/> is running
		///	it will be blocked until <paramref name="action"/> completes and the
		///	lock is released.</para>
		/// </remarks>
		public void Read(Action action)
		{
			if (action == null) throw new ArgumentNullException("action");

			_runLockedActionInterruptible<object>(
				_lock,
				null,
				l => l.EnterReadLock(),
				l => l.ExitReadLock(),
				o => action());
		}

		/// <summary>
		/// 	<para>Runs the specified action safely inside a read context.</para>
		/// </summary>
		/// <typeparam name="T">
		///	<para>The type of state object to pass to <paramref name="action"/>.</para>
		/// </typeparam>
		/// <param name="state">
		///	<para>A state object to pass to <paramref name="action"/>.</para>
		/// </param>
		/// <param name="action">
		///	<para>The action to run.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		///	<para>Thrown when <paramref name="action"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <remarks>
		///	<para><paramref name="action"/> is protected from asynchronous
		///	exceptions like <see cref="ThreadAbortException"/>. If another thread
		///	attempts to perform an abort while <paramref name="action"/> is running
		///	it will be blocked until <paramref name="action"/> completes and the
		///	lock is released.</para>
		/// </remarks>
		public void Read<T>(T state, Action<T> action)
		{
			if (action == null) throw new ArgumentNullException("action");

			_runLockedActionInterruptible<T>(
				_lock,
				state,
				l => l.EnterReadLock(),
				l => l.ExitReadLock(),
				action);
		}

		/// <summary>
		/// 	<para>Runs the specified action safely inside a read context.
		/// 	<paramref name="action"/> may perform a write action in this
		/// 	method whereas in <see cref="Read"/> it could not.</para>
		/// </summary>
		/// <param name="action">
		///	<para>The action to run.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		///	<para>Thrown when <paramref name="action"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <remarks>
		///	<para>Only one upgradable reader may enter concurrently with other
		///	upgradable readers. However other non-upgradable readers are still
		///	permitted to enter concurrently with a single upgradable reader.</para>
		///	<para><paramref name="action"/> is protected from asynchronous
		///	exceptions like <see cref="ThreadAbortException"/>. If another thread
		///	attempts to perform an abort while <paramref name="action"/> is running
		///	it will be blocked until <paramref name="action"/> completes and the
		///	lock is released.</para>
		/// </remarks>
		public void ReadUpgradable(Action action)
		{
			if (action == null) throw new ArgumentNullException("action");

			_runLockedActionInterruptible<object>(
				_lock,
				null,
				l => l.EnterUpgradeableReadLock(),
				l => l.ExitUpgradeableReadLock(),
				o => action());
		}

		/// <summary>
		/// 	<para>Runs the specified action safely inside a read context.
		/// 	<paramref name="action"/> may perform a write action in this
		/// 	method whereas in <see cref="Read{T}"/> it could not.</para>
		/// </summary>
		/// <typeparam name="T">
		///	<para>The type of state object to pass to <paramref name="action"/>.</para>
		/// </typeparam>
		/// <param name="state">
		///	<para>A state object to pass to <paramref name="action"/>.</para>
		/// </param>
		/// <param name="action">
		///	<para>The action to run.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		///	<para>Thrown when <paramref name="action"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <remarks>
		///	<para>Only one upgradable reader may enter concurrently with other
		///	upgradable readers. However other non-upgradable readers are still
		///	permitted to enter concurrently with a single upgradable reader.</para>
		///	<para><paramref name="action"/> is protected from asynchronous
		///	exceptions like <see cref="ThreadAbortException"/>. If another thread
		///	attempts to perform an abort while <paramref name="action"/> is running
		///	it will be blocked until <paramref name="action"/> completes and the
		///	lock is released.</para>
		/// </remarks>
		public void ReadUpgradable<T>(T state, Action<T> action)
		{
			if (action == null) throw new ArgumentNullException("action");

			_runLockedActionInterruptible<T>(
				_lock,
				state,
				l => l.EnterUpgradeableReadLock(),
				l => l.ExitUpgradeableReadLock(),
				action);
		}

		/// <summary>
		/// 	<para>Runs the specified action safely inside a read context.
		/// 	If the invoking thread is blocked for longer than the specified
		/// 	timeout <paramref name="action"/> will not be run and 
		/// 	<see langword="false"/> will be returned.</para>
		/// </summary>
		/// <param name="millisecondsTimeout">
		///	<para>The number of milliseconds to wait, or -1
		///	<see cerf="Timeout.Infinite"/> to wait indefinitely.</para>
		/// </param>
		/// <param name="action">
		///	<para>The action to run.</para>
		/// </param>
		/// <returns>
		///	<para><see langword="true"/> if the read ran successfully;
		///	<see langword="false"/> if the read timed out.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para>Thrown when <paramref name="action"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <remarks>
		///	<para><paramref name="action"/> is protected from asynchronous
		///	exceptions like <see cref="ThreadAbortException"/>. If another thread
		///	attempts to perform an abort while <paramref name="action"/> is running
		///	it will be blocked until <paramref name="action"/> completes and the
		///	lock is released.</para>
		/// </remarks>
		public bool TryRead(int millisecondsTimeout, Action action)
		{
			if (action == null) throw new ArgumentNullException("action");

			return _tryRunLockedActionInterruptible<object, int>(
				_lock,
				null,
				millisecondsTimeout,
				(l, t) => l.TryEnterReadLock(t),
				l => l.ExitReadLock(),
				o => action());
		}

		/// <summary>
		/// 	<para>Runs the specified action safely inside a read context.
		/// 	If the invoking thread is blocked for longer than the specified
		/// 	timeout <paramref name="action"/> will not be run and 
		/// 	<see langword="false"/> will be returned.</para>
		/// </summary>
		/// <typeparam name="T">
		///	<para>The type of state object to pass to <paramref name="action"/>.</para>
		/// </typeparam>
		/// <param name="state">
		///	<para>A state object to pass to <paramref name="action"/>.</para>
		/// </param>
		/// <param name="millisecondsTimeout">
		///	<para>The number of milliseconds to wait, or -1
		///	<see cerf="Timeout.Infinite"/> to wait indefinitely.</para>
		/// </param>
		/// <param name="action">
		///	<para>The action to run.</para>
		/// </param>
		/// <returns>
		///	<para><see langword="true"/> if the read ran successfully;
		///	<see langword="false"/> if the read timed out.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para>Thrown when <paramref name="action"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <remarks>
		///	<para><paramref name="action"/> is protected from asynchronous
		///	exceptions like <see cref="ThreadAbortException"/>. If another thread
		///	attempts to perform an abort while <paramref name="action"/> is running
		///	it will be blocked until <paramref name="action"/> completes and the
		///	lock is released.</para>
		/// </remarks>
		public bool TryRead<T>(T state, int millisecondsTimeout, Action<T> action)
		{
			if (action == null) throw new ArgumentNullException("action");

			return _tryRunLockedActionInterruptible<T, int>(
				_lock,
				state,
				millisecondsTimeout,
				(l, t) => l.TryEnterReadLock(t),
				l => l.ExitReadLock(),
				action);
		}

		/// <summary>
		/// 	<para>Runs the specified action safely inside a read context.
		/// 	If the invoking thread is blocked for longer than the specified
		/// 	timeout <paramref name="action"/> will not be run and 
		/// 	<see langword="false"/> will be returned.</para>
		/// </summary>
		/// <param name="timeout">
		///	<para>The time to wait, or -1 milliseconds
		///	<see cerf="Timeout.Infinite"/> to wait indefinitely.</para>
		/// </param>
		/// <param name="action">
		///	<para>The action to run.</para>
		/// </param>
		/// <returns>
		///	<para><see langword="true"/> if the read ran successfully;
		///	<see langword="false"/> if the read timed out.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para>Thrown when <paramref name="action"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <remarks>
		///	<para><paramref name="action"/> is protected from asynchronous
		///	exceptions like <see cref="ThreadAbortException"/>. If another thread
		///	attempts to perform an abort while <paramref name="action"/> is running
		///	it will be blocked until <paramref name="action"/> completes and the
		///	lock is released.</para>
		/// </remarks>
		public bool TryRead(TimeSpan timeout, Action action)
		{
			if (action == null) throw new ArgumentNullException("action");

			return _tryRunLockedActionInterruptible<object, TimeSpan>(
				_lock,
				null,
				timeout,
				(l, t) => l.TryEnterReadLock(t),
				l => l.ExitReadLock(),
				o => action());
		}

		/// <summary>
		/// 	<para>Runs the specified action safely inside a read context.
		/// 	If the invoking thread is blocked for longer than the specified
		/// 	timeout <paramref name="action"/> will not be run and 
		/// 	<see langword="false"/> will be returned.</para>
		/// </summary>
		/// <typeparam name="T">
		///	<para>The type of state object to pass to <paramref name="action"/>.</para>
		/// </typeparam>
		/// <param name="state">
		///	<para>A state object to pass to <paramref name="action"/>.</para>
		/// </param>
		/// <param name="timeout">
		///	<para>The time to wait, or -1 milliseconds
		///	<see cerf="Timeout.Infinite"/> to wait indefinitely.</para>
		/// </param>
		/// <param name="action">
		///	<para>The action to run.</para>
		/// </param>
		/// <returns>
		///	<para><see langword="true"/> if the read ran successfully;
		///	<see langword="false"/> if the read timed out.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para>Thrown when <paramref name="action"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <remarks>
		///	<para><paramref name="action"/> is protected from asynchronous
		///	exceptions like <see cref="ThreadAbortException"/>. If another thread
		///	attempts to perform an abort while <paramref name="action"/> is running
		///	it will be blocked until <paramref name="action"/> completes and the
		///	lock is released.</para>
		/// </remarks>
		public bool TryRead<T>(T state, TimeSpan timeout, Action<T> action)
		{
			if (action == null) throw new ArgumentNullException("action");

			return _tryRunLockedActionInterruptible<T, TimeSpan>(
				_lock,
				state,
				timeout,
				(l, t) => l.TryEnterReadLock(t),
				l => l.ExitReadLock(),
				action);
		}

		/// <summary>
		/// 	<para>Runs the specified action safely inside a read context.
		/// 	If the invoking thread is blocked for longer than the specified
		/// 	timeout <paramref name="action"/> will not be run and 
		/// 	<see langword="false"/> will be returned. A write action
		/// 	may also be performed in this context unlike with a normal read.</para>
		/// </summary>
		/// <param name="millisecondsTimeout">
		///	<para>The number of milliseconds to wait, or -1
		///	<see cerf="Timeout.Infinite"/> to wait indefinitely.</para>
		/// </param>
		/// <param name="action">
		///	<para>The action to run.</para>
		/// </param>
		/// <returns>
		///	<para><see langword="true"/> if the read ran successfully;
		///	<see langword="false"/> if the read timed out.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para>Thrown when <paramref name="action"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <remarks>
		///	<para>Only one upgradable reader may enter concurrently with other
		///	upgradable readers. However other non-upgradable readers are still
		///	permitted to enter concurrently with a single upgradable reader.</para>
		///	<para><paramref name="action"/> is protected from asynchronous
		///	exceptions like <see cref="ThreadAbortException"/>. If another thread
		///	attempts to perform an abort while <paramref name="action"/> is running
		///	it will be blocked until <paramref name="action"/> completes and the
		///	lock is released.</para>
		/// </remarks>
		public bool TryReadUpgradable(int millisecondsTimeout, Action action)
		{
			if (action == null) throw new ArgumentNullException("action");

			return _tryRunLockedActionInterruptible<object, int>(
				_lock,
				null,
				millisecondsTimeout,
				(l, t) => l.TryEnterUpgradeableReadLock(t),
				l => l.ExitUpgradeableReadLock(),
				o => action());
		}

		/// <summary>
		/// 	<para>Runs the specified action safely inside a read context.
		/// 	If the invoking thread is blocked for longer than the specified
		/// 	timeout <paramref name="action"/> will not be run and 
		/// 	<see langword="false"/> will be returned. A write action
		/// 	may also be performed in this context unlike with a normal read.</para>
		/// </summary>
		/// <typeparam name="T">
		///	<para>The type of state object to pass to <paramref name="action"/>.</para>
		/// </typeparam>
		/// <param name="state">
		///	<para>A state object to pass to <paramref name="action"/>.</para>
		/// </param>
		/// <param name="millisecondsTimeout">
		///	<para>The number of milliseconds to wait, or -1
		///	<see cerf="Timeout.Infinite"/> to wait indefinitely.</para>
		/// </param>
		/// <param name="action">
		///	<para>The action to run.</para>
		/// </param>
		/// <returns>
		///	<para><see langword="true"/> if the read ran successfully;
		///	<see langword="false"/> if the read timed out.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para>Thrown when <paramref name="action"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <remarks>
		///	<para>Only one upgradable reader may enter concurrently with other
		///	upgradable readers. However other non-upgradable readers are still
		///	permitted to enter concurrently with a single upgradable reader.</para>
		///	<para><paramref name="action"/> is protected from asynchronous
		///	exceptions like <see cref="ThreadAbortException"/>. If another thread
		///	attempts to perform an abort while <paramref name="action"/> is running
		///	it will be blocked until <paramref name="action"/> completes and the
		///	lock is released.</para>
		/// </remarks>
		public bool TryReadUpgradable<T>(T state, int millisecondsTimeout, Action<T> action)
		{
			if (action == null) throw new ArgumentNullException("action");

			return _tryRunLockedActionInterruptible<T, int>(
				_lock,
				state,
				millisecondsTimeout,
				(l, t) => l.TryEnterUpgradeableReadLock(t),
				l => l.ExitUpgradeableReadLock(),
				action);
		}

		/// <summary>
		/// 	<para>Runs the specified action safely inside a read context.
		/// 	If the invoking thread is blocked for longer than the specified
		/// 	timeout <paramref name="action"/> will not be run and 
		/// 	<see langword="false"/> will be returned. A write action
		/// 	may also be performed in this context unlike with a normal read.</para>
		/// </summary>
		/// <param name="timeout">
		///	<para>The time to wait, or -1 milliseconds
		///	<see cerf="Timeout.Infinite"/> to wait indefinitely.</para>
		/// </param>
		/// <param name="action">
		///	<para>The action to run.</para>
		/// </param>
		/// <returns>
		///	<para><see langword="true"/> if the read ran successfully;
		///	<see langword="false"/> if the read timed out.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para>Thrown when <paramref name="action"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <remarks>
		///	<para>Only one upgradable reader may enter concurrently with other
		///	upgradable readers. However other non-upgradable readers are still
		///	permitted to enter concurrently with a single upgradable reader.</para>
		///	<para><paramref name="action"/> is protected from asynchronous
		///	exceptions like <see cref="ThreadAbortException"/>. If another thread
		///	attempts to perform an abort while <paramref name="action"/> is running
		///	it will be blocked until <paramref name="action"/> completes and the
		///	lock is released.</para>
		/// </remarks>
		public bool TryReadUpgradable(TimeSpan timeout, Action action)
		{
			if (action == null) throw new ArgumentNullException("action");

			return _tryRunLockedActionInterruptible<object, TimeSpan>(
				_lock,
				null,
				timeout,
				(l, t) => l.TryEnterUpgradeableReadLock(t),
				l => l.ExitUpgradeableReadLock(),
				o => action());
		}

		/// <summary>
		/// 	<para>Runs the specified action safely inside a read context.
		/// 	If the invoking thread is blocked for longer than the specified
		/// 	timeout <paramref name="action"/> will not be run and 
		/// 	<see langword="false"/> will be returned. A write action
		/// 	may also be performed in this context unlike with a normal read.</para>
		/// </summary>
		/// <typeparam name="T">
		///	<para>The type of state object to pass to <paramref name="action"/>.</para>
		/// </typeparam>
		/// <param name="state">
		///	<para>A state object to pass to <paramref name="action"/>.</para>
		/// </param>
		/// <param name="timeout">
		///	<para>The time to wait, or -1 milliseconds
		///	<see cerf="Timeout.Infinite"/> to wait indefinitely.</para>
		/// </param>
		/// <param name="action">
		///	<para>The action to run.</para>
		/// </param>
		/// <returns>
		///	<para><see langword="true"/> if the read ran successfully;
		///	<see langword="false"/> if the read timed out.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para>Thrown when <paramref name="action"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <remarks>
		///	<para>Only one upgradable reader may enter concurrently with other
		///	upgradable readers. However other non-upgradable readers are still
		///	permitted to enter concurrently with a single upgradable reader.</para>
		///	<para><paramref name="action"/> is protected from asynchronous
		///	exceptions like <see cref="ThreadAbortException"/>. If another thread
		///	attempts to perform an abort while <paramref name="action"/> is running
		///	it will be blocked until <paramref name="action"/> completes and the
		///	lock is released.</para>
		/// </remarks>
		public bool TryReadUpgradable<T>(T state, TimeSpan timeout, Action<T> action)
		{
			if (action == null) throw new ArgumentNullException("action");

			return _tryRunLockedActionInterruptible<T, TimeSpan>(
				_lock,
				state,
				timeout,
				(l, t) => l.TryEnterUpgradeableReadLock(t),
				l => l.ExitUpgradeableReadLock(),
				action);
		}

		/// <summary>
		/// 	<para>Runs the specified action safely inside a write context.
		/// 	Only one writer can run at a time. No readers may run while
		/// 	writes are being performed.</para>
		/// </summary>
		/// <param name="action">
		///	<para>The action to run.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		///	<para>Thrown when <paramref name="action"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <remarks>
		///	<para><paramref name="action"/> is protected from asynchronous
		///	exceptions like <see cref="ThreadAbortException"/>. If another thread
		///	attempts to perform an abort while <paramref name="action"/> is running
		///	it will be blocked until <paramref name="action"/> completes and the
		///	lock is released.</para>
		/// </remarks>
		public void Write(Action action)
		{
			if (action == null) throw new ArgumentNullException("action");

			_runLockedAction<object>(
				_lock,
				null,
				l => l.EnterWriteLock(),
				l => l.ExitWriteLock(),
				o => action());
		}

		/// <summary>
		/// 	<para>Runs the specified action safely inside a write context.
		/// 	Only one writer can run at a time. No readers may run while
		/// 	writes are being performed.</para>
		/// </summary>
		/// <typeparam name="T">
		///	<para>The type of state object to pass to <paramref name="action"/>.</para>
		/// </typeparam>
		/// <param name="state">
		///	<para>A state object to pass to <paramref name="action"/>.</para>
		/// </param>
		/// <param name="action">
		///	<para>The action to run.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		///	<para>Thrown when <paramref name="action"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <remarks>
		///	<para><paramref name="action"/> is protected from asynchronous
		///	exceptions like <see cref="ThreadAbortException"/>. If another thread
		///	attempts to perform an abort while <paramref name="action"/> is running
		///	it will be blocked until <paramref name="action"/> completes and the
		///	lock is released.</para>
		/// </remarks>
		public void Write<T>(T state, Action<T> action)
		{
			if (action == null) throw new ArgumentNullException("action");

			_runLockedAction<T>(
				_lock,
				state,
				l => l.EnterWriteLock(),
				l => l.ExitWriteLock(),
				action);
		}

		/// <summary>
		/// 	<para>Runs the specified action safely inside a write context.
		/// 	Only one writer can run at a time. No readers may run while
		/// 	writes are being performed.</para>
		/// 	<para>If the invoking thread is blocked for longer than the specified
		/// 	timeout <paramref name="action"/> will not be run and 
		/// 	<see langword="false"/> will be returned.</para>
		/// </summary>
		/// <param name="millisecondsTimeout">
		///	<para>The number of milliseconds to wait, or -1
		///	<see cerf="Timeout.Infinite"/> to wait indefinitely.</para>
		/// </param>
		/// <param name="action">
		///	<para>The action to run.</para>
		/// </param>
		/// <returns>
		///	<para><see langword="true"/> if the write ran successfully;
		///	<see langword="false"/> if the write timed out.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para>Thrown when <paramref name="action"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <remarks>
		///	<para><paramref name="action"/> is protected from asynchronous
		///	exceptions like <see cref="ThreadAbortException"/>. If another thread
		///	attempts to perform an abort while <paramref name="action"/> is running
		///	it will be blocked until <paramref name="action"/> completes and the
		///	lock is released.</para>
		/// </remarks>
		public bool TryWrite(int millisecondsTimeout, Action action)
		{
			if (action == null) throw new ArgumentNullException("action");

			return _tryRunLockedAction<object, int>(
				_lock,
				null,
				millisecondsTimeout,
				(l, t) => l.TryEnterWriteLock(t),
				l => l.ExitWriteLock(),
				o => action());
		}

		/// <summary>
		/// 	<para>Runs the specified action safely inside a write context.
		/// 	Only one writer can run at a time. No readers may run while
		/// 	writes are being performed.</para>
		/// 	<para>If the invoking thread is blocked for longer than the specified
		/// 	timeout <paramref name="action"/> will not be run and 
		/// 	<see langword="false"/> will be returned.</para>
		/// </summary>
		/// <typeparam name="T">
		///	<para>The type of state object to pass to <paramref name="action"/>.</para>
		/// </typeparam>
		/// <param name="state">
		///	<para>A state object to pass to <paramref name="action"/>.</para>
		/// </param>
		/// <param name="millisecondsTimeout">
		///	<para>The number of milliseconds to wait, or -1
		///	<see cerf="Timeout.Infinite"/> to wait indefinitely.</para>
		/// </param>
		/// <param name="action">
		///	<para>The action to run.</para>
		/// </param>
		/// <returns>
		///	<para><see langword="true"/> if the write ran successfully;
		///	<see langword="false"/> if the write timed out.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para>Thrown when <paramref name="action"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <remarks>
		///	<para><paramref name="action"/> is protected from asynchronous
		///	exceptions like <see cref="ThreadAbortException"/>. If another thread
		///	attempts to perform an abort while <paramref name="action"/> is running
		///	it will be blocked until <paramref name="action"/> completes and the
		///	lock is released.</para>
		/// </remarks>
		public bool TryWrite<T>(T state, int millisecondsTimeout, Action<T> action)
		{
			if (action == null) throw new ArgumentNullException("action");

			return _tryRunLockedAction<T, int>(
				_lock,
				state,
				millisecondsTimeout,
				(l, t) => l.TryEnterWriteLock(t),
				l => l.ExitWriteLock(),
				action);
		}

		/// <summary>
		/// 	<para>Runs the specified action safely inside a write context.
		/// 	Only one writer can run at a time. No readers may run while
		/// 	writes are being performed.</para>
		/// 	<para>If the invoking thread is blocked for longer than the specified
		/// 	timeout <paramref name="action"/> will not be run and 
		/// 	<see langword="false"/> will be returned.</para>
		/// </summary>
		/// <param name="timeout">
		///	<para>The time to wait, or -1 milliseconds
		///	<see cerf="Timeout.Infinite"/> to wait indefinitely.</para>
		/// </param>
		/// <param name="action">
		///	<para>The action to run.</para>
		/// </param>
		/// <returns>
		///	<para><see langword="true"/> if the write ran successfully;
		///	<see langword="false"/> if the write timed out.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para>Thrown when <paramref name="action"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <remarks>
		///	<para><paramref name="action"/> is protected from asynchronous
		///	exceptions like <see cref="ThreadAbortException"/>. If another thread
		///	attempts to perform an abort while <paramref name="action"/> is running
		///	it will be blocked until <paramref name="action"/> completes and the
		///	lock is released.</para>
		/// </remarks>
		public bool TryWrite(TimeSpan timeout, Action action)
		{
			if (action == null) throw new ArgumentNullException("action");

			return _tryRunLockedAction<object, TimeSpan>(
				_lock,
				null,
				timeout,
				(l, t) => l.TryEnterWriteLock(t),
				l => l.ExitWriteLock(),
				o => action());
		}

		/// <summary>
		/// 	<para>Runs the specified action safely inside a write context.
		/// 	Only one writer can run at a time. No readers may run while
		/// 	writes are being performed.</para>
		/// 	<para>If the invoking thread is blocked for longer than the specified
		/// 	timeout <paramref name="action"/> will not be run and 
		/// 	<see langword="false"/> will be returned.</para>
		/// </summary>
		/// <typeparam name="T">
		///	<para>The type of state object to pass to <paramref name="action"/>.</para>
		/// </typeparam>
		/// <param name="state">
		///	<para>A state object to pass to <paramref name="action"/>.</para>
		/// </param>
		/// <param name="timeout">
		///	<para>The time to wait, or -1 milliseconds
		///	<see cerf="Timeout.Infinite"/> to wait indefinitely.</para>
		/// </param>
		/// <param name="action">
		///	<para>The action to run.</para>
		/// </param>
		/// <returns>
		///	<para><see langword="true"/> if the write ran successfully;
		///	<see langword="false"/> if the write timed out.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		///	<para>Thrown when <paramref name="action"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <remarks>
		///	<para><paramref name="action"/> is protected from asynchronous
		///	exceptions like <see cref="ThreadAbortException"/>. If another thread
		///	attempts to perform an abort while <paramref name="action"/> is running
		///	it will be blocked until <paramref name="action"/> completes and the
		///	lock is released.</para>
		/// </remarks>
		public bool TryWrite<T>(T state, TimeSpan timeout, Action<T> action)
		{
			if (action == null) throw new ArgumentNullException("action");

			return _tryRunLockedAction<T, TimeSpan>(
				_lock,
				state,
				timeout,
				(l, t) => l.TryEnterWriteLock(t),
				l => l.ExitWriteLock(),
				action);
		}

		private static void _runLockedAction<TState>(
			ReaderWriterLockSlim lockObject,
			TState state,
			Action<ReaderWriterLockSlim> enterLock,
			Action<ReaderWriterLockSlim> exitLock,
			Action<TState> action)
		{
			try { }
			finally
			{
				enterLock(lockObject);
				try
				{
					action(state);
				}
				finally
				{
					exitLock(lockObject);
				}
			}
		}

		private static void _runLockedActionInterruptible<TState>(
			ReaderWriterLockSlim lockObject,
			TState state,
			Action<ReaderWriterLockSlim> enterLock,
			Action<ReaderWriterLockSlim> exitLock,
			Action<TState> action)
		{
			bool entered = false;
			try
			{
				try
				{
				}
				finally
				{
					enterLock(lockObject);
					entered = true;
				}
				action(state);
			}
			finally
			{
				if (entered)
					exitLock(lockObject);
			}
		}

		private delegate bool TryAction<T1, T2>(T1 obj1, T2 obj2);

		private static bool _tryRunLockedAction<TState, TTimeout>(
			ReaderWriterLockSlim lockObject,
			TState state,
			TTimeout timeout,
			TryAction<ReaderWriterLockSlim, TTimeout> tryEnterLock,
			Action<ReaderWriterLockSlim> exitLock,
			Action<TState> action)
		{
			bool entered;
			try { }
			finally
			{
				entered = tryEnterLock(lockObject, timeout);
				if (entered)
				{
					try
					{
						action(state);
					}
					finally
					{
						exitLock(lockObject);
					}
				}
			}
			return entered;
		}

		private static bool _tryRunLockedActionInterruptible<TState, TTimeout>(
			ReaderWriterLockSlim lockObject,
			TState state,
			TTimeout timeout,
			TryAction<ReaderWriterLockSlim, TTimeout> tryEnterLock,
			Action<ReaderWriterLockSlim> exitLock,
			Action<TState> action)
		{
			bool entered = false;
			try
			{
				try
				{
				}
				finally
				{
					entered = tryEnterLock(lockObject, timeout);
				}
				if (entered)
					action(state);
			}
			finally
			{
				if (entered)
					exitLock(lockObject);
			}
			return entered;
		}

		#region IDisposable returning methods
		/// <summary>
		/// Creates a disposable scope encapsulating a read context.
		/// </summary>
		/// <returns>An <see cref="IDisposable"/> object that exits the
		/// read context when <see cref="IDisposable.Dispose"/> is called.
		/// Is null if the current thread was aborted during the lock
		/// acquisition.</returns>
		public IDisposable ReadScope()
		{
			bool succeeded;
			try
			{
			} finally
			{
				_lock.EnterReadLock();
				succeeded = (Thread.CurrentThread.ThreadState & ThreadState.AbortRequested)
					!= ThreadState.AbortRequested;
				if (!succeeded)
				{
					_lock.ExitReadLock();
				}
			}
			return succeeded ? new ReadLockCleanup(_lock) : null;
		}

		/// <summary>
		/// Creates a disposable scope encapsulating a read context within a
		/// user supplied timeout to acquire the lock.
		/// </summary>
		/// <param name="timeout">The allowable <see cref="TimeSpan"/> for the
		/// lock to be acquired.</param>
		/// <returns>An <see cref="IDisposable"/> object that exits the
		/// read context when <see cref="IDisposable.Dispose"/> is called.
		/// Is null if lock acquisition timed out or the current thread
		/// was aborted during the lock acquisition.</returns>
		public IDisposable TryReadScope(TimeSpan timeout)
		{
			bool succeeded;
			try
			{
			}
			finally
			{
				succeeded = _lock.TryEnterReadLock(timeout);
				if (succeeded)
				{
					succeeded = (Thread.CurrentThread.ThreadState & ThreadState.AbortRequested)
						!= ThreadState.AbortRequested;
					if (!succeeded)
					{
						_lock.ExitReadLock();
					}
				}
			}
			return succeeded ? new ReadLockCleanup(_lock) : null;
		}


		/// <summary>
		/// Creates a disposable scope encapsulating a read context within a
		/// user supplied timeout to acquire the lock.
		/// </summary>
		/// <param name="timeout">The allowable <see cref="Int32"/> milliseconds
		/// for the lock to be acquired.</param>
		/// <returns>An <see cref="IDisposable"/> object that exits the
		/// read context when <see cref="IDisposable.Dispose"/> is called.
		/// Is null if lock acquisition timed out or the current thread
		/// was aborted during the lock acquisition.</returns>
		public IDisposable TryReadScope(int timeout)
		{
			return TryReadScope(TimeSpan.FromMilliseconds(timeout));
		}

		/// <summary>
		/// Creates a disposable scope encapsulating an upgradable read context.
		/// </summary>
		/// <returns>An <see cref="IDisposable"/> object that exits the
		/// read context when <see cref="IDisposable.Dispose"/> is called.
		/// Is null if the current thread was aborted during the lock
		/// acquisition.</returns>
		public IDisposable ReadUpgradableScope()
		{
			bool succeeded;
			try
			{
			}
			finally
			{
				_lock.EnterUpgradeableReadLock();
				succeeded = (Thread.CurrentThread.ThreadState & ThreadState.AbortRequested)
					!= ThreadState.AbortRequested;
				if (!succeeded)
				{
					_lock.ExitUpgradeableReadLock();
				}
			}
			return succeeded ? new ReadUpgradableLockCleanup(_lock) : null;
		}

		/// <summary>
		/// Creates a disposable scope encapsulating an upgradable read
		/// context within a user supplied timeout to acquire the lock.
		/// </summary>
		/// <param name="timeout">The allowable <see cref="TimeSpan"/> for the
		/// lock to be acquired.</param>
		/// <returns>An <see cref="IDisposable"/> object that exits the
		/// read context when <see cref="IDisposable.Dispose"/> is called.
		/// Is null if lock acquisition timed out or the current thread
		/// was aborted during the lock acquisition.</returns>
		public IDisposable TryReadUpgradableScope(TimeSpan timeout)
		{
			bool succeeded;
			try
			{
			}
			finally
			{
				succeeded = _lock.TryEnterUpgradeableReadLock(timeout);
				if (succeeded)
				{
					succeeded = (Thread.CurrentThread.ThreadState & ThreadState.AbortRequested)
						!= ThreadState.AbortRequested;
					if (!succeeded)
					{
						_lock.ExitUpgradeableReadLock();
					}
				}
			}
			return succeeded ? new ReadUpgradableLockCleanup(_lock) : null;
		}

		/// <summary>
		/// Creates a disposable scope encapsulating an upgradable read
		/// context within a user supplied timeout to acquire the lock.
		/// </summary>
		/// <param name="timeout">The allowable <see cref="Int32"/> milliseconds
		/// for the lock to be acquired.</param>
		/// <returns>An <see cref="IDisposable"/> object that exits the
		/// read context when <see cref="IDisposable.Dispose"/> is called.
		/// Is null if lock acquisition timed out or the current thread
		/// was aborted during the lock acquisition.</returns>
		public IDisposable TryReadUpgradableScope(int timeout)
		{
			return TryReadUpgradableScope(TimeSpan.FromMilliseconds(timeout));
		}

		/// <summary>
		/// Creates a disposable scope encapsulating a write context.
		/// </summary>
		/// <returns>An <see cref="IDisposable"/> object that exits the
		/// read context when <see cref="IDisposable.Dispose"/> is called.
		/// Is null if the current thread was aborted during the lock
		/// acquisition.</returns>
		/// <remarks>Unlike <see cref="Write"/>, there is no interruption
		/// protection of the code branch enclosed in the <see langword="using"/>
		/// that calls this.</remarks>
		public IDisposable WriteScope()
		{
			bool succeeded;
			try
			{
			}
			finally
			{
				_lock.EnterWriteLock();
				succeeded = (Thread.CurrentThread.ThreadState & ThreadState.AbortRequested)
					!= ThreadState.AbortRequested;
				if (!succeeded)
				{
					_lock.ExitWriteLock();
				}
			}
			return succeeded ? new WriteLockCleanup(_lock) : null;
		}

		/// <summary>
		/// Creates a disposable scope encapsulating a write context within a
		/// user supplied timeout to acquire the lock.
		/// </summary>
		/// <param name="timeout">The allowable <see cref="TimeSpan"/> for the
		/// lock to be acquired.</param>
		/// <returns>An <see cref="IDisposable"/> object that exits the
		/// read context when <see cref="IDisposable.Dispose"/> is called.
		/// Is null if lock acquisition timed out or the current thread
		/// was aborted during the lock acquisition.</returns>
		/// <remarks>Unlike <see cref="Write"/>, there is no interruption
		/// protection of the code branch enclosed in the <see langword="using"/>
		/// that calls this.</remarks>
		public IDisposable TryWriteScope(TimeSpan timeout)
		{
			bool succeeded;
			try
			{
			}
			finally
			{
				succeeded = _lock.TryEnterWriteLock(timeout);
				if (succeeded)
				{
					succeeded = (Thread.CurrentThread.ThreadState & ThreadState.AbortRequested)
						!= ThreadState.AbortRequested;
					if (!succeeded)
					{
						_lock.ExitWriteLock();
					}
				}
			}
			return succeeded ? new WriteLockCleanup(_lock) : null;
		}

		/// <summary>
		/// Creates a disposable scope encapsulating a write context within a
		/// user supplied timeout to acquire the lock.
		/// </summary>
		/// <param name="timeout">The allowable <see cref="Int32"/> milliseconds
		/// for the lock to be acquired.</param>
		/// <returns>An <see cref="IDisposable"/> object that exits the
		/// read context when <see cref="IDisposable.Dispose"/> is called.
		/// Is null if lock acquisition timed out or the current thread
		/// was aborted during the lock acquisition.</returns>
		/// <remarks>Unlike <see cref="Write"/>, there is no interruption
		/// protection of the code branch enclosed in the <see langword="using"/>
		/// that calls this.</remarks>
		public IDisposable TryWriteScope(int timeout)
		{
			return TryWriteScope(TimeSpan.FromMilliseconds(timeout));
		}
		#endregion
	}
}
