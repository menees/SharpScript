namespace SharpScript
{
	#region Using Directives

	using System;
	using System.Diagnostics.CodeAnalysis;
	using System.IO;
	using System.Text;
	using System.Threading;
	using System.Windows.Forms;
	using Menees;
	using Menees.Windows.Forms;

	#endregion

	public static class Script
	{
		#region Private Data Members

		private static bool initialized;
		private static ScriptParameters? scriptParameters;
		private static DateTime startTime;
		private static System.Threading.Timer? timeoutTimer;

		#endregion

		#region Public Properties

		public static bool IsExecuting => initialized;

		public static string FileName
		{
			get
			{
				RequireInitialized();
				return scriptParameters!.FileName;
			}
		}

		public static bool Debug
		{
			get
			{
				RequireInitialized();
				return scriptParameters!.Debug;
			}
		}

		public static string Name
		{
			get
			{
				RequireInitialized();
				return scriptParameters!.ScriptName;
			}
		}

		public static bool InConsole
		{
			get
			{
				RequireInitialized();
				return scriptParameters!.InConsole;
			}
		}

		public static DateTime StartTime
		{
			get
			{
				RequireInitialized();
				return startTime;
			}
		}

		public static TimeSpan Timeout
		{
			get
			{
				RequireInitialized();
				return scriptParameters!.Timeout;
			}
		}

		public static bool Quiet
		{
			get
			{
				RequireInitialized();
				return scriptParameters!.Quiet;
			}
		}

		public static string SharpScriptDirectory
		{
			get
			{
				RequireInitialized();
				return scriptParameters!.SharpScriptDirectory;
			}
		}

		#endregion

		#region Public Methods

		public static string[] GetArguments()
		{
			RequireInitialized();
			return (string[]?)scriptParameters?.Arguments.Clone() ?? Array.Empty<string>();
		}

		public static void Echo(params object[] values)
		{
			RequireInitialized();

			if (InConsole)
			{
				foreach (object value in values)
				{
					Console.WriteLine(value);
				}
			}
			else
			{
				StringBuilder sb = new();

				foreach (object value in values)
				{
					if (sb.Length > 0)
					{
						sb.Append("\r\n");
					}

					sb.Append(value);
				}

				string message = sb.ToString();
				MsgBox(message);
			}
		}

		public static void Cancel()
		{
			RequireInitialized();

			// NOTE: This method needs to be equivalent to ScriptApplication.CancelScriptExecution().
			// Since they're running in different objects in different AppDomains, I'm sort of
			// duplicating this functionality.
			//
			// NOTE 2: The docs for Unload say that CannotUnloadAppDomainException will never be thrown
			// to this thread.  So if Unload returns to this thread, then we'll just exit the process.
			//
			// NOTE 3: I'm not able to clean up any temporary output assembly here because this AppDomain
			// still has it loaded.
			AppDomain.Unload(AppDomain.CurrentDomain);
			Environment.Exit((int)ExitCode.Cancelled);
		}

		public static void Sleep(int milliseconds)
		{
			RequireInitialized();
			Thread.Sleep(milliseconds);
		}

		public static DialogResult MsgBox(string prompt) => MsgBox(prompt, MessageBoxButtons.OK);

		public static DialogResult MsgBox(string prompt, MessageBoxButtons buttons) => MsgBox(prompt, buttons, Name);

		public static DialogResult MsgBox(string prompt, MessageBoxButtons buttons, string caption) => MsgBox(prompt, buttons, MessageBoxIcon.None, caption);

		public static DialogResult MsgBox(string prompt, MessageBoxButtons buttons, MessageBoxIcon icon, string caption)
		{
			RequireInitialized();
			return MessageBox.Show(prompt, caption, buttons, icon);
		}

		public static string? InputBox(string prompt) => InputBox(prompt, Name, string.Empty);

		public static string? InputBox(string prompt, string caption) => InputBox(prompt, caption, string.Empty);

		public static string? InputBox(string prompt, string caption, string defaultValue)
			=> WindowsUtility.ShowInputBox(null, prompt, caption, defaultValue, null, null);

		#endregion

		#region Internal Methods

		internal static void Initialize(ScriptParameters parameters)
		{
			scriptParameters = parameters;
			startTime = DateTime.UtcNow.ToLocalTime();

			// Setup the timeout timer if necessary.
			if (scriptParameters.Timeout != TimeSpan.Zero)
			{
				timeoutTimer = new System.Threading.Timer(
					new TimerCallback(TimeoutTimer_Callback),
					null,
					scriptParameters.Timeout,
					new TimeSpan(0, 0, 0, 0, -1));
			}

			if (scriptParameters.InConsole)
			{
				ApplicationInfo.Initialize(scriptParameters.ScriptName);
			}
			else
			{
				WindowsUtility.InitializeApplication(scriptParameters.ScriptName, null);
			}

			initialized = true;
		}

		#endregion

		#region Private Methods

		private static void TimeoutTimer_Callback(object value)
		{
			// We don't need to get any more notifications after this one.
			timeoutTimer?.Dispose();

			// If we get here, then the script timed out, so Cancel it.
			// It would be nice to return a special exit code for timeout,
			// but that would take too much work since cancelling in most
			// cases will unload this AppDomain and abort the current thread.
			Cancel();
		}

		private static void RequireInitialized()
		{
			if (!initialized)
			{
				throw new InvalidOperationException("The Script class can only be used during the execution of a SharpScript.");
			}
		}

		#endregion
	}
}
