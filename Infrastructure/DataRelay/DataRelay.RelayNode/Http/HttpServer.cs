

using System.Net;
using System.IO;
using System;
using System.Text;
namespace MySpace.DataRelay.Http
{
	internal enum ServerCommand : byte
	{
		Empty,
		Status,
		Get,
		FavIcon
	}
	
	class HttpServer
	{

		private static readonly byte[] emptyResponse = new byte[0];
		private static readonly Logging.LogWrapper log = new Logging.LogWrapper();        
		private static byte[] favIconResponse;
		
		static ServerCommand[] serverCommandLookupTable;
		static readonly Encoding encoding = new UTF8Encoding(false);
		static readonly MySpace.Common.IO.Compressor compressor = MySpace.Common.IO.Compressor.GetInstance();
		static readonly int compressionThreshold = 10240;

		const string serverName = "MySpace-DataRelay";
		const HttpResponseHeader serverHeader = HttpResponseHeader.Server;
		
		private readonly HttpListener _listener;
		private string _listenPrefix;

		private readonly RelayNode _node;

		private readonly AsyncCallback _getContextCallback;
		
		static HttpServer()
		{
			InitializeFavIcon();
			InitializeCommandLookup();
		}
		
		internal HttpServer(int listenPort, RelayNode node)
		{
			_getContextCallback = GetContextCallback;
			_listenPrefix = GetListenPrefix(listenPort);
			_listener = new HttpListener();
			_listener.Prefixes.Add(_listenPrefix);
			_node = node;
		}

		internal void Start()
		{
			log.InfoFormat("Starting http listener on {0}", _listenPrefix);
			_listener.Start();
			_listener.BeginGetContext(_getContextCallback, _listener);
		}

		internal void Stop()
		{
			log.InfoFormat("Stopping http listener on {0}", _listenPrefix);
			_listener.Stop();
		}

		internal void ChangePort(int newListenPort)
		{
			lock (_listenPrefix)
			{
				try
				{
					string newPrefix = GetListenPrefix(newListenPort);
					if (newPrefix != _listenPrefix)
					{
						_listener.Prefixes.Add(newPrefix);
						_listener.Prefixes.Remove(_listenPrefix);
						_listenPrefix = newPrefix;
					}
				}
				catch (Exception e)
				{
					log.ErrorFormat("Exception changing listen port to {0}: {1}",newListenPort,e );
				}
			}
		}
		
		private static string GetListenPrefix(int listenPort)
		{
			return string.Format("http://*:{0}/", listenPort);
		}
		void GetContextCallback(IAsyncResult ar)
		{
			HttpListener contextListener = ar.AsyncState as HttpListener;
			HttpListenerContext context = null;
			if (contextListener == null)
			{
				return;
			}

			try
			{
				context = contextListener.EndGetContext(ar);
			}
			catch (HttpListenerException hle)
			{
				if (hle.ErrorCode != 995) //="The I/O operation has been aborted because of either a thread exit or an application request" aka we stopped the listener
				{
					log.Error("Exception ending get context: {0}", hle);
				}

			}
			catch (Exception e)
			{
				log.Error("Exception ending get context: {0}", e);
			}

			if (contextListener.IsListening)
			{
				contextListener.BeginGetContext(_getContextCallback, contextListener);
			}

			if (context != null)
			{
				ProcessListenerContext(context);
			}

		}

		private void ProcessListenerContext(HttpListenerContext context)
		{
			byte[] responseBytes = null;
			int responseCode = 0;

			RelayMessage requestMessage;

			HttpListenerRequest request = context.Request;
			HttpListenerResponse response = context.Response;
			bool responseCompressed = false;
			string responseType, commandParam;
			try
			{	
				ServerCommand command;
				string verb = request.HttpMethod;
				if (verb == "GET")
				{
					ExtractRequestParameters(request.RawUrl, out command, out commandParam, out requestMessage, out responseType);

					switch (command)
					{
						case ServerCommand.Status:
							ProcessStatusRequest(commandParam, out responseCode, out responseBytes, out responseCompressed);
							break;
						case ServerCommand.FavIcon:
							ProcessFavIconRequest(out responseCode, out responseBytes);
							break;
						case ServerCommand.Get:
							ProcessGetRequest(requestMessage, out responseCode, out responseBytes);
							break;
						case ServerCommand.Empty:
							ProcessEmptyRequest(out responseCode, out responseBytes);
							break;
					}
				}
			}
			catch(InvalidRequestException)
			{
				log.ErrorFormat("Invalid request {0} from {1}", context.Request.RawUrl, context.Request.RemoteEndPoint);
				responseBytes = encoding.GetBytes("Invalid request");
				responseCode = 400; 
			}
			catch (Exception e)
			{
				log.ErrorFormat("Exception encountered while processing request {0} from {1}: {2}", context.Request.RawUrl,
								context.Request.RemoteEndPoint, e);

				responseBytes = encoding.GetBytes(string.Format("Error processing request: {0}", e.Message));
				responseCode = 500;
			}
			finally
			{
				response.Headers.Add(serverHeader, serverName);
				response.StatusCode = responseCode;
				if (responseBytes != null)
				{
					if (responseCompressed)
					{
						response.Headers.Add(HttpResponseHeader.ContentEncoding, "gzip");
					}

					response.ContentLength64 = responseBytes.Length;
					response.OutputStream.Write(responseBytes, 0, responseBytes.Length);

				}
				else
				{
					response.ContentLength64 = 0;
				}
				context.Response.Close();
			}
		}

