namespace SharpScript
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Text;

	#endregion

	internal sealed class ScriptCSharpProvider : ScriptTypeProvider
	{
		#region Constructors

		internal ScriptCSharpProvider()
			: base(new string[] { ".scs", ".cs" }, "//", true, ScriptType.CSharp)
		{
		}

		#endregion

		#region Public Properties

		public override IEnumerable<string> SpecialReferences => new[] { "Microsoft.CSharp.dll" };

		public override string DebugCompileOptions => "/define:TRACE;DEBUG /checked";

		public override string ReleaseCompileOptions => "/define:TRACE /optimize";

		#endregion
	}
}
