namespace SharpScript
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.IO;

	#endregion

	[Serializable]
	internal sealed class ScriptParameters
	{
		#region Private Data Members

		private string[]? arguments;
		private string? fileName;
		private bool inConsole;
		private bool quiet;
		private TimeSpan timeout;
		private string? sharpScriptDirectory;

		#endregion

		#region Public Properties

		public string[] Arguments => this.arguments ?? Array.Empty<string>();

		public string FileName => this.fileName ?? string.Empty;

		// This is internally settable because it can be read from the
		// command line or from a #debug directive in the script.
		public bool Debug { get; internal set; }

		public bool InConsole => this.inConsole;

		public bool Quiet => this.quiet;

		public TimeSpan Timeout => this.timeout;

		public string SharpScriptDirectory => this.sharpScriptDirectory ?? string.Empty;

		public string ScriptName => Path.GetFileNameWithoutExtension(this.FileName);

		#endregion

		#region Public Methods

		public bool Initialize(string[] appArgs, bool inConsole, string sharpScriptDirectory)
		{
			bool result = false;

			// If they didn't pass any arguments, then quit early.
			if (appArgs != null)
			{
				int numAppArgs = appArgs.Length;
				if (numAppArgs > 0)
				{
					this.inConsole = inConsole;
					this.sharpScriptDirectory = sharpScriptDirectory;

					// Process the arguments.  Anything that begins with "//" must be
					// one of our arguments.  The first argument that doesn't begin
					// with "//" or "/" will be considered the script file name.
					bool validArgs = true;
					List<string> lstArgs = new();
					for (int i = 0; i < numAppArgs; i++)
					{
						string arg = appArgs[i];

						if (arg.StartsWith("//"))
						{
							// Note: If you support more options here, make sure you
							// add it to the "Usage" resource string.
							arg = arg.ToUpper();
							const string TimeoutPrefix = "//T:";
							if (arg == "//D")
							{
								this.Debug = true;
							}
							else if (arg == "//Q")
							{
								this.quiet = true;
							}
							else if (arg.StartsWith(TimeoutPrefix))
							{
								string seconds = arg.Substring(TimeoutPrefix.Length);
								this.timeout = TimeSpan.FromSeconds(GetInt(seconds));
							}
							else
							{
								// It's an unknown SharpScript option.
								validArgs = false;
								break;
							}
						}
						else if (this.fileName == null && !arg.StartsWith("/"))
						{
							// Always expand the file name to an absolute file name.  In
							// ScriptApplication and ScriptHandler we depend on having a
							// fully qualified script file name.  ScriptApplication needs
							// it so it can set a full path for AppDomain.ApplicationBase.
							// If it only sets a relative path, things don't load correctly.
							// ScriptHandler needs it so it can expand relative assembly
							// references within scripts.
							this.fileName = Path.GetFullPath(arg);
						}
						else
						{
							lstArgs.Add(arg);
						}
					}

					this.arguments = lstArgs.ToArray();

					// We must have a script file name to be considered initialized.
					result = validArgs && this.fileName != null;
				}
			}

			return result;
		}

		#endregion

		#region Private Methods

		private static int GetInt(string value)
		{
			if (!int.TryParse(value, out int result))
			{
				result = 0;
			}

			return result;
		}

		#endregion
	}
}
