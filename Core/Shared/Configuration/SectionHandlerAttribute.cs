using System;

namespace MySpace.Common.Configuration
{
	/// <summary>
	///		<para>An attribute that must be used on a descendent of
	///		<see cref="BaseSectionHandler{TImplementation,TConfigType}"/>
	///		in order to indicate the name of the section.</para>
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public sealed class SectionHandlerAttribute : Attribute
	{
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="SectionHandlerAttribute"/> class.</para>
		/// </summary>
		/// <param name="name">
		/// 	<para>The name of this configuration section within the config file.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="name"/> is <see langword="null"/>.</para>
		/// </exception>
		public SectionHandlerAttribute(string name)
		{
			if (name == null) throw new ArgumentNullException("name");

			Name = name;
		}

		///	<summary>
		///		<para>Gets the name of this configuration section within the config file.</para>
		/// </summary>
		///	<value>
		///		<para>A <see cref="string"/> indicating the name of this configuration section;
		///		never <see langword="null"/>.</para>
		///	</value>
		public string Name { get; private set; }
	}
}