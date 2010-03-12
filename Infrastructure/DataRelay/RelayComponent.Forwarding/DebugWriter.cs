using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using MySpace.DataRelay.Configuration;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{

	internal class DebugWriter
	{
		private static bool _writeMessageTrace;
		internal static bool WriteMessageTrace
		{
			get
			{
				return _writeMessageTrace;
			}
			set
			{
				_writeMessageTrace = value;
				if (_messageTracer != null)
					_messageTracer.Activated = value;
			}
		}
		internal static bool WriteCallingMethod;
		private static MessageTracer _messageTracer;
		
		internal static void SetTraceSettings(short maxTypeId, TraceSettings settings)
		{
			if(_messageTracer == null)
			{
				_messageTracer = new MessageTracer(maxTypeId, settings);
			}
			else
			{
				_messageTracer.ReloadConfig(maxTypeId,settings);
			}
		}

		internal static void WriteDebugInfo(RelayMessage message, SimpleLinkedList<Node> destinations)
		{
			if (WriteMessageTrace && message != null)
			{
				StringBuilder debugString = new StringBuilder();
				StackTrace stack = null;
				if (WriteCallingMethod)
				{
					stack = new StackTrace(2,true);
				}
				debugString.Append("Relay Forwarding: ");
				debugString.Append(message.ToString());
				debugString.Append(Environment.NewLine);
				debugString.Append("    sending " + DescribeDestinations(destinations));
				if (stack != null)
				{
					debugString.Append(Environment.NewLine);
				    debugString.Append("    called ");
                    debugString.Append(stack.ToString());
				}
				
				_messageTracer.WriteLogMessage(message.MessageType,message.TypeId,debugString.ToString());
			}
		}

		private static string DescribeDestinations(SimpleLinkedList<Node> destinations)
		{
			if (destinations == null || destinations.Count == 0)
			{
				return "nowhere";
			}

			StringBuilder sb = new StringBuilder();
			sb.Append("to ");
			sb.Append(destinations.Count);
			sb.Append(" nodes:");
			List<Node> nodes = destinations.PeekAll();

			foreach (Node node in nodes)
			{
				sb.Append(Environment.NewLine);
				sb.Append("     *");
				sb.Append(node.NodeGroup.GroupName);
				sb.Append(" ");
				sb.Append(node.Host);
				sb.Append(":");
				sb.Append(node.Port);
			}
			return sb.ToString();
		}



		

		
	}
}
