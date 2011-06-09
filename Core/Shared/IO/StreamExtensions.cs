using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using MySpace.Common.Dynamic;
using MySpace.Common.Dynamic.Reflection;
using MySpace.Logging;

namespace MySpace.Common.IO
{
	/// <summary>
	/// Encapsulates extension methods for <see cref="Stream"/> types.
	/// </summary>
	public static class StreamExtensions
	{
		private static readonly Factory<MemoryStream, bool> _isPubliclyVisible;

		static StreamExtensions()
		{
			var field = typeof(MemoryStream).GetField("_exposable", BindingFlags.NonPublic | BindingFlags.Instance);

			Debug.Assert(field != null, "Expected field was not found.");

			if (field == null)
			{
				_isPubliclyVisible = m => false;
			}
			else
			{
				var dm = new DynamicMethod(Guid.NewGuid().ToString("N"), typeof(bool), new[] { typeof(MemoryStream) }, true);
				var il = dm.GetILGenerator();
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Ldfld, field);
				il.Emit(OpCodes.Ret);
				_isPubliclyVisible = (Factory<MemoryStream, bool>)dm.CreateDelegate(typeof(Factory<MemoryStream, bool>));
			}
		}

		/// <summary>
		/// Reads the specified number of bytes from <paramref name="stream"/>.
		/// </summary>
		/// <param name="stream">The stream to read from.</param>
		/// <param name="count">The the number of bytes to read.</param>
		/// <returns>
		///		<para>A byte[] containing the bytes read from the stream. The size may be less than count if the stream contains fewer bytes than requested.</para>
		/// </returns>
		public static byte[] Read(this Stream stream, int count)
		{
			var result = new byte[count];

			int bytesRead = 0;
			while (true)
			{
				bytesRead += stream.Read(result, bytesRead, count);
				if (bytesRead == count) return result;
				if (bytesRead <= 0)
				{
					var newResult = new byte[bytesRead];
					Buffer.BlockCopy(result, 0, newResult, 0, bytesRead);
					return newResult;
				}
			}
		}

		/// <summary>
		/// Writes the specified buffer to stream.
		/// </summary>
		/// <param name="stream">The stream to write to.</param>
		/// <param name="buffer">The buffer to read from.</param>
		public static void Write(this Stream stream, byte[] buffer)
		{
			ArgumentAssert.IsNotNull(stream, "stream");

			stream.Write(buffer, 0, buffer.Length);
		}

		/// <summary>
		/// Converts <paramref name="stream"/> into a seek-able stream.
		/// Returns <paramref name="stream"/> if <paramref name="stream"/> is already seek-able.
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <returns>A seek-able stream.</returns>
		public static Stream ToSeekable(this Stream stream)
		{
			if (stream.CanSeek) return stream;
			var data = stream.ReadToEnd();

			return new MemoryStream(data, 0, data.Length, stream.CanWrite, true);
		}

		/// <summary>
		/// Reads the entire contents of the stream into a <see cref="Byte"/>[].
		/// </summary>
		/// <param name="stream">The stream to read from.</param>
		/// <returns>The entire contents of the stream as a <see cref="Byte"/>[].</returns>
		public static byte[] ReadToEnd(this Stream stream)
		{
			if (stream.CanSeek)
			{
				int start = 0;
				int count = (int)(stream.Length - stream.Position);
				var result = new byte[count];
				while (count > 0)
				{
					var read = stream.Read(result, start, count);
					count -= read;
					start += read;
				}
				return result;
			}
			var ms = new MemoryStream();
			stream.PipeTo(ms);
			return ms.ToArray();
		}

		/// <summary>
		/// Reads the entire contents of the stream into a <see cref="Byte"/>[] asynchronously.
		/// </summary>
		/// <param name="stream">The stream to read from.</param>
		/// <returns>A future of the entire contents of the stream as a <see cref="Byte"/>[].</returns>
		public static Future<byte[]> ReadToEndAsync(this Stream stream)
		{
			if (!stream.CanReadAsync())
			{
				try
				{
					return stream.ReadToEnd();
				}
				catch (Exception ex)
				{
					return ex;
				}
			}

			var result = new Future<byte[]>();

			var output = new MemoryStream();

			var pipeResult = stream.PipeToAsync(output);
			pipeResult.OnComplete(() =>
			{
				if (pipeResult.HasResult)
				{
					result.SetResult(output.ToArray());
				}
				else if (pipeResult.HasError)
				{
					result.SetError(pipeResult.Error);
				}
				else
				{
					result.SetError(new Exception("An underlying future was canceled unexpectedly."));
				}
			});


			return result;
		}

