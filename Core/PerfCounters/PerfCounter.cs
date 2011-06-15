using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using MySpace.Logging;

namespace MySpace.PerfCounters
{
	/// <summary>
	///		<para>The base class of all performance counters.  This and its descendent class
	///		are designed to encapsulate the logic to update, install, and uninstall a single
	///		performance counter.</para>
	/// </summary>
	/// <remarks>
	///		<para>This class must be instantiated as a field of a descendent of
	///		<see cref="PerfCounterCategory{Implementation}"/>.  It cannot be instantiated in any other way.</para>
	/// </remarks>
	public abstract class PerfCounter
	{
		private readonly object _syncRoot = new object();
		private readonly string _description;
		private readonly string _name;
		private readonly PerformanceCounterType _counterType;
		private readonly PerformanceCounterType? _baseCounterType;
		private readonly bool _useCalculatedValue;
		private PerformanceCounter _innerCounter;
		private PerformanceCounter _baseCounter;
		private bool _innerCounterAttempedCreation;
		private bool _baseCounterAttemptedCreation;
		private static readonly LogWrapper Log = new LogWrapper();

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="PerfCounter"/> class.</para>
		/// </summary>
		/// <param name="name">
		/// 	<para>The name of the performance counter that this instance encapsulates;
		/// 	never <see langword="null"/>.</para>
		/// </param>
		/// <param name="description">
		/// 	<para>The description of the Windows performance counter that this instance
		/// 	encapsulates; could be <see langword="null"/> or empty.</para>
		/// </param>
		/// <param name="counterType">
		/// 	<para>The type of the Windows performance counter that this instance encapsulates.</para>
		/// </param>
		/// <param name="baseCounterType">
		/// 	<para>The type of the base counter, if required, that this instance encapsulates;
		/// 	<see langword="null"/> if no base counter is required.</para>
		/// </param>
		/// <param name="useCalculatedValue">
		/// 	<para>
		/// 		<see langword="true"/> if the calculated value is used when the
		/// 	value of this counter is read from; otherwise, <see langword="false"/>.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="name"/> is <see langword="null"/>.</para>
		/// </exception>
		protected PerfCounter(
			string name,
			string description,
			PerformanceCounterType counterType,
			PerformanceCounterType? baseCounterType,
			bool useCalculatedValue)
		{
			if (name == null) throw new ArgumentNullException("name");

			_name = name;
			_description = description ?? string.Empty;

			this._counterType = counterType;
			this._baseCounterType = baseCounterType;
			this._useCalculatedValue = useCalculatedValue;
		}

		/// <summary>
		/// 	<para>Initializes this instance with the specified category and instance names.</para>
		/// </summary>
		/// <param name="categoryName">The name of the category that this instance belongs to.</param>
		/// <param name="instanceName">The instance name of this counter.</param>
		internal void Initialize(string categoryName, string instanceName)
		{
			if (string.IsNullOrEmpty(categoryName)) throw new ArgumentNullException("categoryName");
			if (string.IsNullOrEmpty(instanceName)) throw new ArgumentNullException("instanceName");

			lock (_syncRoot)
			{
				if (IsInitialized)
				{
					throw new InvalidOperationException(String.Format("PerfCounter instance {0} in {1} has already been initialized", instanceName, categoryName));
				}

				CategoryName = categoryName;
				InstanceName = instanceName;
			}
			OnInitialize();
		}

		/// <summary>
		/// Called when the instance is initialized via <see cref="Initialize"/>.
		/// </summary>
		protected virtual void OnInitialize()
		{
		}

		/// <summary>
		/// Gets a value indicating whether this instance is available.
		/// </summary>
		/// <value>
		/// 	<see langword="true"/> if this instance is available; otherwise, <see langword="false"/>.
		/// </value>
		public bool IsAvailable
		{
			get { return _innerCounter != null && (!_baseCounterType.HasValue || _baseCounter != null); }
		}

		/// <summary>
		/// 	<para>Gets a value indicating whether this instance is initialized.</para>
		/// </summary>
		/// <value>
		/// 	<para><see langword="true"/> if this instance is initialized; otherwise, <see langword="false"/>.</para>
		/// </value>
		internal bool IsInitialized { get { return CategoryName != null; } }

