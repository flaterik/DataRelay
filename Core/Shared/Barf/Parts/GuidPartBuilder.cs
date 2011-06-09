using System;
using System.Diagnostics;
using System.IO;
using MySpace.Common.Dynamic;

namespace MySpace.Common.Barf.Parts
{
    internal class GuidPartBuilder : IPartBuilder
    {
        private const int _guidSize = 16;
        private static readonly GuidPartBuilder _instance = new GuidPartBuilder();

        public static GuidPartBuilder Instance
        {
            [DebuggerStepThrough]
            get { return _instance; }
        }

        private GuidPartBuilder()
        {
        }

        #region IPartBuilder Members

        void IPartBuilder.GenerateSerializePart(GenSerializeContext context)
        {
            var g = context.Generator;
            g.Load(context.Writer);
            g.LoadMember("BaseStream");
            g.BeginCall(typeof(Stream).GetMethod("Write", new[] { typeof(byte[]), typeof(int), typeof(int) }));
            {
                g.Load(context.Member, LoadOptions.ValueAsAddress);
                g.BeginCall(typeof(Guid).GetMethod("ToByteArray", Type.EmptyTypes));
                g.EndCall();
                // ,
                g.Load(0);
                // ,
                g.Load(_guidSize);
            }
            g.EndCall();
        }

        void IPartBuilder.GenerateDeserializePart(GenDeserializeContext context)
		{
			var g = context.Generator;

			var buffer = g.DeclareLocal(typeof(byte[]));

			g.BeginAssign(buffer);
			{
				g.BeginNewArray(typeof(byte));
				{
					g.Load(_guidSize);
				}
				g.EndNewArray();
			}
			g.EndAssign();

			g.If(() =>
			{
				g.Load(context.Reader);
				g.LoadMember("BaseStream");
				g.BeginCall(typeof(Stream).GetMethod("Read", new[] { typeof(byte[]), typeof(int), typeof(int) }));
				{
					g.Load(buffer);
					g.Load(0);
					g.Load(_guidSize);
				}
				g.EndCall();
				g.Load(_guidSize);
				return BinaryOperator.LessThan;
			});
			{
				g.BeginNewObject(typeof(EndOfStreamException).GetConstructor(Type.EmptyTypes));
				g.EndNewObject();
				g.Throw();
			}
			g.EndIf();

			g.BeginAssign(context.Member);
			{
				g.BeginNewObject(typeof(Guid).GetConstructor(new[] { typeof(byte[]) }));
				{
					g.Load(buffer);
				}
				g.EndNewObject();
			}
			g.EndAssign();
		}

		void IPartBuilder.GenerateFillPart(GenFillContext context)
		{
			var g = context.Generator;
			g.BeginAssign(context.Member);
			{
				g.Call(typeof(Guid).ResolveMethod("NewGuid"));
			}
			g.EndAssign();
		}

		void IPartBuilder.GenerateAssertAreEqualPart(GenAssertAreEqualContext context)
		{
			context.GenerateSimpleAssert();
		}

		#endregion
	}
}
