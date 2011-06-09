using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using MySpace.Common.Dynamic;

namespace MySpace.Common.Barf
{
	/// <summary>
	/// Builds methods for serializing and deserializing barf objects.
	/// </summary>
	internal class BarfSerializerBuilder
	{
		private readonly BarfTypeDefinition _def;

		/// <summary>
		/// Initializes a new instance of the <see cref="BarfSerializerBuilder"/> class.
		/// </summary>
		/// <param name="typeDefinition">The type definition to create methods for.</param>
		public BarfSerializerBuilder(BarfTypeDefinition typeDefinition)
		{
			if (typeDefinition == null) throw new ArgumentNullException("typeDefinition");

			_def = typeDefinition;
		}

		/// <summary>
		/// Generates the body of the <see cref="BarfSerializer{T}.CreateEmpty()"/>.
		/// </summary>
		/// <param name="msilWriter">The MSIL writer to write to.</param>
		public void GenerateCreateEmptyMethod(ICilWriter msilWriter)
		{
			var g = new MethodGenerator(msilWriter);
			if (_def.Type.IsValueType)
			{
				g.LoadDefaultOf(_def.Type);
			}
			else
			{
				g.NewObject(_def.Type, Type.EmptyTypes);
			}
			g.Return();
		}

		/// <summary>
		/// Generates the body of the serialize method.
		/// </summary>
		/// <param name="msilWriter">The MSIL writer to write to.</param>
		public void GenerateSerializeMethod(ICilWriter msilWriter)
		{
			var g = new MethodGenerator(msilWriter);
			var instance = g.GetParameter(0);
			var args = g.GetParameter(1);

			if (!_def.Type.IsValueType)
			{
				g.If(() =>
				{
					g.Load(instance);
					return BinaryOperator.IsNull;
				});
				{
					g.Load(args);
					// todo - call through MemberResolver
					g.Call(typeof(BarfSerializationArgs).GetMethod("WriteNullObject"));
					g.Return();
				}
				g.EndIf();
			}

			IVariable typeContext;
			MethodInfo beginMethod;
			MethodInfo endMethod;
			if (_def.IsForwardCompatible)
			{
				typeContext = g.DeclareLocal(typeof(BarfSerializationArgs.TypeContext));
				beginMethod = typeof(BarfSerializationArgs).GetMethods(BindingFlags.Instance | BindingFlags.Public)
					.Where<MethodInfo>(m => m.Name == "BeginObject")
					.Where<MethodInfo>(m =>
					{
						var ps = m.GetParameters();
						return ps.Length == 1 && ps[0].ParameterType.IsGenericParameter;
					})
					.FirstOrDefault<MethodInfo>()
					.MakeGenericMethod(_def.Type);
				endMethod = typeof(BarfSerializationArgs).ResolveMethod("EndObject", typeof(BarfSerializationArgs.TypeContext));
			}
			else
			{
				typeContext = g.DeclareLocal(typeof(long));
				beginMethod = typeof(BarfSerializationArgs)
					.ResolveMethod("BeginObject", Type.EmptyTypes)
					.MakeGenericMethod(_def.Type);
				endMethod = typeof(BarfSerializationArgs).ResolveMethod("EndObject", typeof(long));
			}
			g.Load(args);
			g.BeginCall(beginMethod);
			if (_def.IsForwardCompatible)
			{
				g.Load(instance);
			}
			g.EndCall();
			g.Store(typeContext);

			foreach (var part in _def.Parts)
			{
				Trace.WriteLine("\tBuilding Serialize Part - " + part.FullName);
				g.BeginScope();
				{
					var context = new GenSerializeContext(g, part, g.CreateExpression(instance));
					part.GetCurrentBuilder()
						.GenerateSerializePart(context);
				}
				g.EndScope();
			}

			g.Load(args);
			g.BeginCall(endMethod);
			{
				g.Load(typeContext);
			}
			g.EndCall();

			g.Return();
		}

		public void GenerateInnerDeserializeMethod(ICilWriter msilWriter)
		{
			var g = new MethodGenerator(msilWriter);

			var instance = g.CreateExpression(g.GetParameter(0));
			var args = g.CreateExpression(g.GetParameter(1));
			var header = g.DeclareLocal(typeof(BarfObjectHeader));

			g.BeginAssign(header);
			{
				g.Load(args);
				g.Call(typeof(BarfDeserializationArgs)
					.ResolveMethod("BeginObject")
					.MakeGenericMethod(_def.Type));
			}
			g.EndAssign();

			var version = g.CreateExpression(header).AddMember("Version");
			g.If(() =>
			{
				g.Load(header);
				g.LoadMember("IsNull");
				return BinaryOperator.IsTrue;
			});
			{
				g.BeginAssign(instance);
				g.LoadNull();
				g.EndAssign();
			}
			g.Else();
			{
				g.If(() =>
				{
					g.Load(instance);
					return BinaryOperator.IsNull;
				});
				{
					if (_def.Type.IsAbstract)
					{
						g.BeginCall(typeof(BarfErrors).ResolveMethod("RaiseAbstractConstructionError", typeof(Type)));
						g.Load(_def.Type);
						g.EndCall();
					}
					else
					{
						g.BeginAssign(instance);
						g.NewObject(instance.Type);
						g.EndAssign();
					}
				}
				g.EndIf();

				var partsByVersion = _def.Parts
					.GroupBy<PartDefinition, int>(part => part.Version)
					.OrderBy<IGrouping<int, PartDefinition>, int>(group => group.Key);

				int count = 0;
				foreach (var versionGroup in partsByVersion)
				{
					g.If(() =>
					{
						g.Load(version);
						g.Load(versionGroup.Key);
						return BinaryOperator.GreaterThanOrEqualTo;
					});
					{
						foreach (var part in versionGroup)
						{
							Trace.WriteLine("\tBuilding Deserialize Part - " + part.FullName);

							g.BeginScope();
							var context = new GenDeserializeContext(g, part, instance, args, header);
							part.GetCurrentBuilder()
								.GenerateDeserializePart(context);
							g.EndScope();
						}
					}
					count++;
				}
				for (; count > 0; --count)
				{
					g.EndIf();
				}

				if (_def.IsForwardCompatible)
				{
					g.If(() =>
					{
						g.Load(header).LoadMember("Version");
						g.Load(_def.CurrentVersion);
						return BinaryOperator.GreaterThan;
					});
					{
						g.Load(args);
						g.BeginCall(typeof(BarfDeserializationArgs)
							.ResolveMethod(
								"CaptureFutureData",
								new[] { _def.Type },
								typeof(BarfObjectHeader), new GenericParameter(0).MakeByRefType()));
						{
							g.Load(header);
							g.Load(instance, LoadOptions.AnyAsAddress);
						}
						g.EndCall();
					}
					g.EndIf();
				}
			}
			g.EndIf();

			g.Load(args).BeginCall(typeof(BarfDeserializationArgs)
				.ResolveMethod("EndObject", new[] { _def.Type }, typeof(BarfObjectHeader)));
			{
				g.Load(header);
			}
			g.EndCall();

			g.Return();
		}
	}
}