		///	<summary>
		///		<para>Gets the name of the category to which this counter belongs.</para>
		/// </summary>
		///	<value>
		///		<para>A <see cref="string"/> providing the category name; 
		///		<see langword="null"/> if the counter has not been initialized.</para>
		///	</value>
		public string CategoryName { get; private set; }

		/// <summary>
		///	<para>Gets the instance name of the underlying <see cref="PerformanceCounter"/>.</para>
		/// </summary>
		/// <value>The instance name of the underlying <see cref="PerformanceCounter"/>;
		/// never <see langword="null"/> if the counter has not been initialized.</value>
		public string InstanceName { get; private set; }

		///	<summary>
		///		<para>Gets the name of this performance counter within
		///		its category.</para>
		/// </summary>
		///	<value>
		///		<para>A <see cref="string"/> providing the name of this
		///		counter; never <see langword="null"/>.</para>
		///	</value>
		public string Name
		{
			[DebuggerStepThrough]
			get { return _name; }
		}

		///	<summary>
		///		<para>Gets the detailed description of this performance counter.</para>
		/// </summary>
		///	<value>
		///		<para>A <see cref="string"/> providing the detailed description
		///		of this performance counter; never <see langword="null"/>.</para>
		///	</value>
		public string Description
		{
			[DebuggerStepThrough]
			get { return _description; }
		}

		/// <summary>
		/// 	<para>Gets the inner counter.</para>
		/// </summary>
		/// <value>
		/// 	<para>The inner counter. <see langword="null"/> if the counter cannot be created.</para>
		/// </value>
		internal PerformanceCounter InnerCounter
		{
			get
			{
				if (_innerCounter != null) return _innerCounter;
				lock (_syncRoot)
				{
					if (_innerCounter != null) return _innerCounter;
					if (_innerCounterAttempedCreation) return null;

					PerformanceCounter counter = TryCreateCounter(Name);
					if (counter != null)
					{
						Thread.MemoryBarrier();
						_innerCounter = counter;
					}
					_innerCounterAttempedCreation = true;
					return counter;
				}
			}
		}

		/// <summary>
		/// 	<para>Gets the base counter.</para>
		/// </summary>
		/// <value>
		/// 	<para>The base counter.</para>
		/// </value>
		internal PerformanceCounter BaseCounter
		{
			get
			{
				if (!_baseCounterType.HasValue) return null;
				if (_baseCounter != null) return _baseCounter;

				lock (_syncRoot)
				{
					if (_baseCounter != null) return _baseCounter;
					if (_baseCounterAttemptedCreation) return null;

					PerformanceCounter counter = TryCreateCounter(BaseCounterName);
					if (counter != null)
					{
						Thread.MemoryBarrier();
						_baseCounter = counter;
					}
					_baseCounterAttemptedCreation = true;
					return _baseCounter;
				}
			}
		}

		private PerformanceCounter TryCreateCounter(string name)
		{
			if (!IsInitialized) throw new InvalidOperationException("This PerfCounter instance has not been initialized");
            try 
            {
                if (!PerformanceCounterCategory.Exists(CategoryName))
                {
                    // the category doesn't exist.  This means that the counters were never installed, or previous errors have occurred.
                    return null;
                }
                if (!PerformanceCounterCategory.CounterExists(name, CategoryName))
                {
                    // the category exists, but the counter does not.
                    return null;
                }

                return new PerformanceCounter(CategoryName, name, InstanceName, false);
            }
            catch (InvalidOperationException ioex)
            {
                // Counter not installed.
                Log.DebugFormat(
                    "PerfCounter {0} of {1} in {2} could not be initialized, and will not be available. {3}", name,
                    InstanceName, CategoryName, ioex);
            }
            catch(UnauthorizedAccessException uae)
            {
                // if the account doesn't have privileges to use counters, this exception is thrown.  The account has to be in the 
                // Perfmormance Monitor Users group or be admin.
                Log.DebugFormat("The process does not have the privilege to initialze PerfCounter {0} of {1} in {2}. {3}", name, InstanceName, CategoryName, uae);
            }
		    return null;
		}

		/// <summary>
		/// Removes the specified instance.
		/// </summary>
		/// <returns>
		///	<para><see langword="true"/> if the instance was found and removed.
		///	<see langword="false"/> otherwise.</para>
		/// </returns>
		public bool RemoveInstance()
		{
			PerformanceCounter target = InnerCounter;

			if (target != null)
			{
				target.RemoveInstance();
			}
			else
			{
				return false;
			}

			if (_baseCounterType.HasValue)
			{
				target = BaseCounter;

				if (target != null)
				{
					target.RemoveInstance();
				}
				else
				{
					return false;
				}
			}

			return true;
		}