		private void ProcessGetRequest(RelayMessage message, out int responseCode, out byte[] responseBytes)
		{
		
			if(message == null)
			{
				responseCode = 400;
				responseBytes = emptyResponse;
				return;
			}
			
			_node.HandleMessage(message);

			if(message.Payload == null)
			{
				responseCode = 404;
				responseBytes = encoding.GetBytes(string.Format("Object {0}:{1} not found", message.TypeId, message.Id));
				return;
			}

			responseCode = 200;
			responseBytes = GetPayloadRepresentation(message.Payload);
			
			return;
		}

		private static byte[] GetPayloadRepresentation(RelayPayload relayPayload)
		{
			if(relayPayload == null || relayPayload.ByteArray == null)
			{
				return emptyResponse;
			}

			return encoding.GetBytes(BitConverter.ToString(relayPayload.ByteArray));
		}

		private void ProcessStatusRequest(string commandParam, out int responseCode, out byte[] responseBytes, out bool responseCompressed)
		{
			string status;
			if(string.IsNullOrEmpty(commandParam))
			{	
				responseBytes = encoding.GetBytes(_node.GetComponentsDescription());
				responseCompressed = false;
				responseCode = 400;
				return;
			}

			ComponentRuntimeInfo componentStatus = _node.GetComponentRuntimeInfo(commandParam);
			if(componentStatus != null)
			{
				status = componentStatus.GetRuntimeInfoAsString() ?? String.Empty;
			}
			else
			{
				status = String.Format("No status found for component '{0}'", commandParam);
			}


			responseBytes = encoding.GetBytes(status);

			if (responseBytes.Length > compressionThreshold)
			{
				responseBytes = compressor.Compress(responseBytes, true, MySpace.Common.IO.CompressionImplementation.ManagedZLib);
				responseCompressed = true;
			}
			else
			{
				responseCompressed = false;
			}

			responseCode = 200;
		}

		private static void ProcessEmptyRequest(out int responseCode, out byte[] responseBytes)
		{
			responseCode = 200;
			responseBytes = emptyResponse;
		}

		private static void ProcessFavIconRequest(out int responseCode, out byte[] responseBytes)
		{
			responseCode = 200;
			responseBytes = favIconResponse;
		}


		private static void ExtractRequestParameters(string requestUri,
			out ServerCommand command, 
			out string commandParam,
			out RelayMessage requestMessage,
			out string responseType)
		{
			responseType = "";
			//for now sticking with manual parsing because we have a limited command set. 
			//if the command set gets much bigger we can switch to just using HttpUtility parsing and a name/value collection.
			char[] queryDelimeters = { '?', '=' }; //. is a hack to get favicon to work with enum parsing
			// /get/{typeid}/{objectid}
			// /status/{componentName}
			string[] querySplit = requestUri.Split(queryDelimeters); //0=command string, then 1/2 = name/value etc
			string[] slashSplit = querySplit[0].Split('/');
			short typeId;
			int objectId;
			requestMessage = null;

			if (slashSplit.Length < 2)
			{
				throw new InvalidRequestException("Request Uri Invalid - " + requestUri);
			}

			//0- empty
			//1- command
			//2- typeId OR commandParam1
			//3- objectId
			string commandString = slashSplit[1];//.ToLowerInvariant();
			commandParam = String.Empty;
			if (commandString.Length == 0)
			{
				command = ServerCommand.Empty;
			}
			else
			{
				if (commandString[0] < serverCommandLookupTable.Length)
				{
					command = serverCommandLookupTable[commandString[0]];
					switch (command)
					{
						case ServerCommand.Status:
							if (slashSplit.Length > 2)
							{
								commandParam = slashSplit[2];
							}
							break;
						case ServerCommand.Get:
							if (slashSplit.Length > 3)
							{
								short.TryParse(slashSplit[2], out typeId);
								int.TryParse(slashSplit[3], out objectId);
								requestMessage = new RelayMessage(typeId, objectId, MessageType.Get);
							}
							break;
					}
				}
				else
				{
					throw new InvalidRequestException("Request Uri Invalid - " + requestUri);
				}
			}

		}

		private static void InitializeCommandLookup()
		{
			string[] commandStrings = {"get", "status", "favicon.ico" };
			int maxIndex = 0;
			foreach (string commandString in commandStrings)
			{
				if ((commandString[0]) > maxIndex)
				{
					maxIndex = commandString[0];
				}
				if ((commandString.ToUpperInvariant()[0]) > maxIndex)
				{
					maxIndex = commandString.ToUpperInvariant()[0];
				}
			}
			serverCommandLookupTable = new ServerCommand[maxIndex + 1];
			serverCommandLookupTable[(int)'s'] = ServerCommand.Status;
			serverCommandLookupTable[(int)'S'] = ServerCommand.Status;
			serverCommandLookupTable[(int)'F'] = ServerCommand.FavIcon;
			serverCommandLookupTable[(int)'f'] = ServerCommand.FavIcon;
			serverCommandLookupTable[(int)'g'] = ServerCommand.Get;
			serverCommandLookupTable[(int)'G'] = ServerCommand.Get;

		}

		private static void InitializeFavIcon()
		{
			try
			{
				favIconResponse = File.ReadAllBytes("favicon.ico");
			}
			catch (Exception e)
			{
				log.ErrorFormat("Exception reading favicon file: {0}", e);
				favIconResponse = emptyResponse;
			}
		}
	}

	public class InvalidRequestException : Exception
	{
		public InvalidRequestException(string message) : base(message)
		{
		}
	}
}
