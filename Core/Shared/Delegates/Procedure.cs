using System;

namespace MySpace.Common
{
	/// <summary>
	///	<para>Encapsulates a method that has two parameters and does not return a value.</para>
	/// </summary>
	/// <typeparam name="T">
	///	<para>The type of parameter of the method that this delegate encapsulates.</para>
	/// </typeparam>
	/// <param name="arg">
	///	<para>The parameter of the method that this delegate encapsulates.</para>
	/// </param>
	public delegate void Procedure<T>(T arg);

	/// <summary>
	///	<para>Encapsulates a method that has two parameters and does not return a value.</para>
	/// </summary>
	/// <typeparam name="T1">
	///	<para>The type of the first parameter of the method that this delegate encapsulates.</para>
	/// </typeparam>
	/// <typeparam name="T2">
	///	<para>The type of the second parameter of the method that this delegate encapsulates.</para>
	/// </typeparam>
	/// <param name="arg1">
	///	<para>The first parameter of the method that this delegate encapsulates.</para>
	/// </param>
	/// <param name="arg2">
	///	<para>The second parameter of the method that this delegate encapsulates.</para>
	/// </param>
	public delegate void Procedure<T1, T2>(T1 arg1, T2 arg2);

	/// <summary>
	///	<para>Encapsulates a method that has three parameters and does not return a value.</para>
	/// </summary>
	/// <typeparam name="T1">
	///	<para>The type of the first parameter of the method that this delegate encapsulates.</para>
	/// </typeparam>
	/// <typeparam name="T2">
	///	<para>The type of the second parameter of the method that this delegate encapsulates.</para>
	/// </typeparam>
	/// <typeparam name="T3">
	///	<para>The type of the third parameter of the method that this delegate encapsulates.</para>
	/// </typeparam>
	/// <param name="arg1">
	///	<para>The first parameter of the method that this delegate encapsulates.</para>
	/// </param>
	/// <param name="arg2">
	///	<para>The second parameter of the method that this delegate encapsulates.</para>
	/// </param>
	/// <param name="arg3">
	///	<para>The third parameter of the method that this delegate encapsulates.</para>
	/// </param>
	public delegate void Procedure<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3);
}
