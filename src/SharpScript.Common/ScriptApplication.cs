namespace SharpScript
{
	#region Using Directives

	using System;
	using System.Diagnostics.CodeAnalysis;
	using System.IO;
	using System.Reflection;
	using System.Runtime.Remoting;
	using System.Text;
	using Menees;

	#endregion

	public abstract class ScriptApplication : CrossAppDomainObject
	{
		#region Private Data Members

		private readonly object monitor = new();
		private AppDomain? executionDomain;
		private ScriptParameters? parameters;
		private string[]? outputFiles;
		private ScriptType scriptType;
		private DebuggerHandler? debugger;

		#endregion

		#region Private Types

		private delegate int ExecuteScriptHandler();

		#endregion

		#region Protected Properties

		protected static string ApplicationName => Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);

		protected static string SharpScriptDirectory => Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

		protected string ScriptName => Path.GetFileName(this.parameters?.FileName ?? string.Empty);

		protected ScriptType ScriptType => this.scriptType;

		protected bool Debug => this.parameters?.Debug ?? false;

		protected bool IsDebuggerAttached => this.CurrentDebugger?.IsAttached ?? false;

		protected bool Quiet => this.parameters?.Quiet ?? false;

		#endregion

		#region Protected Abstract Properties

		protected abstract bool InConsole { get; }

		#endregion

		#region Private Properties

		private DebuggerHandler? CurrentDebugger
		{
			get
			{
				lock (this.monitor)
				{
					return this.debugger;
				}
			}

			set
			{
				lock (this.monitor)
				{
					this.debugger = value;
				}
			}
		}

		#endregion

		#region Protected Methods

		protected static string GetResourceText(string resourceName) => Properties.Resources.ResourceManager.GetString(resourceName);

		[SuppressMessage(
			"Microsoft.Design",
			"CA1031:DoNotCatchGeneralExceptionTypes",
			Justification = "We don't want an exception to escape the foreground execution step.")]
		protected int Run(string[] appArgs)
		{
			int exitCode = 0;

			try
			{
				// Parse the command line
				this.parameters = new ScriptParameters();
				if (!this.parameters.Initialize(appArgs, this.InConsole, SharpScriptDirectory))
				{
					this.ShowInformation(string.Format(Properties.Resources.Usage, ApplicationName));
					exitCode = (int)ExitCode.InvalidArg;
				}
				else
				{
					// Make sure the file exists.
					string fileName = this.parameters.FileName;
					if (!File.Exists(fileName))
					{
						this.ShowError(string.Format(Properties.Resources.FileNotFound, fileName));
						exitCode = (int)ExitCode.InvalidArg;
					}
					else
					{
						// Make sure the file has a supported extension.
						this.scriptType = ScriptTypeProvider.GetProviderType(fileName).ScriptType;

						// Execute the script asynchronously so we can cancel it if necessary.
						ExecuteScriptHandler handler = new(this.BackgroundExecuteScript);
						IAsyncResult result = handler.BeginInvoke(null, null);

						// Let derived application types "wait" in an appropriate manner
						// while the script finishes executing.
						this.ScriptExecuting(result);

						// We may never get to this depending on how the app terminates,
						// but we'll try to call it because we're supposed to.
						exitCode = handler.EndInvoke(result);
					}
				}
			}
			catch (Exception ex)
			{
				this.ShowError(ex.Message);
				exitCode = (int)ExitCode.UnhandledException;
			}

			Environment.ExitCode = exitCode;
			return exitCode;
		}

		protected void CancelScriptExecution()
		{
			AppDomain? executionDomain;
			lock (this.monitor)
			{
				executionDomain = this.executionDomain;
				this.executionDomain = null;
			}

			if (executionDomain != null)
			{
				// NOTE: This method needs to be equivalent to Script.Cancel().
				try
				{
					AppDomain.Unload(executionDomain);
				}
				catch (CannotUnloadAppDomainException)
				{
					// If the execution AppDomain didn't unload gracefully,
					// then we'll just exit the process immediately.
					Environment.Exit((int)ExitCode.Cancelled);
				}

				// Delete any temporary files (e.g., .exe, .pdb) now that the
				// AppDomain has unloaded and (hopefully) released them.
				ScriptCompiler.CleanUpOutputFiles(this.outputFiles);
			}
		}

		protected void BreakInDebugger()
		{
			DebuggerHandler? handler = this.CurrentDebugger;
			if (handler != null)
			{
				handler.Break();
			}
		}

		protected bool AttachDebugger() => this.CurrentDebugger?.Attach() ?? false;

		#endregion

		#region Protected Abstract Methods

		protected abstract void ScriptExecuting(IAsyncResult result);

		protected abstract void ScriptExecutionFinished();

		protected abstract void ShowError(string message);

		protected abstract void ShowInformation(string message);

		#endregion

		#region Private Methods

		[SuppressMessage(
			"Microsoft.Design",
			"CA1031:DoNotCatchGeneralExceptionTypes",
			Justification = "We don't want an exception to escape the background execution step.")]
		private int BackgroundExecuteScript()
		{
			int exitCode;
			try
			{
				// We'll create a new AppDomain to execute the script in.
				// That way we can change the application base to the file's
				// directory to behave more like it was run from there.  We
				// can also cancel the script by unloading the AppDomain.

				// Set the base path to the directory the file is in.
				AppDomainSetup setup = new()
				{
					ApplicationBase = Path.GetDirectoryName(this.parameters?.FileName ?? string.Empty),
					ApplicationName = this.ScriptName,
					ShadowCopyFiles = "false",
				};

				// Create the new AppDomain.
				AppDomain executionDomain = AppDomain.CreateDomain(ApplicationName, Assembly.GetExecutingAssembly().Evidence, setup);
				lock (this.monitor)
				{
					this.executionDomain = executionDomain;
				}

				string asmPath = typeof(ScriptApplication).Assembly.Location;

				// See the comments in the ScriptHandler constructor for more
				// on how much loader magic is going on here.  Because we changed
				// the ApplicationBase, we have to jump through some hoops.
				ScriptHandler script = (ScriptHandler)executionDomain.CreateInstanceFromAndUnwrap(asmPath, typeof(ScriptHandler).FullName);

				// As of .NET 2.0, unhandled exceptions in the script AppDomain won't
				// automatically propagate back to the calling domain.  In fact, some
				// exception types may not even marshal across the AppDomain boundaries.
				// So I'll just check for a returned error message string from the Compile
				// and Execute methods.

				// Make two calls here so we can get back the compiler's temporary
				// file list first.  That way we can clean everything up later after
				// we unload the AppDomain.
				string? exceptionMessage = null;
				this.outputFiles = this.parameters != null ? script.Compile(this.parameters, out exceptionMessage) : null;
				if (exceptionMessage != null && exceptionMessage.Length > 0)
				{
					throw Exceptions.Log(new ScriptException(exceptionMessage));
				}

				// Create the debugger handler now that we've compiled but before we execute.
				this.CurrentDebugger = (DebuggerHandler)executionDomain.CreateInstanceFromAndUnwrap(asmPath, typeof(DebuggerHandler).FullName);

				// Now run the script since it compiled successfully.
				exitCode = script.Execute(out exceptionMessage);
				if (exceptionMessage != null && exceptionMessage.Length > 0)
				{
					throw Exceptions.Log(new ScriptException(exceptionMessage));
				}
			}
			catch (AppDomainUnloadedException)
			{
				// Don't report anything for this exception.  It just means the user
				// cancelled the script.  We'll just use a special exit code.
				exitCode = (int)ExitCode.Cancelled;
			}
			catch (Exception ex)
			{
				exitCode = (int)ExitCode.Exception;
				this.ShowError(ex.Message);
			}

			// Clear the debugger handler now that the script is done.
			// Since it's an object in the script AppDomain, it may no longer
			// be safe to reference (e.g., if the script was cancelled and
			// the AppDomain was unloaded).
			lock (this.monitor)
			{
				if (this.executionDomain != null)
				{
					this.CurrentDebugger = null;
				}
			}

			Environment.ExitCode = exitCode;

			this.ScriptExecutionFinished();

			// We have to call this to force the execution AppDomain to unload.
			// That will release the output assembly, and then this method will
			// clean up any temporary files.
			this.CancelScriptExecution();

			return exitCode;
		}

		#endregion
	}
}
