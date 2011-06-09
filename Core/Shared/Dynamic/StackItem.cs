using System;
using System.Diagnostics;

namespace MySpace.Common.Dynamic
{
	internal struct StackItem : IEquatable<StackItem>
	{
		public static bool operator ==(StackItem a, StackItem b)
		{
			if ((object)a == null) return (object)b == null;
			if ((object)b == null) return false;
			return a.Equals(b);
		}

		public static bool operator !=(StackItem a, StackItem b)
		{
			return !(a == b);
		}

		private readonly Type _type;
		private readonly bool _boxed;
		private Type _normalType;

		public StackItem(Type type)
			: this(type, LoadOptions.Default)
		{
		}

		public StackItem(Type type, LoadOptions options)
		{
			if (type.IsEnum)
			{
				type = Enum.GetUnderlyingType(type);
			}

			_boxed = options.ShouldBox(type);

			type = type.ResolveByRef(options);

			_type = type;
			_normalType = _type.IsByRef ? _type.GetElementType() : _type;
		}

		public bool IsAddress
		{
			get { return _type.IsByRef; }
		}

		public ItemType ItemType
		{
			get
			{
				return _type.IsByRef
					? LoadOptions.AnyAsAddress.GetItemType(Type, _boxed)
					: LoadOptions.Default.GetItemType(Type, _boxed);
			}
		}

		public Type Type
		{
			[DebuggerStepThrough]
			get { return _normalType; }
		}

		#region IEquatable<EvalStackItem> Members

		public bool Equals(StackItem other)
		{
			return _boxed == other._boxed && _type == other._type;
		}

		public override bool Equals(object obj)
		{
			return Equals((StackItem)obj);
		}

		public override int GetHashCode()
		{
			return _boxed.GetHashCode() ^ _type.GetHashCode();
		}

		#endregion

		public override string ToString()
		{
			return string.Format("ItemType={0}, Type={1}", ItemType, Type == null ? "null" : Type.Name);
		}
	}
}
