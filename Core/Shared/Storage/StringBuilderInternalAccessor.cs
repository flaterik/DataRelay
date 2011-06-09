using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using MySpace.Common.IO;

namespace MySpace.Common.Storage
{
	public partial struct DataBuffer
	{
		internal abstract class StringBuilderInternalAccessor
		{
			private static readonly Func<int, string> _stringAllocator;
			private static readonly Func<StringBuilder, string> _stringValueGetter;
			private static readonly Func<StringBuilder, StringBuilder> _previousGetter;
			private static readonly Action<StringBuilder, StringBuilder> _previousSetter;
			private static readonly Func<StringBuilder, char[]> _charsGetter;
			private static readonly Action<StringBuilder, char[]> _charsSetter;
			private static readonly Func<StringBuilder, int> _offsetGetter, _lengthGetter;
			private static readonly Action<StringBuilder, int> _offsetSetter, _lengthSetter;
			private static readonly StringBuilderInternalAccessor _instance;

			private static bool GetFieldAccessors<T>(string name, bool writeAlso,
				out Func<StringBuilder, T> getter, out Action<StringBuilder, T> setter)
			{
				var field = typeof(StringBuilder).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
				if (field == null)
				{
					getter = null;
					setter = null;
					return false;
				}
				var method = new DynamicMethodHelper(name + "_StringBuilder_getter",
					typeof(T), new[] { typeof(StringBuilder) },
					typeof(StringBuilder));
				method.GetField(0, field);
				method.Return();
				getter = method.Compile<Func<StringBuilder, T>>();
				if (writeAlso)
				{
					method = new DynamicMethodHelper(name + "_StringBuilder_setter",
						null, new[] { typeof(StringBuilder), typeof(T) },
						typeof(StringBuilder));
					method.PushArg(0);
					method.PushArg(1);
					method.SetField(field);
					method.Return();
					setter = method.Compile<Action<StringBuilder, T>>();
				}
				else
				{
					setter = null;
				}
				return true;
			}

			static StringBuilderInternalAccessor()
			{
				var method = typeof (string).GetMethod("FastAllocateString", BindingFlags.NonPublic |
					BindingFlags.Static);
				var dynamic = new DynamicMethodHelper("FastAllocateString_String_caller", typeof (string),
					new[] {typeof (int)}, typeof (string));
				dynamic.PushArg(0);
				dynamic.CallMethod(method);
				dynamic.Return();
				_stringAllocator = dynamic.Compile<Func<int, string>>();
				Action<StringBuilder, string> dummySetter;
				if (GetFieldAccessors("m_StringValue", false, out _stringValueGetter,
					out dummySetter))
				{
					_instance = new StringBuilderInternalAccessorOriginal();
				}
				else
				{
					GetFieldAccessors("m_ChunkPrevious", true, out _previousGetter, out _previousSetter);
					GetFieldAccessors("m_ChunkChars", true, out _charsGetter, out _charsSetter);
					GetFieldAccessors("m_ChunkOffset", true, out _offsetGetter, out _offsetSetter);
					GetFieldAccessors("m_ChunkLength", true, out _lengthGetter, out _lengthSetter);
					_instance = new StringBuilderInternalAccessor40();
				}
			}

			public abstract object GetObject(StringBuilder sbd);

			public string AllocateString(int length)
			{
				return _stringAllocator(length);
			}

			public static StringBuilderInternalAccessor Instance { get { return _instance; } }

			sealed class StringBuilderInternalAccessorOriginal : StringBuilderInternalAccessor
			{
				public override object GetObject(StringBuilder sbd)
				{
					return _stringValueGetter(sbd);
				}
			}

			sealed class StringBuilderInternalAccessor40 : StringBuilderInternalAccessor
			{
				public unsafe override object GetObject(StringBuilder sbd)
				{
					var offset = _offsetGetter(sbd);
					var chunk = _charsGetter(sbd);
					if (offset == 0)
					{
						// not broken up, can return as is
						return chunk;
					}
					// is broken up, have to consolidate
					var totalLength = offset + _lengthGetter(sbd);
					var totalChunk = new char[offset + chunk.Length]; // capacity
					fixed (char* dst = totalChunk)
					{
						for (var current = sbd; current != null; current = _previousGetter(current))
						{
							chunk = _charsGetter(current);
							var length = _lengthGetter(current);
							offset = _offsetGetter(current);
							fixed (char* src = chunk)
							{
								CopyMemory(new IntPtr(dst + offset), new IntPtr(src), length << _charShift);
							}
						}
					}
					_offsetSetter(sbd, 0);
					_lengthSetter(sbd, totalLength);
					_charsSetter(sbd, totalChunk);
					_previousSetter(sbd, null);
					return totalChunk;
				}
			}
		}
	}
}
