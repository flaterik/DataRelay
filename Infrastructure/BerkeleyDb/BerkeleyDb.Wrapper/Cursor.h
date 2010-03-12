#pragma once
#include "Stdafx.h"
#include "Database.h"

using namespace System;

namespace BerkeleyDbWrapper
{
	///<summary>
	///Holds the lengths of key and value in a Berkeley Db entry.
	///</summary>
	public value class Lengths
	{
	public:
		///<summary>
		///Gets the key length.
		///</summary>
		///<value>
		///The <see cref="Int32" /> length of the key.
		///</value>
		property int KeyLength { int get() { return _keyLength; } }
		///<summary>
		///Gets the value length.
		///</summary>
		///<value>
		///The <see cref="Int32" /> value of the key.
		///</value>
		property int ValueLength { int get() { return _valueLength; } }
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="Lengths"/> structure.</para>
		/// </summary>
		/// <param name="keyLength">
		/// 	<para>The <see cref="Int32" /> length of the key.</para>
		/// </param>
		/// <param name="valueLength">
		/// 	<para>The <see cref="Int32" /> value of the key.</para>
		/// </param>
		Lengths(int keyLength, int valueLength) : _keyLength(keyLength), _valueLength(valueLength) {}
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="Lengths"/> structure.</para>
		/// </summary>
		/// <param name="key">
		/// 	<para>The <see cref="DataBuffer" /> key.</para>
		/// </param>
		/// <param name="value">
		/// 	<para>The <see cref="DataBuffer" /> key.</para>
		/// </param>
		Lengths(DataBuffer key, DataBuffer value) : _keyLength(key.ByteLength), _valueLength(value.ByteLength) {}
		/// <summary>
		/// 	<para>Constant the represents an entry not found.</para>
		/// </summary>		
		literal int NotFound = -1;
		/// <summary>
		/// 	<para>Constant the represents an entry deleted.</para>
		/// </summary>		
		literal int Deleted = -2;
		/// <summary>
		/// 	<para>Constant the represents an entry created but not populated.</para>
		/// </summary>		
		literal int KeyExists = -3;
	private:
		int _keyLength;
		int _valueLength;
	};

	///<summary>
	///Holds the key and value streams of a Berkeley Db entry.
	///</summary>
	public value class Streams
	{
	public:
		///<summary>
		///Gets the key stream.
		///</summary>
		///<value>
		///The key <see cref="Stream" />.
		///</value>
		property Stream^ KeyStream { Stream^ get() { return _keyStream; } }
		///<summary>
		///Gets the value stream.
		///</summary>
		///<value>
		///The value <see cref="Stream" />.
		///</value>
		property Stream^ ValueStream { Stream^ get() { return _valueStream; } }
		///<summary>
		///Gets the return code associated with the operation.
		///</summary>
		///<value>
		///The <see cref="Int32" /> return code.
		///</value>
		property int ReturnCode { int get() { return _returnCode; } }
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="Streams"/> structure.</para>
		/// </summary>
		/// <param name="keyStream">
		/// 	<para>The key <see cref="Stream" />.</para>
		/// </param>
		/// <param name="valueStream">
		/// 	<para>The value <see cref="Stream" />.</para>
		/// </param>
		/// <param name="returnCode">
		/// 	<para>The <see cref="Int32" /> return code associated with the
		///		operation.</para>
		/// </param>
		Streams(Stream^ keyStream, Stream^ valueStream, int returnCode) :
			_keyStream(keyStream), _valueStream(valueStream), _returnCode(returnCode) {}
	private:
		Stream^ _keyStream;
		Stream^ _valueStream;
		int _returnCode;
	};