		private string BaseCounterName
		{
			[DebuggerStepThrough]
			get
			{
				return this._name + "_base";
			}
		}

		/// <summary>
		/// 	<para>Used by descendent classes to increment this counter
		///		by the specified amount.</para>
		/// </summary>
		/// <param name="incrementBy">
		/// 	<para>The amount to increment the counter by.</para>
		/// </param>
		/// <param name="incrementBaseBy">
		/// 	<para>The amount to increment the base counter by.
		///		Ignored if this counter has no base counter.</para>
		/// </param>
		protected void BaseIncrement(long? incrementBy, long? incrementBaseBy)
		{
			bool incrementTop
				= incrementBy.HasValue
				&& incrementBy.Value != 0L;
			bool incrementBase
				= incrementBaseBy.HasValue
				&& incrementBaseBy.Value != 0L
				&& _baseCounterType.HasValue;

			PerformanceCounter innerCounter = null;
			PerformanceCounter baseCounter = null;

			if (incrementTop) innerCounter = InnerCounter;
			if (incrementBase) baseCounter = BaseCounter;

			if (incrementTop && innerCounter == null) return;
			if (incrementBase && baseCounter == null) return;

			if (innerCounter != null) innerCounter.IncrementBy(incrementBy.Value);
			if (baseCounter != null) baseCounter.IncrementBy(incrementBaseBy.Value);
		}

		/// <summary>
		/// 	<para>Used by descendent classes to set the raw value of this 
		///		counter to the specified number.</para>
		/// </summary>
		/// <param name="rawValue">
		/// 	<para>The new raw value of the counter.</para>
		/// </param>
		/// <param name="baseRawValue">
		/// 	<para>The new raw value of the base counter.
		///		Ignored if this counter has no base counter.</para>
		/// </param>
		protected void BaseSetRawValue(long? rawValue, long? baseRawValue)
		{
			bool incrementTop = rawValue.HasValue;
			bool incrementBase = baseRawValue.HasValue && _baseCounterType.HasValue;

			PerformanceCounter innerCounter = null;
			PerformanceCounter baseCounter = null;

			if (incrementTop) innerCounter = InnerCounter;
			if (incrementBase) baseCounter = BaseCounter;

			if (incrementTop && innerCounter == null) return;
			if (incrementBase && baseCounter == null) return;

			if (innerCounter != null) innerCounter.RawValue = rawValue.Value;
			if (baseCounter != null) baseCounter.RawValue = baseRawValue.Value;
		}

		/// <summary>
		/// 	<para>Gets the current value of this performance counter.</para>
		/// </summary>
		/// <returns>
		/// 	<para>A <see cref="Single"/> value providing the value of the
		/// 	specified instance; always 0 if the couter has not been installed.</para>
		/// </returns>
		public virtual float GetValue()
		{
			PerformanceCounter counter = InnerCounter;
			PerformanceCounter baseCounter = BaseCounter;
			if (counter == null || _baseCounterType.HasValue && baseCounter == null)
			{
				return 0;
			}

			if (this._useCalculatedValue)
			{
				if (_baseCounterType.HasValue)
				{
					float topVal = counter.NextValue();
					float baseVal = baseCounter.NextValue();
					if (baseVal == 0) return 0;
					return topVal / baseVal;
				}
				return counter.NextValue();
			}

			if (_baseCounterType.HasValue)
			{
				float topVal = counter.RawValue;
				float baseVal = baseCounter.RawValue;
				if (baseVal == 0) return 0;
				return topVal / baseVal;
			}
			return counter.RawValue;
		}

		/// <summary>
		/// 	<para>Gets all <see cref="CounterCreationData"/> instances necessary to install
		///		this performance counter.</para>
		/// </summary>
		/// <returns>
		///	<para>All <see cref="CounterCreationData"/> instances necessary to install
		///		this performance counter.</para>
		/// </returns>
		internal IEnumerable<CounterCreationData> GetCounterCreationData()
		{
			yield return new CounterCreationData(_name, _description, _counterType);

			if (_baseCounterType.HasValue)
			{
				yield return new CounterCreationData(BaseCounterName, String.Empty, _baseCounterType.Value);
			}
		}
	}
}