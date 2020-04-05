namespace SharpScript
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Runtime.Serialization;
	using System.Text;

	#endregion

	[Serializable]
	public sealed class ScriptException : Exception
	{
		#region Constructors

		public ScriptException()
			: base()
		{
		}

		public ScriptException(string message)
			: base(message)
		{
		}

		public ScriptException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

		private ScriptException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}

		#endregion
	}
}
