using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MySpace.Common.IO;
using MySpace.Common;
using MySpace.Logging;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// Implement persistent error queues.  When persistent error queues are 
	/// enabled, the errored messages, in the form of <see cref="SerializedRelayMessage"/>
	/// objects, are spilled to disk as they are added to the error queue.  Messages
	/// are read from disk as they are dequeued.  The in-memory queues are kept
	/// as empty as possible.
	/// </summary>
	internal class ErrorQueuePersistence
	{
		/// <summary>
		/// Initializes persistent error queues.
		/// </summary>
		/// <param name="rootFolder">The root folder for persistent error queue storage.</param>
		/// <param name="nodeName">Name of the node.  Used as a sub-folder name, for this node, under rootFolder.</param>
		/// <param name="itemsPerFile">The number of messages per file.</param>
		/// <param name="maxFileSize">The maximum file size in bytes.</param>
		/// <param name="globalMaxMb">The maximum aggregate files sizes in MB.  Zero is unlimited.</param>
		/// <param name="getMessages">A delegate that will be called to retrieve messages from the <see cref="ErrorQueue"/>.</param>
		/// <param name="probeMessages">A delegate that will be called to determine if the <see cref="ErrorQueue"/> has any
		/// messages waiting to be persisted.</param>
		/// <returns>An <see cref="ErrorQueuePersistence"/> object if persistence is enabled.  Returns <see langword="null"/>
		/// if persistence is disabled or the storage folder is inaccessable.</returns>
		public static ErrorQueuePersistence Initialize(
			string rootFolder,
			string nodeName,
			int itemsPerFile,
			int maxFileSize,
			int globalMaxMb,
			Func<SerializedRelayMessage[]> getMessages,
			Func<bool> probeMessages)
		{
			if (string.IsNullOrEmpty(rootFolder))
				return null;
			
			if (nodeName == null)
				throw new ArgumentNullException("nodeName");
			if (nodeName == string.Empty)
				throw new ArgumentException("Node name may not be empty.", "nodeName");
			if (itemsPerFile <= 0)
				throw new ArgumentException("Items per file must be greater than zero.", "itemsPerFile");
			if (maxFileSize <= 0)
				throw new ArgumentException("Max file size must be greater than zero.", "maxFileSize");
			if (globalMaxMb < 0)
				throw new ArgumentException("Global Max MB must be non-negative", "globalMaxMB");
			if (getMessages == null)
				throw new ArgumentNullException("getMessages");
			if (probeMessages == null)
				throw new ArgumentNullException("probeMessages");

			try
			{
				var spillFolder = Path.Combine(rootFolder, nodeName);

				var peristence = new ErrorQueuePersistence(spillFolder, nodeName, itemsPerFile, maxFileSize, getMessages, probeMessages);

				GlobalMaxBytes = globalMaxMb > 0 ? globalMaxMb * 1024L * 1024L : long.MaxValue;

				_log.DebugFormat("Persistent Error Queue Enabled for {0}:  itemsPerFile={1}, maxFileSize={2}, maxTotalBytes={3}",
					nodeName, itemsPerFile, maxFileSize, GlobalMaxBytes);

				return peristence;
			}
			catch (Exception e)
			{
				_log.Error("Exception initializing " + nodeName, e);
				return null;
			}
		}

		/// <summary>
		/// Gets or sets the maximum aggregate size, in bytes, of all error queue files.
		/// </summary>
		/// <value>The global max bytes.</value>
		public static long GlobalMaxBytes { get { return _globalMaxBytes; } set { _globalMaxBytes = value; } }

		/// <summary>
		/// Gets the approximate number of bytes occupied by error queue files.  (Does not take allocation
		/// units into consideration.)
		/// </summary>
		/// <value>The aggregate byte count of all error queue files.</value>
		public static long GlobalFileBytes { get { return _globalFileSize; } }

		private static int GetFileSequence(string path)
		{
			int seq;
			if (int.TryParse(Path.GetFileNameWithoutExtension(path), out seq))
				return seq;
			return -1;
		}

		private static FileHeader GetFileHeader(string path)
		{
			using (var file = File.OpenRead(path))
			{
				return GetFileHeader(file);
			}
		}

		private static FileHeader GetFileHeader(Stream stream)
		{
			stream.Position = 0;
			var header = Serializer.Deserialize<FileHeader>(stream);
			header.Length = stream.Length;
			stream.Position = header.Position;

			return header;
		}

		private static void WriteFileHeader(Stream stream, FileHeader header)
		{
			stream.Position = 0;
			Serializer.Serialize(stream, header);

			if (stream.Position > header.Position)
				throw new FormatException("The Position value in the FileHeader is not past the end of the header.");

			stream.Position = header.Position;
		}

		private ErrorQueuePersistence(
			string spillFolder,
			string nodeName,
			int itemsPerFile,
			int maxFileSize,
			Func<SerializedRelayMessage[]> getMessages,
			Func<bool> probeMessages)
		{

			_spillFolder = spillFolder;
			_nodeName = nodeName;
			_itemsPerFile = itemsPerFile;
			_maxFileSize = maxFileSize;
			_getMessages = getMessages;
			_probeMessages = probeMessages;

			if (Directory.Exists(_spillFolder))
			{
				// Delete spill folder if it is empty.
				try
				{
					Directory.Delete(_spillFolder);
				}
// ReSharper disable EmptyGeneralCatchClause
				catch (Exception)
// ReSharper restore EmptyGeneralCatchClause
				{
				}

				if (Directory.Exists(_spillFolder))
				{
					_spillInProgress = true;
					ThreadPool.UnsafeQueueUserWorkItem(CountMessages, null);
				}
			}
		}

		private void CountMessages(object obj)
		{
			try
			{
				ResetGlobalFileSizeForNode(_nodeName);

				var pattern = string.Format(_spillFilenameFormat, "*");
				var seqList = from fp in Directory.GetFiles(_spillFolder, pattern)
					            select new {Path = fp, Seq = GetFileSequence(fp)};
				var fileList = seqList.OrderBy(f => f.Seq).ToArray();

				foreach (var file in fileList)
				{
					if (file.Seq < 0)
					{
						_log.WarnFormat("Encountered spill file with malformed name: {0}", file.Path);
					}
					else
					{
						var header = GetFileHeader(file.Path);
						IncrementGlobalFileSize(_nodeName, header.Length);
						Interlocked.Add(ref _messageCount, header.MessageCount);
						NodeManager.Instance.Counters.IncrementErrorQueueBy(header.MessageCount);

						lock (_spillFiles)
						{
							_spillFiles.Enqueue(file.Path);
							_spillFileSequence = file.Seq;
						}
					}
				}
			}
			catch (Exception e)
			{
				_log.Error("Exception counting messages for " + _nodeName, e);
			}
			finally
			{
				lock (_spillLock)
				{
					_spillInProgress = false;
					StartSpill();
				}
			}
		}

		/// <summary>
		/// Creates the spill folder for this persistent error queue.
		/// </summary>
		/// <returns><see langword="true"/> if the folder exists or was successfully created; otherwise <see langword="false"/>.</returns>
		public bool CreateSpillFolder()
		{
			try
			{
				lock (_spillFiles)
				{
					if (Directory.Exists(_spillFolder))
						return true;
					Directory.CreateDirectory(_spillFolder);
					return true;
				}
			}
			catch (Exception e)
			{
				_log.Error("Exception creating spill folder: " + _spillFolder, e);
			}

			return false;
		}

		/// <summary>
		/// Gets the count of persisted messages.
		/// </summary>
		/// <value>The message count.</value>
		public int MessageCount { get { return _messageCount; } }

		/// <summary>
		/// Requests that a spill of queued messages from the <see cref="ErrorQueue"/> to persistent storage
		/// be started.  If a spill is already in progress or the task has been queued no action is taken.
		/// </summary>
		public void StartSpill()
		{
			lock (_spillLock)
			{
				if (!_spillInProgress)
				{
					ThreadPool.UnsafeQueueUserWorkItem(RunSpill, null);
					_spillInProgress = true;
				}
			}
		}

		private void RunSpill(object obj)
		{
			Stream spillFile = null;
			FileHeader header = null;
			long bytesWritten = 0;
			long initialLength = 0;

			try
			{
				while (true)
				{
					var messages = _getMessages();
					if (messages == null || messages.Length == 0) break;

					foreach (var message in messages)
					{
						if (spillFile == null) lock (_spillFiles)
						{
							if (_currentSpillFilePath == null)
							{
								_currentSpillFilePath = Path.Combine(_spillFolder,
								                                 string.Format(_spillFilenameFormat, (++_spillFileSequence).ToString("d8")));
								header = new FileHeader();
								spillFile = File.Open(_currentSpillFilePath, FileMode.CreateNew, FileAccess.ReadWrite);
								WriteFileHeader(spillFile, header);
								_spillFiles.Enqueue(_currentSpillFilePath);
								initialLength = 0;
							}
							else
							{
								spillFile = File.Open(_currentSpillFilePath, FileMode.Open, FileAccess.ReadWrite);
								header = GetFileHeader(spillFile);
								spillFile.Position = spillFile.Length;
								initialLength = spillFile.Length;
							}
						}

						Serializer.Serialize(spillFile, message);
						++header.MessageCount;
						Interlocked.Increment(ref _messageCount);

						if (header.MessageCount >= _itemsPerFile || spillFile.Position >= _maxFileSize) lock (_spillFiles)
						{
							bytesWritten += spillFile.Length - initialLength;
							_currentSpillFilePath = null;
							WriteFileHeader(spillFile, header);
							spillFile.Close();
							spillFile = null;
						}
					}
				}

				if (spillFile != null)
				{
					bytesWritten += spillFile.Length - initialLength;
					WriteFileHeader(spillFile, header);
					spillFile.Close();
					spillFile = null;
				}

				if (IncrementGlobalFileSize(_nodeName, bytesWritten))
					DiscardOneFile();
			}
			catch (Exception e)
			{
				_log.Error(e);

				if (spillFile != null) lock (_spillFiles)
				{
					var path = _currentSpillFilePath;
					_currentSpillFilePath = null;
					spillFile.Dispose();

					if (path != null) try
					{
						var newName = path.Replace(_spillFileExtension, ".error");
						File.Delete(newName);
						File.Move(path, newName);
					}
					catch (Exception e2)
					{
						_log.Error("Exception renaming " + path, e2);
					}
				}
			}
			finally
			{
				lock (_spillLock)
				{
					if (_probeMessages())
					{
						// New messages appeared between the time we detected the empty
						// queue above and now.
						ThreadPool.UnsafeQueueUserWorkItem(RunSpill, null);
					}
					else
					{
						_spillInProgress = false;
						Monitor.PulseAll(_spillLock);
					}
				}
			}
		}

		private void DiscardOneFile()
		{
			string path;

			lock (_spillFiles)
			{
				if (_spillFiles.Count == 0) return;
				path = _spillFiles.Peek();
				if (path == _currentSpillFilePath) return;
				_spillFiles.Dequeue();
			}

			try
			{
				using (var file = File.Open(path, FileMode.Open, FileAccess.Read))
				{
					var header = GetFileHeader(file);

					_log.WarnFormat("Discarding Error Queue file containing {0} messages because maximum disk space exceeded: {1}",
									header.MessageCount, path);

					Interlocked.Add(ref _messageCount, -header.MessageCount);
					NodeManager.Instance.Counters.IncrementErrorQueueBy(-header.MessageCount);
					IncrementDiscardCount(header.MessageCount);
					IncrementGlobalFileSize(_nodeName, -file.Length);

					for (int i = 0; i < header.MessageCount; ++i)
					{
						var message = Serializer.Deserialize<SerializedRelayMessage>(file);
						Forwarder.RaiseMessageDropped(message);
					}
				}
			}
			catch (Exception e)
			{
				_log.Error(e);
			}
			finally
			{
				File.Delete(path);
			}
		}

		/// <summary>
		/// Waits for any in-progress spill operation to complete.
		/// </summary>
		public void WaitForSpill()
		{
			lock (_spillLock)
			{
				while (_spillInProgress)
				{
					Monitor.Wait(_spillLock);
				}
			}
		}

		/// <summary>
		/// Dequeues persisted messages into the supplied <see cref="SerializedMessageList"/>.
		/// </summary>
		/// <remarks>
		/// Each message file holds, at most, the number of messages specfied by the ItemsPerDequeue
		/// QueueConfig" parameter.  One file is deserialized per <see cref="Dequeue"/> call, so the
		/// number of messages returned will not exceed ItemsPerDequeue.
		/// </remarks>
		/// <param name="messages">A <see cref="SerializedMessageList"/> into which the messages are stored.</param>
		public void Dequeue(SerializedMessageList messages)
		{
			string path;

			lock (_spillFiles)
			{
				// This should never happen.
				if (_spillFiles.Count == 0) return;

				path = _spillFiles.Peek();
				if (path == _currentSpillFilePath) lock (_spillLock)
				{
					// We'll deadlock if we try to wait for the spill to complete, so bail-out.
					if (_spillInProgress) return;

					_currentSpillFilePath = null;
				}

				_spillFiles.Dequeue();
			}

			try
			{
				using (var file = File.Open(path, FileMode.Open, FileAccess.Read))
				{
					var header = GetFileHeader(file);
					IncrementGlobalFileSize(_nodeName, -file.Length);

					for (int i = 0; i < header.MessageCount; ++i)
					{
						var message = Serializer.Deserialize<SerializedRelayMessage>(file);
						messages.Add(message);
					}

					Interlocked.Add(ref _messageCount, -header.MessageCount);
				}

				File.Delete(path);
			}
			catch (Exception e)
			{
				_log.Error("Exception deserializing " + path, e);
				
				if (File.Exists(path))
				{
					try
					{
						string newName = path.Replace(_spillFileExtension, ".error");
						File.Delete(newName);
						File.Move(path, newName);
					}
					catch (IOException ioex)
					{
						_log.Error("Exception renaming " + path, ioex);
					}
				}
				
				throw;
			}
		}

		/// <summary>
		/// Gets count of files for this node.
		/// </summary>
		/// <value>The file count.</value>
		public int FileCount { get { lock (_spillFiles) return _spillFiles.Count; } }

		private static bool IncrementGlobalFileSize(string node, long increment)
		{
			lock (_fileSizeByNode)
			{
				long curSize;
				if (_fileSizeByNode.TryGetValue(node, out curSize))
				{
					_fileSizeByNode[node] = curSize + increment;
				}
				else
				{
					_fileSizeByNode.Add(node, increment);
				}
			}

			var byteCount = Interlocked.Add(ref _globalFileSize, increment);
			NodeManager.Instance.Counters.SetPersistentErrorQueueBytes(byteCount);
			return byteCount > GlobalMaxBytes;
		}

		private static void ResetGlobalFileSizeForNode(string node)
		{
			lock (_fileSizeByNode)
			{
				long nodeSize;
				if (_fileSizeByNode.TryGetValue(node, out nodeSize))
				{
					_fileSizeByNode.Remove(node);
					NodeManager.Instance.Counters.SetPersistentErrorQueueBytes(Interlocked.Add(ref _globalFileSize, -nodeSize));
				}
			}
		}

		private void IncrementDiscardCount(int count)
		{
			Interlocked.Add(ref _discardCount, count);
			NodeManager.Instance.Counters.IncrementErrorQueueDiscardsBy(count);
		}

		public int DiscardCount { get { return _discardCount; } }

		[SerializableClass]
		private class FileHeader
		{
			[SerializableProperty(1)]
			public int MessageCount;
			[SerializableProperty(1)]
			public long Position = 64;

			public long Length;
		}

		private static readonly LogWrapper _log = new LogWrapper();

		private readonly object _spillLock = new object();
		private bool _spillInProgress;

		private readonly string _spillFolder;
		private readonly string _nodeName;
		private readonly int _itemsPerFile;
		private readonly int _maxFileSize;
		private readonly Func<SerializedRelayMessage[]> _getMessages;
		private readonly Func<bool> _probeMessages;

		private const string _spillFileExtension = ".spill";
		private const string _spillFilenameFormat = "{0}" + _spillFileExtension;
	
		private readonly Queue<string> _spillFiles = new Queue<string>();
		private int _spillFileSequence;
		private string _currentSpillFilePath;
		
		private int _messageCount;
		private int _discardCount;

		private static long _globalFileSize;
		private static readonly Dictionary<string, long> _fileSizeByNode = new Dictionary<string, long>();
		private static long _globalMaxBytes = long.MaxValue;
	}
}
