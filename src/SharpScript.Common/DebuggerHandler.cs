namespace SharpScript
{
	#region Using Directives

	using System;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;

	#endregion

	[SuppressMessage("", "CA1812", Justification = "Created via reflection using CreateInstanceFromAndUnwrap.")]
	internal sealed class DebuggerHandler : CrossAppDomainObject
	{
		#region Public Properties

		[SuppressMessage(
			"Microsoft.Performance",
			"CA1822:MarkMembersAsStatic",
			Justification = "This must be instance level to work correctly with MarshalByRefObject when marshaling across AppDomains.")]
		public bool IsAttached => Debugger.IsAttached;

		#endregion

		#region Public Methods

		// These methods must be instance-level to work correctly with MarshalByRefObject when marshaling across AppDomains.
#pragma warning disable CA1822 // Use static method
#pragma warning disable CC0091 // Use static method
		public void Break() => Debugger.Break();

		public bool Attach() => Debugger.Launch();
#pragma warning restore CC0091 // Use static method
#pragma warning restore CA1822 // Use static method

		#endregion
	}
}