		/// <summary>
		/// Pipes the contents of <paramref name="input"/> to <paramref name="output"/>.
		/// </summary>
		/// <param name="input">The input stream.</param>
		/// <param name="output">The output stream.</param>
		public static void PipeTo(this Stream input, Stream output)
		{
			var ms = input as MemoryStream;

			if (ms != null && ms.CanSeek)
			{
				var buffer = ms.ForcedGetBuffer();
				int start = (int)ms.Position;
				int count = (int)(ms.Length - ms.Position);
				ms.Seek(count, SeekOrigin.Current);
				output.Write(buffer, start, count);
			}
			else
			{
				const int maxBufferSize = 1 << 10;
				var targetLength = input.CanSeek ? input.Length : maxBufferSize;
				var buffer = new byte[targetLength > maxBufferSize ? maxBufferSize : targetLength];
				for (int read = input.Read(buffer, 0, buffer.Length);
					read > 0;
					read = input.Read(buffer, 0, buffer.Length))
				{
					output.Write(buffer, 0, read);
				}
			}
		}

		/// <summary>
		/// Pipes the contents of <paramref name="input"/> to <paramref name="output"/> asynchronously.
		/// </summary>
		/// <param name="input">The input stream.</param>
		/// <param name="output">The output stream.</param>
		/// <param name="buffer">The buffer to use when piping.</param>
		/// <returns>A future that completes when the piping is done.</returns>
		public static Future PipeToAsync(this Stream input, Stream output, byte[] buffer = null)
		{
			if (buffer == null) buffer = new byte[1 << 10];
			return input.PipeToIterator(output, buffer).ExecuteSequentially(true);
		}

		private static IEnumerable<Future> PipeToIterator(this Stream input, Stream output, byte[] buffer)
		{
			bool readAsync = input.CanReadAsync();
			bool writeAsync = output.CanWriteAsync();

			while (true)
			{
				int read;
				if (readAsync)
				{
					var readFuture = Future.FromAsyncPattern<int>(
						ac => input.BeginRead(buffer, 0, buffer.Length, ac, null),
						input.EndRead);
					yield return readFuture;
					read = readFuture.Result;
				}
				else
				{
					read = input.Read(buffer, 0, buffer.Length);
				}

				if (read == 0) yield break;

				if (writeAsync)
				{
					yield return Future.FromAsyncPattern(
						ac => output.BeginWrite(buffer, 0, read, ac, null),
						ar =>
						{
							output.EndWrite(ar);
							return 0;
						});
				}
				else
				{
					output.Write(buffer, 0, read);
				}
			}
		}

		private static readonly KeyedLazyInitializer<Type, bool> _canReadAsync = new KeyedLazyInitializer<Type, bool>(type =>
			type.GetMethod("BeginRead").DeclaringType != typeof(Stream));

		/// <summary>
		/// Determines whether this stream implements asynchronous reading.
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <returns>
		/// 	<see langword="true"/> if this stream implements asynchronous reading; otherwise, <see langword="false"/>.
		/// </returns>
		public static bool CanReadAsync(this Stream stream)
		{
			ArgumentAssert.IsNotNull(stream, "stream");

			return _canReadAsync[stream.GetType()];
		}

		private static readonly KeyedLazyInitializer<Type, bool> _canWriteAsync = new KeyedLazyInitializer<Type, bool>(type =>
			type.GetMethod("BeginWrite").DeclaringType != typeof(Stream));

		/// <summary>
		/// Determines whether this stream implements asynchronous writing.
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <returns>
		/// 	<see langword="true"/> if this stream implements asynchronous writing; otherwise, <see langword="false"/>.
		/// </returns>
		public static bool CanWriteAsync(this Stream stream)
		{
			ArgumentAssert.IsNotNull(stream, "stream");

			return _canWriteAsync[stream.GetType()];
		}

