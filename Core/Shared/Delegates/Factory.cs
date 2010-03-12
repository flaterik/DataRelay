using System;

namespace MySpace.Common
{
	/// <summary>
	///	<para>Encapsulates a method that has no parameters and returns an <see cref="System.Object"/>.</para>
	/// </summary>
	public delegate object Factory();

	/// <summary>
	///	<para>Encapsulates a method that has no parameters and returns
	///	a value of the type specified by the <typeparam name="TResult"/> parameter.</para>
	/// </summary>
	/// <typeparam name="TResult">
	///	<para>The type of the return value of the method that this delegate encapsulates.</para>
	/// </typeparam>
	public delegate TResult Factory<TResult>();

	/// <summary>
	///	<para>Encapsulates a method that has one parameter and returns
	///	a value of the type specified by the <typeparam name="TResult"/> parameter.</para>
	/// </summary>
	/// <typeparam name="T">
	///	<para>The type of the parameter of the method that this delegate encapsulates.</para>
	/// </typeparam>
	/// <typeparam name="TResult">
	///	<para>The type of the return value of the method that this delegate encapsulates.</para>
	/// </typeparam>
	/// <param name="arg">
	///	<para>The parameter of the method that this delegate encapsulates. </para>
	/// </param>
	public delegate TResult Factory<T, TResult>(T arg);

	/// <summary>
	///	<para>Encapsulates a method that has two parameters and returns
	///	a value of the type specified by the <typeparam name="TResult"/> parameter.</para>
	/// </summary>
	/// <typeparam name="T1">
	///	<para>The type of the first parameter of the method that this delegate encapsulates.</para>
	/// </typeparam>
	/// <typeparam name="T2">
	///	<para>The type of the second parameter of the method that this delegate encapsulates.</para>
	/// </typeparam>
	/// <typeparam name="TResult">
	///	<para>The type of the return value of the method that this delegate encapsulates.</para>
	/// </typeparam>
	/// <param name="arg1">
	///	<para>The first parameter of the method that this delegate encapsulates.</para>
	/// </param>
	/// <param name="arg2">
	///	<para>The second parameter of the method that this delegate encapsulates.</para>
	/// </param>
	public delegate TResult Factory<T1, T2, TResult>(T1 arg1, T2 arg2);
}
