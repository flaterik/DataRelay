using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using MySpace.Logging;

namespace MySpace.PerfCounters
{
	/// <summary>
	///		<para>Base class whose implementations represent one performance counter
	///		category each.  Centralizes the installation, uninstallation, and incrementing
	///		of the performance counters encapsulated by <see cref="PerfCounter"/> 
	///		objects.</para>
	/// </summary>
	/// <typeparam name="Implementation">
	///	<para>The type that derives from this one.</para>
	/// </typeparam>
	/// <remarks>
	///		<para>This class forms the centerpiece of the MySpace performance counters wrapper
	///		framework.  It offers the following advantages compared to using the .NET performance
	///		counter framework directly:</para>
	///		<list type="bullet">
	///			<item>It automates and simplifies the installation and uninstallation of counters.</item>
	///			<item>It eliminates the need to deal with base counters.</item>		
	///			<item>It painlessly supports multi-instance counters.</item>
	///			<item>It allows the creation of performance counters with custom logic.</item>
	///		</list>
	/// 
	///		<para>To declare a new performance counter category and to manage performance counters
	///		within it, the developer needs to first derive a new class from the <see cref="PerfCounterCategory{Implementation}"/>
	///		base class.  The type parameter passed to the base class should be the type of the descendent class
	///		itself.  The descendant category class should have a private constructor that accepts a string
	///		instance name and invokes the base constructor with the name of the category, as well as the
	///		instance name that was passed to the derived constructor:</para>
	/// 
	///		<code>
	///			public sealed class FooCounters: PerfCounterCategory&lt;FooCounters&gt;
	///			{
	///				// ...
	/// 
	///				private class FooCounters(string instanceName)
	///					: base("Foo Category", instanceName)
	///				{
	///				}
	///				
	///				// ...
	///			}
	///		</code>
	/// 
	///		<para>The actual counters are represented as <see cref="PerfCounter"/> objects.  Unlike
	///		the .NET counter framework, in which a single <see cref="PerformanceCounter"/> object encapsulates
	///		counters of all types, here, each counter type is represented by its own class, such as
	///		<see cref="NumberOfItems32Counter"/>, <see cref="RateOfCountsPerSecond32Counter"/>, and
	///		<see cref="AverageCount64Counter"/>.  Creating average counters simply requires instantiating
	///		the correct subclass, and there is no need to deal with base counters.</para>
	/// 
	///		<para>To add counters to a category, simply instantiate the counters as read-only fields under 
	///		the category class we just created:</para>
	/// 
	///		<code>
	///			public sealed class FooCounters : PerfCounterCategory&lt;FooCounters&gt;
	///			{
	///				public readonly NumberOfItems32Counter TotalProcessed = new NumberOfItems32Counter("Total Transactions Processed", "The total number of transactions that have been processed");
	///				public readonly RateOfCountsPerSecond32Counter PerSecProcessed = new RateOfCountsPerSecond32Counter("Transactions/Sec Processed", "The total number of transactions processed per second");
	///				
	///				// ...
	///			}
	///		</code>
	/// 
	///		<para>To install the counters in a category, invoke the <see cref="Install"/> method on your category class:</para>
	///		<code>
	///			FooCounters.Install();
	///		</code>
	/// 
	///		<para>To uninstall the counters in a category, invoke the <see cref="Uninstall"/>
	///		method on your category class:</para>
	///		<code>
	///			FooCounters.Uninstall();
	///		</code>
	/// 
	///		<para>To increment a counter, invoke the Increment method on the 
	///		appropriate counter object, while specifying which instances to update:</para>
	///		<code>
	///			var instanceName = "My FooCounters Instance";
	///			var myFooCounters = FooCounters.GetInstance(instanceName);
	///			myFooCounters.TotalProcessed.Increment(1);
	///		</code>
	/// </remarks>
	public abstract class PerfCounterCategory<Implementation>
		where Implementation : PerfCounterCategory<Implementation>
	{
		private static readonly LogWrapper _log = new LogWrapper();
		private static ConstructorInfo _constructor;
		private static readonly object _constructorLock = new object();
		private static volatile Dictionary<string, Implementation> _instances = new Dictionary<string, Implementation>();
		private static readonly object _instancesLock = new object();
		private const string InstallationInstanceName = "05fd667e04b84eaf8553541c64872708";

		private static ConstructorInfo Constructor
		{
			get
			{
				if (_constructor != null) return _constructor;
				lock (_constructorLock)
				{
					if (_constructor != null) return _constructor;

					var constructors = typeof(Implementation).GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
					if (constructors.Length != 1)
					{
						throw new InvalidOperationException(String.Format("The type {0} must have only one private constructor that accepts a string instance name.", typeof(Implementation)));
					}

					var constructor = typeof(Implementation).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string) }, null);
					if (constructor == null)
					{
						throw new InvalidOperationException(String.Format("The type {0} must have a private constructor that accepts a string instance name.", typeof(Implementation)));
					}
					Thread.MemoryBarrier();
					_constructor = constructor;
					return _constructor;
				}
			}
		}

		private static Implementation CreateNew(string instanceName)
		{
			return (Implementation)Constructor.Invoke(new object[] { instanceName });
		}

		/// <summary>
		/// Gets the <see cref="PerfCounterCategory{T}"/> instance with the specified instance name.
		/// </summary>
		/// <param name="instanceName">The instance name of the <see cref="PerfCounterCategory{T}"/> to get.</param>
		/// <returns>The <see cref="PerfCounterCategory{T}"/> instance with the specified instance name.</returns>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="instanceName"/> is <see langword="null"/>.</para>
		/// </exception>
		public static Implementation GetInstance(string instanceName)
		{
			return GetInstance(instanceName, true);
		}

		/// <summary>
		/// Gets the <see cref="PerfCounterCategory{T}"/> instance with the specified instance name.
		/// </summary>
		/// <param name="instanceName">The instance name of the <see cref="PerfCounterCategory{T}"/> to get.</param>
		///<param name="createIfNotFound"><see langword="true"/> to create the counter instance if it doesn't exist. <see langword="false"/> otherwise.</param>
		///<returns>The <see cref="PerfCounterCategory{T}"/> instance with the specified instance name.</returns>
		/// <exception cref="ArgumentNullException">
		///	<para><paramref name="instanceName"/> is <see langword="null"/>.</para>
		/// </exception>
		public static Implementation GetInstance(string instanceName, bool createIfNotFound)
		{
			if (string.IsNullOrEmpty(instanceName)) throw new ArgumentNullException("instanceName");

			Implementation result;
			if (_instances.TryGetValue(instanceName, out result)) return result;
			if (!createIfNotFound) return null;
			lock (_instancesLock)
			{
				if (_instances.TryGetValue(instanceName, out result)) return result;

				var newInstances = _instances.ToDictionary(item => item.Key, item => item.Value);
				result = CreateNew(instanceName);
				newInstances.Add(instanceName, result);
				// Ensures all updates to newinstances are perceived by other
				// threads before they are made publicly available. This is necessary
				// on processors with weaker memory models like IA-64.
				Thread.MemoryBarrier();
				_instances = newInstances;
				return result;
			}
		}

		/// <summary>
		///		<para>Gets all available instances within this category.</para>
		/// </summary>
		/// <returns>An enumeration containing all available instances within this category; never <see langword="null"/>.</returns>
		public static IEnumerable<Implementation> GetAllInstances()
		{
			foreach (var pair in _instances)
			{
				if (pair.Value.InstanceName != InstallationInstanceName)
				{
					yield return pair.Value;
				}
			}
		}

		/// <summary>
		///	<para>Gets all underlying <see cref="PerformanceCounter"/> encapsulated
		///	by all instances of <see cref="Implementation"/>.</para>
		/// </summary>
		/// <returns>
		///	<para>All underlying <see cref="PerformanceCounter"/> encapsulated
		///	by all instances of <see cref="Implementation"/>.</para>
		/// </returns>
		public static IEnumerable<PerformanceCounter> GetAllCounters()
		{
			var instances = _instances;

			foreach (var item in instances)
			{
				foreach (var counter in item.Value.GetCounters())
				{
					yield return counter;
				}
			}
		}

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="PerfCounterCategory{Implementation}"/> class.</para>
		/// </summary>
		/// <param name="category">
		/// 	<para>The category of performance counters that this instance manages.</para>
		/// </param>
		/// <param name="instanceName">
		///	<para>The instance name of the performance counters that this instance manages.</para>
		/// </param>
		/// <param name="obsoleteCategories">
		/// 	<para>The categories of performance counters that should be deleted when the	
		/// 	above <paramref name="category"/> is installed or uninstalled.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="category"/> is <see langword="null"/>.</para>
		/// 	<para>- or -</para>
		/// 	<para>The argument <paramref name="instanceName"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="InvalidOperationException">
		///	<para>One or more <see cref="PerfCounter"/> field members of
		///	<typeparamref name="Implementation"/> is not assigned prior to construction.</para>
		/// 	<para>- or -</para>
		///	<para>One or more <see cref="PerfCounter"/> field members of
		///	<typeparamref name="Implementation"/> is not marked <see langword="readonly"/>.</para>
		/// </exception>
		protected PerfCounterCategory(string category, string instanceName, params string[] obsoleteCategories)
		{
			if (category == null) throw new ArgumentNullException("category");
			if (instanceName == null) throw new ArgumentNullException("instanceName");

			_category = category;
			_instanceName = instanceName;
			_obsoleteCategories = obsoleteCategories;

			_counters = new Dictionary<string, PerfCounter>();
			foreach (FieldInfo field in this.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
			{
				if (typeof(PerfCounter).IsAssignableFrom(field.FieldType))
				{
					if (!field.IsInitOnly)
					{
						throw new InvalidOperationException(string.Format(
							"One or more PerfCounter field members of {0} are not marked readonly",
							typeof(Implementation)));
					}

					PerfCounter counter = (PerfCounter)field.GetValue(this);

					if (counter == null)
					{
						throw new InvalidOperationException(string.Format(
							"One or more PerfCounter field members of {0} have not been assigned prior to construction",
							typeof(Implementation)));
					}

					counter.Initialize(_category, _instanceName);
					_counters.Add(counter.Name, counter);
				}
			}

			if (_counters.Count == 0)
			{
				throw new InvalidOperationException(string.Format(
					"{0} does not have any readonly PerfCounter field members.",
					typeof(Implementation)));
			}

			InitializeCounters();
		}

		private readonly string _category;
		private readonly string _instanceName;
		private readonly string[] _obsoleteCategories;
		private readonly Dictionary<string, PerfCounter> _counters;

		private void InitializeCounters()
		{
			_log.InfoFormat("Verifying performance counter installation for category '{0}'...", _category);

			try
			{
				if (!PerformanceCounterCategory.Exists(_category))
				{
					_log.WarnFormat("Performance counters in category '{0}' are not installed.", _category);
					return;
				}

				// Zero all counters in the current category.
				_log.InfoFormat("Zeroing all performance counters in category/instance '{0}'/'{1}' ", _category, _instanceName);

				var cat = new PerformanceCounterCategory(_category);
				if (cat.InstanceExists(_instanceName))
				{
					foreach (PerformanceCounter counter in cat.GetCounters(_instanceName))
					{
						try
						{
							counter.ReadOnly = false;
							counter.RawValue = 0;
						}
						finally
						{
							counter.Dispose();
						}
					}
				}
			}
			catch (Exception x)
			{
				_log.Error(String.Format("Initializing perf counter category/instance {0}/{1} failed.", _category, _instanceName),x);
			}
		}

		/// <summary>
		/// Gets the instance name for the <see cref="PerformanceCounter"/> instances managed by this instance.
		/// </summary>
		/// <value>The instance name for the <see cref="PerformanceCounter"/> instances managed by this instance.</value>
		public string InstanceName
		{
			get { return _instanceName; }
		}

		/// <summary>
		/// 	<para>Installs performance counters managed by this instance.</para>
		/// </summary>
		/// <remarks><para>This installer will check to see the counter categories already exist.  If any categories are missing,
		/// then the installer attempts to install all catgories.  If all categories were already installed, no action is taken.</para></remarks>
		public static void Install()
		{
            var allAreInstalled =
                GetInstance(InstallationInstanceName)._counters.Values
                    .Select(counter => counter.CategoryName)
                    .Distinct()
                    .All(PerformanceCounterCategory.Exists);

		    if (!allAreInstalled) PerfCounterInstaller.Install(GetInstance(InstallationInstanceName)._counters.Values);
		}

		/// <summary>
		/// 	<para>Uninstalls performance counters managed by this instance.</para>
		/// </summary>
		public static void Uninstall()
		{
			var tempInstance = GetInstance(InstallationInstanceName);

			PerfCounterInstaller.Uninstall(tempInstance._category);
			PerfCounterInstaller.Uninstall(tempInstance._obsoleteCategories);
			lock (_instancesLock)
			{
				var oldInstances = _instances;
				var instances = new Dictionary<string, Implementation>();
				Thread.MemoryBarrier();
				_instances = instances;
				foreach (var instance in oldInstances.Values)
				{
					instance.RemoveInstance();
				}
			}
		}

		/// <summary>
		/// 	<para>Gets all performance counters within this 
		/// 	instance.</para>
		/// </summary>
		/// <returns>
		/// 	<para>A collection of <see cref="PerformanceCounter"/> objects;
		/// 	never <see langword="null"/>.</para>
		/// </returns>
		public IEnumerable<PerformanceCounter> GetCounters()
		{
			foreach (PerfCounter counter in _counters.Values)
			{
				PerformanceCounter target = counter.InnerCounter;
				if (target != null) yield return target;
			}
		}

		/// <summary>
		/// Removes this instance from all counters under this category.
		/// </summary>
		public void RemoveInstance()
		{
			foreach (PerfCounter counter in _counters.Values)
			{
				counter.RemoveInstance();
			}
		}

		/// <summary>
		/// Removes all instances from a performance counter category that are not in use.
		/// </summary>
		/// <param name="categoryName">Name of the category.</param>
		protected static void RemoveUnreferencedInstances(string categoryName)
		{
			lock (_instancesLock)
			{
				var category = new PerformanceCounterCategory(categoryName);
				foreach (var instanceName in category.GetInstanceNames())
				{
					if (!_instances.ContainsKey(instanceName))
					{
						foreach (var counter in category.GetCounters(instanceName))
						{
							try
							{
								counter.ReadOnly = false;
								counter.RemoveInstance();
							}
							catch (Exception e)
							{
								Trace.WriteLine(string.Format("Exception removing counter from category={0} instance={1}: {2}",
								                              categoryName,
								                              instanceName,
															  e));
							}
							finally
							{
								counter.Dispose();
							}
						}
					}
				}
			}
		}
	}
}