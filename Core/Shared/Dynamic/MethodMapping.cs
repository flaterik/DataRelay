using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace MySpace.Common.Dynamic
{
	internal class MethodMapping :IMapping
	{
		private readonly MethodInfo _sourceGetter;
		private readonly MethodInfo _destSetter;

		internal MethodMapping(MethodInfo sourceGetter, MethodInfo destSetter)
		{
			_sourceGetter = sourceGetter;
			_destSetter = destSetter;
		}

		#region IMapping Members

		public void GenerateMap(ILGenerator gen, int sourceArgIndex, int destArgIndex)
		{
			gen.Emit(OpCodes.Ldarg, destArgIndex);
			gen.Emit(OpCodes.Castclass, _destSetter.DeclaringType);
			gen.Emit(OpCodes.Ldarg, sourceArgIndex);
			gen.Emit(OpCodes.Castclass, _sourceGetter.DeclaringType);
			gen.EmitCall(_sourceGetter.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, _sourceGetter, null);
			gen.EmitCall(_destSetter.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, _destSetter, null);
		}

		#endregion
	}
}
