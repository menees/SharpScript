namespace SharpScript
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;

	#endregion

	internal sealed class ScriptVbProvider : ScriptTypeProvider
	{
		#region Public Constants

		public static readonly IEnumerable<string> GlobalImports = new[]
		{
			"Microsoft.VisualBasic",
			"System",
			"System.Collections",
			"System.Diagnostics",
			"System.Data",
			"System.Linq",
			"System.Xml.Linq",
			nameof(SharpScript),
		};

		#endregion

		#region Constructors

		internal ScriptVbProvider()
			: base(new string[] { ".svb", ".vb" }, "'", false, ScriptType.VB)
		{
		}

		#endregion

		#region Public Properties

		public override IEnumerable<string> SpecialReferences => new[] { "Microsoft.VisualBasic.dll" };

		public override string DebugCompileOptions
			=> "/define:TRACE=True,DEBUG=True /optionexplicit+ /optionstrict+ /optioninfer+ /imports:" + string.Join(",", GlobalImports);

		public override string ReleaseCompileOptions
			=> "/define:TRACE=True /optionexplicit+ /optionstrict+ /optioninfer+ /optimize /removeintchecks+ /imports:" + string.Join(",", GlobalImports);

		#endregion
	}
}