		private static readonly LazyInitializer<Func<MemoryStream, byte[]>> _forcedGetBuffer = new LazyInitializer<Func<MemoryStream, byte[]>>(() =>
		{
			var field = typeof(MemoryStream).GetField("_buffer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

			if (field == null)
			{
				Trace.TraceWarning(string.Format("Couldn't find MemoryStream._buffer. {0}.ForcedGetBuffer ought to be re-factored.", typeof(StreamExtensions)));
				return ms => ms.ToArray();
			}

			var dm = new DynamicMethod("MemoryStream_ForcedGetBuffer", typeof(byte[]), new[] { typeof(MemoryStream) }, typeof(MemoryStream), true);

			var g = new MethodGenerator(new MsilWriter(dm));

			var target = g.GetParameter(0);

			g.Load(target);
			g.LoadField(field);
			g.Return();

			return (Func<MemoryStream, byte[]>)dm.CreateDelegate(typeof(Func<MemoryStream, byte[]>));
		});

		/// <summary>
		/// Returns the underlying buffer inside <paramref name="stream"/>.
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <returns>The underlying buffer inside <paramref name="stream"/>.</returns>
		public static byte[] ForcedGetBuffer(this MemoryStream stream)
		{
			return _forcedGetBuffer.Value(stream);
		}

		/// <summary>
		/// Writes an <see cref="Int32"/> in a format that allows smaller values to occupy less space.
		/// The trade off is that bigger values occupy more space (as much as 5 bytes).
		/// Use <see cref="WriteVarInt32"/> and <see cref="IPrimitiveReader.ReadVarInt32"/> instead of
		/// <see cref="BinaryWriter.Write(Int32)"/> and <see cref="BinaryReader.ReadInt32"/> when you expect
		/// <paramref name="value"/> to be less than 134,217,728 or greater than -134,217,728 more
		/// often than not.
		/// </summary>
		/// <param name="stream">The <see cref="Stream"/> to write to.</param>
		/// <param name="value">The <see cref="Int32"/> value to write.</param>
		/// <remarks>
		/// Exact space consumption.
		/// -63 to 63 - 1 byte
		/// -8,192 to 8,192 - 2 bytes
		/// -1,048,576 to 1,048,576 - 3 bytes
		/// -134,217,728 to 134,217,728 - 4 bytes
		/// Everything else - 5 bytes
		/// </remarks>
		public static void WriteVarInt32(this Stream stream, int value)
		{
			unchecked
			{
				uint val = (uint)(value < 0 ? -value : value);

				byte part = (byte)
				(
					(val >= 0x40u ? 0x80u : 0u)
					| (value < 0 ? 0x40u : 0u)
					| (val & 0x3Fu)
				);
				stream.WriteByte(part);
				val >>= 6;

				if (val > 0)
				{
					while (val >= 0x80u)
					{
						part = (byte)(0x80u | val);
						stream.WriteByte(part);
						val >>= 7;
					}
					stream.WriteByte((byte)val);
				}
			}
		}

		/// <summary>
		/// Reads an <see cref="Int32"/> in a format that allows smaller values to occupy less space
		/// (as little as 1 byte). The trade off is that bigger values occupy more space (as much as 5 bytes).
		/// Use <see cref="WriteVarInt32"/> and <see cref="ReadVarInt32"/> instead of
		/// <see cref="BinaryWriter.Write(Int32)"/> and <see cref="BinaryReader.ReadInt32"/> when you expect
		/// the value to be less than 134,217,728 or greater than -134,217,728 more
		/// often than not.
		/// </summary>
		/// <param name="stream">The <see cref="Stream"/> to write to.</param>
		/// <returns>The de-serialized <see cref="Int32"/> value.</returns>
		/// <remarks>
		/// Exact space consumption.
		/// -63 to 63										1 byte
		/// -8,192 to 8,192							2 bytes
		/// -1,048,576 to 1,048,576			3 bytes
		/// -134,217,728 to 134,217,728	4 bytes
		/// Everything else							5 bytes
		/// </remarks>
		public static int ReadVarInt32(this Stream stream)
		{
			unchecked
			{
				int part = stream.ReadByte();
				if (part == -1)
				{
					throw new InvalidDataException("Unexpected end of stream");
				}
				bool negative = (part & 0x40) == 0x40;
				uint value = (byte)part & 0x3FU;
				int bitsRead = 6;

				while ((part & 0x80) == 0x80)
				{
					if (bitsRead > 0x20)
					{
						throw new InvalidDataException("Invalid VarInt32");
					}
					part = stream.ReadByte();
					value |= ((byte)part & 0x7FU) << bitsRead;
					bitsRead += 7;
				}
				return negative ? -(int)value : (int)value;
			}
		}

		/// <summary>
		/// Determines whether or not <see cref="MemoryStream.GetBuffer"/> can be called without an exception.
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <returns>
		/// 	<c>true</c> if <see cref="MemoryStream.GetBuffer"/> can be called; otherwise, <c>false</c>.
		/// </returns>
		public static bool IsPubliclyVisible(this MemoryStream stream)
		{
			return _isPubliclyVisible(stream);
		}

		/// <summary>
		/// Writes the stream contents to a byte array, regardless of the <see cref="Stream.Position"/> property.
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <returns>A new byte array.</returns>
		/// <exception cref="ArgumentNullException">
		///		<para><paramref name="stream"/> is <see langword="null"/>.</para>
		/// </exception>
		public static byte[] ToArray(this Stream stream)
		{
			if (stream == null) throw new ArgumentNullException("stream");

			var ms = stream as MemoryStream;

			if (ms != null) return ms.ToArray();

			if (stream.CanSeek)
			{
				var result = new byte[stream.Length];
				var currentPosition = stream.Position;
				stream.Seek(0, SeekOrigin.Begin);
				stream.Read(result, 0, (int)stream.Length);
				stream.Position = currentPosition;
				return result;
			}

			throw new ArgumentException("stream must be able to seek", "stream");
		}
	}
}
