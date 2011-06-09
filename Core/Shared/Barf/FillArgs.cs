using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MySpace.Common.Barf
{
	/// <summary>
	/// Arguments passed to the <see cref="IBarfTester{T}.Fill(FillArgs)"/> method.
	/// </summary>
	public class FillArgs
	{
		private readonly FillSettings _settings;
		private readonly Factory<Type, Array> _enumValues = Algorithm.LazyIndexer<Type, Array>(Enum.GetValues);
		private Dictionary<Type, int> _recursionDepths;

		internal FillArgs(Random random, FillSettings settings)
		{
			Random = random;
			_settings = settings;
		}

		/// <summary>
		/// Gets or sets the random from which to generate random values.
		/// </summary>
		/// <value>The random from which to generate random values.</value>
		public Random Random { get; private set; }

		internal bool BeginDeferredFill<T>()
		{
			if (_recursionDepths == null)
			{
				_recursionDepths = new Dictionary<Type, int>();
			}
			int currentDepth;
			_recursionDepths.TryGetValue(typeof(T), out currentDepth);
			if (currentDepth > 0 && currentDepth > _settings.MaxRecursionDepth)
			{
				return false;
			}
			_recursionDepths[typeof(T)] = currentDepth + 1;
			return true;
		}

		internal void EndDeferredFill<T>()
		{
			_recursionDepths[typeof(T)] = _recursionDepths[typeof(T)] - 1;
		}

		/// <summary>
		/// Gets the next random <see cref="Boolean"/> value.
		/// </summary>
		/// <value>The next random <see cref="Boolean"/> value.</value>
		public bool NextBoolean
		{
			get { return Random.Next() % 2 == 0; }
		}

		/// <summary>
		/// Gets the next random <see cref="Byte"/> value.
		/// </summary>
		/// <value>The next random <see cref="Byte"/> value.</value>
		public byte NextByte
		{
			get { return (byte)Random.Next(byte.MinValue, byte.MaxValue + 1); }
		}

		/// <summary>
		/// Gets the next random <see cref="Int16"/> value.
		/// </summary>
		/// <value>The next random <see cref="Int16"/> value.</value>
		public short NextInt16
		{
			get { return (short)Random.Next(short.MinValue, short.MaxValue + 1); }
		}

		/// <summary>
		/// Gets the next random <see cref="Int32"/> value.
		/// </summary>
		/// <value>The next random <see cref="Int32"/> value.</value>
		public int NextInt32
		{
			get { return NextBoolean ? Random.Next() : -Random.Next(); }
		}

		/// <summary>
		/// Gets the next random <see cref="Int64"/> value.
		/// </summary>
		/// <value>The next random <see cref="Int64"/> value.</value>
		public long NextInt64
		{
			get
			{
				var buff = new byte[sizeof(long)];
				Random.NextBytes(buff);
				return BitConverter.ToInt64(buff, 0);
			}
		}

		/// <summary>
		/// Gets the next random <see cref="Int16"/> value.
		/// </summary>
		/// <value>The next random <see cref="Int16"/> value.</value>
		public ushort NextUInt16
		{
			get { return (ushort)Random.Next(ushort.MinValue, ushort.MaxValue + 1); }
		}

		/// <summary>
		/// Gets the next random <see cref="Int32"/> value.
		/// </summary>
		/// <value>The next random <see cref="Int32"/> value.</value>
		public uint NextUInt32
		{
			get
			{
				var buff = new byte[sizeof(uint)];
				Random.NextBytes(buff);
				return BitConverter.ToUInt32(buff, 0);
			}
		}

		/// <summary>
		/// Gets the next random <see cref="Int64"/> value.
		/// </summary>
		/// <value>The next random <see cref="Int64"/> value.</value>
		public ulong NextUInt64
		{
			get
			{
				var buff = new byte[sizeof(ulong)];
				Random.NextBytes(buff);
				return BitConverter.ToUInt64(buff, 0);
			}
		}

		/// <summary>
		/// Gets the next random <see cref="Single"/> value.
		/// </summary>
		/// <value>The next random <see cref="Single"/> value.</value>
		public float NextSingle
		{
			get
			{
				float result1 = Random.Next();
				float result2 = Random.Next();
				while (result2 == 0)
					result2 = Random.Next();
				return result1 / result2;
			}
		}

		/// <summary>
		/// Gets the next random <see cref="Double"/> value.
		/// </summary>
		/// <value>The next random <see cref="Double"/> value.</value>
		public double NextDouble
		{
			get
			{
				double result1 = Random.Next();
				double result2 = Random.Next();
				while (result2 == 0)
					result2 = Random.Next();
				return result1 / result2;
			}
		}

		/// <summary>
		/// Gets the next random <see cref="Char"/> value.
		/// </summary>
		/// <value>The next random <see cref="Char"/> value.</value>
		public char NextChar
		{
			get
			{
				var type = Random.Next(0, 3);
				switch (type)
				{
					case 0: return (char)((short)'a' + Random.Next(0, 26));
					case 1: return (char)((short)'A' + Random.Next(0, 26));
					default: return (char)((short)' ' + Random.Next(0, 0x20));
				}
			}
		}

		/// <summary>
		/// Gets the next random <see cref="String"/> value.
		/// </summary>
		/// <value>The next random <see cref="String"/> value.</value>
		public string NextString
		{
			get
			{
				var length = Random.Next(1, 30);
				var chars = new char[length];
				for (int i = 0; i < chars.Length; ++i)
				{
					chars[i] = NextChar;
				}
				return new string(chars);
			}
		}

		/// <summary>
		/// Gets the next random <see cref="DateTime"/> value.
		/// </summary>
		/// <value>The next random <see cref="DateTime"/> value.</value>
		public DateTime NextDateTime
		{
			get
			{
				var result = NextBoolean
					? DateTime.Now
					: DateTime.UtcNow;
				var dif = Random.Next((int)TimeSpan.FromDays(365 * 10).TotalSeconds);
				if (NextBoolean)
				{
					result.Add(TimeSpan.FromSeconds(dif));
				}
				else
				{
					result.Subtract(TimeSpan.FromSeconds(dif));
				}
				return result;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the next reference type should be filled with
		/// <see langword="null"/> or a random value.
		/// </summary>
		/// <value><c>true</c> if the next reference type should be filled with <see langword="null"/>; otherwise, <c>false</c>.</value>
		public bool NextIsNull
		{
			get
			{
				if (_settings.AllowNulls)
				{
					return Random.Next() % 2 == 0;
				}

				return false;
			}
		}

		/// <summary>
		/// Gets the size of the next collection.
		/// </summary>
		/// <value>The size of the next collection.</value>
		public int NextCollectionSize
		{
			get
			{
				return Random.Next((int)_settings.MinCollectionSize, (int)_settings.MaxCollectionSize + 1);
			}
		}

		/// <summary>
		/// Gets the next random value of the specified enumeration type.
		/// </summary>
		/// <typeparam name="T">The type of the enumeration.</typeparam>
		/// <returns>
		///		<para>The next random value of the specified enumeration type.</para>
		/// </returns>
		/// <exception cref="ArgumentException">
		///		<para><typeparamref name="T"/> is not an enumeration.</para>
		/// </exception>
		public T GetRandomEnumValue<T>()
		{
			if (!typeof(T).IsEnum)
			{
				throw new ArgumentException("Type argument T must be an enumeration", "T");
			}

			var possibleValues = _enumValues(typeof(T));
			return (T)possibleValues.GetValue(Random.Next(0, possibleValues.Length));
		}

		private static readonly Factory<string, bool> _traceOnce = Algorithm.LazyIndexer<string, bool>(message =>
		{
			Trace.TraceWarning(message);
			return true;
		});

		/// <summary>
		/// Raises a warning to the framework that the specified BARF part was skipped.
		/// </summary>
		/// <param name="partName">Name of the skipped part.</param>
		/// <param name="reason">The reason why the part was skipped.</param>
		public void RaiseSkippedWarning(string partName, string reason)
		{
			string finalMessage = "Member=\"" + partName + "\" couldn't be filled with random values";
			if (!string.IsNullOrEmpty(reason))
			{
				finalMessage += ", Reason=\"" + reason + "\"";
			}
			switch (_settings.NotSupportedBehavior)
			{
				case NotSupportedBehavior.RaiseInconclusive:
					Assert.Inconclusive(finalMessage);
					break;
				case NotSupportedBehavior.TraceField:
					Trace.TraceWarning(finalMessage);
					break;
				case NotSupportedBehavior.TraceFieldOnce:
					_traceOnce(finalMessage);
					break;
			}
		}
	}
}
