using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using MySpace.Common.Dynamic;

namespace MySpace.Common.Barf.Parts
{
	internal class BinaryFormatterPartBuilder : IPartBuilder
	{
		private static readonly BinaryFormatterPartBuilder _instance = new BinaryFormatterPartBuilder();

		public static BinaryFormatterPartBuilder Instance
		{
			[DebuggerStepThrough]
			get { return _instance; }
		}

		private BinaryFormatterPartBuilder() { }

		#region IPartBuilder Members

		public void GenerateSerializePart(GenSerializeContext context)
		{
			var g = context.Generator;
			g.Load(context.SerializationArgs);
			g.LoadMember("BinaryFormatter");
			g.BeginCall(typeof(BinaryFormatter).ResolveMethod("Serialize", typeof(Stream), typeof(object)));
			{
				g.Load(context.Writer);
				g.LoadMember("BaseStream");
				g.Load(context.Member);
				if (context.Member.Type.IsValueType)
				{
					g.Box();
				}
			}
			g.EndCall();
		}

		public void GenerateDeserializePart(GenDeserializeContext context)
		{
			var g = context.Generator;
			g.BeginAssign(context.Member);
			{
				g.Load(context.DeserializationArgs);
				g.LoadMember("BinaryFormatter");
				g.BeginCall(typeof(BinaryFormatter).ResolveMethod("Deserialize", typeof(Stream)));
				{
					g.Load(context.Reader);
					g.LoadMember("BaseStream");
				}
				g.EndCall();
				if (context.Member.Type.IsValueType)
				{
					g.UnboxAny(context.Member.Type);
				}
				else
				{
					g.Cast(context.Member.Type);
				}
			}
			g.EndAssign();
		}

		public void GenerateFillPart(GenFillContext context)
		{
			context.GenerateSkippedWarning(true, "fields that use BinaryFormatter can't be filled");
		}

		public void GenerateAssertAreEqualPart(GenAssertAreEqualContext context)
		{
			context.GenerateRaiseNotSupported();
		}

		#endregion
	}
}
