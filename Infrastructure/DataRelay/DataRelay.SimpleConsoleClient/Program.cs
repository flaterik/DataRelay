using System.Threading;
using MySpace.DataRelay;
using MySpace.DataRelay.Client;
using MySpace.Common.Framework;
using System;

namespace MySpace.DataRelay.SimpleConsoleClient
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("Initializing...");
			RelayClient.Instance.GetHtmlStatus(); //just to kick the instance and get it warmed up
			PrintUsage();
			string entry;
			do
			{
				entry = Console.ReadLine();
				if(string.IsNullOrEmpty(entry))
					break;
				string[] entryArgs = entry.Split(new [] {' '}, StringSplitOptions.RemoveEmptyEntries);
				if (entryArgs.Length <= 1)
				{
					PrintUsage();
					break;
				}


				string command = entryArgs[0].ToLowerInvariant();
				string key = entryArgs[1];
				switch (command)
				{

					case "save":
						if (entryArgs.Length < 3)
							PrintUsage();
						else
							Save(key, entryArgs[2]);
						break;
					case "get":
						string value;
						if (TryGet(key, out value))
							Console.WriteLine(value);
						else
							Console.WriteLine("Not found");
						break;
					case "delete":
						Delete(key);
						break;
					default:
						PrintUsage();
						break;
				}
			} while (true);
			
			
			
			
		}

		private static void PrintUsage()
		{
			Console.WriteLine("Usage: [Get {id}] | [Save {id} {value}] | [Delete {id}] [Empty Command Exits]");
		}

		static void Save(string key, string value)
		{
			GenericStorage<string> item = new GenericStorage<string>(key, value);
			RelayClient.Instance.SaveObject(item);
		}

		static bool TryGet(string key, out string value)
		{
			GenericStorage<string> emptyItem = new GenericStorage<string>(key);
			RelayClient.Instance.GetObject(emptyItem);
			if (emptyItem.DataSource == DataSource.Cache)
			{
				value = emptyItem.Payload;
				return true;
			}
			value = String.Empty;
			return false;
		}

		static void Delete(string key)
		{
			GenericStorage<string> emptyItem = new GenericStorage<string>(key);
			RelayClient.Instance.DeleteObject(emptyItem);
		}
	}
}