	///<summary>
	///Wrapper around a Berkeley Db cursor.
	///</summary>
	[SuppressUnmanagedCodeSecurity()]
	public ref class Cursor
	{
	public:
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="Cursor"/> structure.</para>
		/// </summary>
		/// <param name="db">
		/// 	<para>The <see cref="Database" /> that this cursor will iterate over.</para>
		/// </param>
		Cursor(Database ^db);
		/// <summary>
		/// 	<para>Disposes of this <see cref="Cursor"/> structure.</para>
		/// </summary>
		~Cursor();
		/// <summary>
		/// 	<para>Finalizes this <see cref="Cursor"/> structure.</para>
		/// </summary>
		!Cursor();
		/// <summary>
		/// 	<para>Reads a cursor entry into user supplied buffers.</para>
		/// </summary>
		/// <param name="key">
		/// 	<para>The key <see cref="DataBuffer" />. Will be read for exact or wildcard
		///		searches. Will be written for all exact searches.</para>
		/// </param>
		/// <param name="value">
		/// 	<para>The value <see cref="DataBuffer" /> written to.</para>
		/// </param>
		/// <param name="offset">
		/// 	<para>The offset within the entry value to begin copying data.</para>
		/// </param>
		/// <param name="position">
		/// 	<para>The <see cref="CursorPosition"/> specifying the position at
		///		which to read.</para>
		/// </param>
		/// <param name="flags">
		/// 	<para>The <see cref="GetOpFlags"/> specifying the read options.</para>
		/// </param>
		/// <returns>
		///		<para>The <see cref="Lengths"/> specifying the lengths of the key
		///		and value entries. These can be greater than the lengths of the data
		///		read if the data buffers are too small. Negative values match the
		///		conditional literals in <see cref="Lengths"/>.</para>
		/// </returns>
		Lengths Get(DataBuffer key, DataBuffer value, int offset,
			CursorPosition position, GetOpFlags flags);
		/// <summary>
		/// 	<para>Reads a cursor entry into streams.</para>
		/// </summary>
		/// <param name="key">
		/// 	<para>The key <see cref="DataBuffer" />. Will be read for exact or wildcard
		///		searches. Will be written for all exact searches.</para>
		/// </param>
		/// <param name="value">
		/// 	<para>The value <see cref="DataBuffer" /> written to.</para>
		/// </param>
		/// <param name="offset">
		/// 	<para>The offset within the entry value to begin copying data.</para>
		/// </param>
		/// <param name="position">
		/// 	<para>The <see cref="CursorPosition"/> specifying the position at
		///		which to read.</para>
		/// </param>
		/// <param name="length">
		/// 	<para>The length of the segment within the entry value to copying data.
		///		Use a negative value to read to the end.</para>
		/// </param>
		/// <param name="flags">
		/// 	<para>The <see cref="GetOpFlags"/> specifying the read options.</para>
		/// </param>
		/// <returns>
		///		<para>The <see cref="Streams"/> holding the key and value
		///		<see cref="Stream"/>s, as well as operation return code. The
		///		return code will match on of the conditional literals
		///		in <see cref="Lengths"/>.</para>
		/// </returns>
		Streams Get(DataBuffer key, int offset, int length,
			CursorPosition position, GetOpFlags flags);
		/// <summary>
		/// 	<para>Writes a cursor entry.</para>
		/// </summary>
		/// <param name="key">
		/// 	<para>The key <see cref="DataBuffer" /> to write.</para>
		/// </param>
		/// <param name="value">
		/// 	<para>The value <see cref="DataBuffer" /> to write.</para>
		/// </param>
		/// <param name="offset">
		/// 	<para>The offset within the entry value to begin writing data.</para>
		/// </param>
		/// <param name="length">
		/// 	<para>The length of the segment within the entry value to write data.
		///		Use a negative value to read to the end.</para>
		/// </param>
		/// <param name="position">
		/// 	<para>The <see cref="CursorPosition"/> specifying the position at
		///		which to write.</para>
		/// </param>
		/// <param name="flags">
		/// 	<para>The <see cref="PutOpFlags"/> specifying the write options.</para>
		/// </param>
		/// <returns>
		///		<para>The <see cref="Lengths"/> specifying the lengths of the key
		///		and value written. Negative values match the conditional literals
		///		in <see cref="Lengths"/>.</para>
		/// </returns>
		Lengths Put(DataBuffer key, DataBuffer value, int offset, int length,
			CursorPosition position, PutOpFlags flags);
		/// <summary>
		/// 	<para>Deletes the current cursor entry.</para>
		/// </summary>
		/// <param name="flags">
		/// 	<para>The <see cref="DeleteOpFlags"/> specifying the write options.</para>
		/// </param>
		/// <returns>
		///		<para>Whether or not there was a current entry to be deleted.</para>
		/// </returns>
		bool Delete(DeleteOpFlags flags);

	private:
		typedef int (*BdbCall)(Dbc *, Dbt *, Dbt *, int);
		Database^ _db;
		Dbc *_cursorp;
		int DeadlockLoop(String ^methodName, Dbt *key, Dbt *data, int options,
			BdbCall bdbCall);
		static const int intDeadlockValue = static_cast<int>(DbRetVal::LOCK_DEADLOCK);
		static const int intMemorySmallValue = static_cast<int>(DbRetVal::BUFFER_SMALL);
	};
}