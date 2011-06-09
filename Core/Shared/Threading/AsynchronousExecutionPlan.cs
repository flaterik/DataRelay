using System.Collections.Generic;

namespace MySpace.Common.Threading
{
	/// <summary>
	/// A delegate type that represents the asynchronous code to be executed by this operation. You write a delegate
	/// of this type and pass it to 
	/// <see cref="Operation.Start(AsynchronousExecutionPlan,System.Action{MySpace.Common.Threading.Operation})"/> 
	/// or <see cref="Operation.Start(AsynchronousExecutionPlan,System.Action{MySpace.Common.Threading.Operation},System.Action,System.Action)"/> 
	/// to begin the framework for the asynchronous operation.
	/// <code>IEnumerable{Operation} MyExecutionPlan(Operation operation) { yield break; }</code>
	/// </summary>
	/// <param name="parentOperation">The controlling, parent operation for the execution plan.  <see cref="Operation"/> objects returned from this delegate 
	///		must be created from <see cref="Operation.Create(System.Action{System.Action},System.TimeSpan)"/> or <see cref="Operation.Create(System.IAsyncResult,System.TimeSpan)"/>.</param>
	/// <returns>Returns an <see cref="IEnumerable{T}"/> of <see cref="Operation"/>, neither can be <see langword="null"/>.  Your delegate should yield return <see cref="Operation"/> objects.  If there are no <see cref="Operation"/> objects to return, <c>yield break</c>.</returns>
	public delegate IEnumerable<Operation> AsynchronousExecutionPlan(Operation parentOperation);
}