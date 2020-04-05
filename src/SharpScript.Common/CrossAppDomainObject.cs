namespace SharpScript
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;

	#endregion

	public abstract class CrossAppDomainObject : MarshalByRefObject
	{
		#region Public Methods

		public override object InitializeLifetimeService() => null;

		#endregion
	}
}
