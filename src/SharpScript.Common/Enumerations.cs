namespace SharpScript
{
	#region public ScriptType

	public enum ScriptType
	{
		CSharp,
		VB,
	}

	#endregion

	#region internal CompilerType

	internal enum CompilerType
	{
		CodeDom,
		Roslyn,
	}

	#endregion

	#region internal ExitCode

	internal enum ExitCode
	{
		Success = 0,
		InvalidArg = 1,
		Exception = 2,
		Cancelled = 3,
		UnhandledException = 4,
	}

	#endregion
}
