using System;

namespace MySpace.Common
{
	/// <summary>
	///		<para>An object that represents the return value of a
	///		method indicating either a success or failure.</para>
	/// </summary>
	/// <typeparam name="TValue">
	///		<para>The type of value that is returned if the method call is a success.</para>
	/// </typeparam>
	/// <typeparam name="TError">
	///		<para>The type of error message that is returned if the method call failed.</para>
	/// </typeparam>
	public sealed class MethodResult<TValue, TError>
	{
		/// <summary>
		/// 	<para>Creates an instance of <see cref="MethodResult{TValue, TError}"/>
		///		that represents the return value of a successful method call.</para>
		/// </summary>
		/// <param name="value">
		/// 	<para>The return value of the successful method call.</para>
		/// </param>
		/// <returns>
		/// 	<para>A <see cref="MethodResult{TValue, TError}"/> that represents
		///		the specifeid return value of a successful method call; never <see langword="null"/>.</para>
		/// </returns>
		public static MethodResult<TValue, TError> CreateSuccess(TValue value)
		{
			return new MethodResult<TValue, TError>(true, value, default(TError));
		}

		/// <summary>
		/// 	<para>Creates an instance of <see cref="MethodResult{TValue, TError}"/>
		///		that represents the return value of a failed method call.</para>
		/// </summary>
		/// <param name="error">
		/// 	<para>The return value of the failed method call.</para>
		/// </param>
		/// <returns>
		/// 	<para>A <see cref="MethodResult{TValue, TError}"/> that represents
		///		the specified return value of a failed method call; never <see langword="null"/>.</para>
		/// </returns>
		public static MethodResult<TValue, TError> CreateFailure(TError error)
		{
			return new MethodResult<TValue, TError>(false, default(TValue), error);
		}

		private MethodResult(bool isSuccess, TValue value, TError error)
		{
			this.isSuccess = isSuccess;

			if (isSuccess)
			{
				this.value = value;
			}
			else
			{
				this.error = error;
			}
		}

		/// <summary>
		/// 	<para>Gets the return value of the method call if it was successful.</para>
		/// </summary>
		/// <value>
		/// 	<para>A value of type <typeparamref name="TValue"/>, that provides the
		/// 	return value of a successful method call.</para>
		/// </value>
		/// <exception cref="InvalidOperationException">
		///		<para>This instance represents the result of a failed method call.</para>
		/// </exception>
		public TValue Value
		{
			[System.Diagnostics.DebuggerStepThrough]
			get
			{
				if (isSuccess == false)
				{
					throw new InvalidOperationException("Only accessible on a successful result.");
				}
				return value;
			}
		}
		private TValue value = default(TValue);

		/// <summary>
		/// 	<para>Gets the return value of the method call if it failed.</para>
		/// </summary>
		/// <value>
		/// 	<para>A value of type <typeparamref name="TValue"/>, that provides the
		/// 	return value of a failed method call.</para>
		/// </value>
		/// <exception cref="InvalidOperationException">
		///		<para>This instance represents the result of a successful method call.</para>
		/// </exception>
		public TError Error
		{
			[System.Diagnostics.DebuggerStepThrough]
			get
			{
				if (isSuccess == true)
				{
					throw new InvalidOperationException("Only accessible on a successful result.");
				}
				return error;
			}
		}
		private TError error = default(TError);

		///	<summary>
		///		<para>Gets whether this instance represents a successful result.</para>
		/// </summary>
		///	<value>
		///		<para><see langword="true"/> if this instance represents a successful
		///		result; otherwise, <see langword="false"/>.</para>
		///	</value>
		public bool IsSuccess
		{
			[System.Diagnostics.DebuggerStepThrough]
			get
			{
				return isSuccess;
			}
		}
		private bool isSuccess = false;

		/// <summary>
		/// 	<para>Implicitly converts an object of type 
		///		<typeparamref name="TError"/> to a <see cref="MethodResult{TValue,TError}"/>
		///		representing an error.</para>
		/// </summary>
		/// <param name="error">
		/// 	<para>The error object to implicitly convert to a result object.</para>
		/// </param>
		/// <returns>
		/// 	<para>A <see cref="MethodResult{TValue,TError}"/> representing an error
		///		and encapsulating the specified error object; <see langword="null"/> if
		///		<paramref name="error"/> is <see langword="null"/>.</para>
		/// </returns>
		public static implicit operator MethodResult<TValue, TError>(TError error)
		{
			return CreateFailure(error);
		}

		/// <summary>
		/// 	<para>Implicitly converts an object of type 
		///		<typeparamref name="TValue"/> to a <see cref="MethodResult{TValue,TError}"/>
		///		representing a success.</para>
		/// </summary>
		/// <param name="value">
		/// 	<para>The error object to implicitly convert to a result object.</para>
		/// </param>
		/// <returns>
		/// 	<para>A <see cref="MethodResult{TValue,TError}"/> representing a success
		///		and encapsulating the specified value object; <see langword="null"/> if
		///		<paramref name="value"/> is <see langword="null"/>.</para>
		/// </returns>
		public static implicit operator MethodResult<TValue, TError>(TValue value)
		{
			return CreateSuccess(value);
		}
	}
}