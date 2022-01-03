namespace SharpScript
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Menees;
	using Microsoft.Win32;

	#endregion

	[SuppressMessage("", "CA1812", Justification = "Created via reflection using CreateInstanceFromAndUnwrap.")]
	internal sealed class ScriptHandler : CrossAppDomainObject
	{
		#region Private Data Members

		private ScriptParameters? scriptParameters;
		private Assembly? emittedAssembly;

		#endregion

		#region Constructor

		public ScriptHandler()
		{
			// Before we do anything else, we need to get setup to handle assembly Load
			// failures.  This is necessary since this object is the first thing created
			// in this AppDomain, and we created it using AppDomain.CreateInstanceFrom.
			// That causes this assembly to be loaded into the "LoadFrom" context.  As
			// soon as the Execute method is called, the .NET loader will need to get
			// this assembly into the "Load" context, so it can marshal the ScriptParameters
			// class correctly.  The only way to do that and have the same Assembly reference
			// used in both contexts is to use the AssemblyResolve event.
			AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(this.CurrentDomain_AssemblyResolve);
		}

		#endregion

		#region Public Methods

		[SuppressMessage(
			"Microsoft.Design",
			"CA1031:DoNotCatchGeneralExceptionTypes",
			Justification = "We don't want an exception to escape the Compile step because it might not cross the AppDomain boundary.")]
		public string[]? Compile(ScriptParameters parameters, out string exceptionMessage)
		{
			string[]? result = null;

			exceptionMessage = string.Empty;
			try
			{
				// Kick this task off first, so it can run while we prep the compiler and read the script.
				Task<IEnumerable<string>> assemblyFoldersTask = Task.Run(() => CacheAssemblyFolders());

				this.scriptParameters = parameters;

				// Determine the script type based on the extension.
				ScriptTypeProvider stp = ScriptTypeProvider.GetProviderType(this.scriptParameters.FileName);

				// Read all the script directives like #reference, #option, etc.
				ScriptDirectives directives = new(stp, this.scriptParameters);

				ScriptCompiler compiler;
				switch (directives.Compiler)
				{
					case CompilerType.CodeDom:
						compiler = new CodeDomCompiler(stp, directives, parameters, assemblyFoldersTask);
						break;

					default:
						compiler = new CommandLineCompiler(stp, directives, parameters, assemblyFoldersTask);
						break;
				}

				this.emittedAssembly = compiler.Compile(true);

				// Return all the output file names, so the caller can clean them up after our AppDomain unloads.
				result = compiler.GetOutputFiles();
			}
			catch (Exception ex)
			{
				exceptionMessage = ex.Message;
			}

			return result;
		}

		[SuppressMessage(
			"Microsoft.Design",
			"CA1031:DoNotCatchGeneralExceptionTypes",
			Justification = "We don't want an exception to escape the Execute step because it might not cross the AppDomain boundary.")]
		public int Execute(out string exceptionMessage)
		{
			int result;

			exceptionMessage = string.Empty;
			try
			{
				if (this.emittedAssembly == null)
				{
					Debug.Assert(false, "Compile should have been called before Execute.");

					// This doesn't use a string resource because it should never get hit.
					throw Exceptions.Log(new ScriptException("INTERNAL ERROR: Execute called before successful compile."));
				}

				// Initialize the static "Script" class since we're about to execute.
				if (this.scriptParameters != null)
				{
					Script.Initialize(this.scriptParameters);
				}

				// Run the script.
				result = this.ExecuteAssembly();

				// Try to return the best ExitCode for the process.
				if (result == 0)
				{
					result = Environment.ExitCode;
				}
			}
			catch (Exception ex)
			{
				exceptionMessage = ex.Message;
				result = (int)ExitCode.Exception;
			}

			return result;
		}

		#endregion

		#region Private Methods

		private static IEnumerable<string> CacheAssemblyFolders()
		{
			List<string> result = new();

			// The main .NET reference assemblies are always in the 32-bit Program Files directory.
			// (e.g., %ProgramFiles%\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0).
			// http://msdn.microsoft.com/en-us/library/ee395432.aspx
			// http://blogs.msdn.com/jgoldb/archive/2010/04/12/what-s-new-in-net-framework-4-client-profile-rtm.aspx
			Version dotNetVersion = Environment.Version;
			string programFiles = Environment.ExpandEnvironmentVariables(Environment.Is64BitProcess ?
				"%ProgramFiles(x86)%" : "%ProgramFiles%");
			string baseReferencePath = Path.Combine(programFiles, @"Reference Assemblies\Microsoft\Framework\.NETFramework\");

			// .NET 4.5-4.6 still report as 4.0.30319.x from Environment.Version, but since we're interested in the latest 4.x,
			// we need to look for their reference assemblies first so scripts can refer to any 4.x types and members.
			const int DotNetVersion4 = 4;
			if (dotNetVersion.Major == DotNetVersion4 && dotNetVersion.Minor == 0)
			{
				// Look through the v4.x.y versions in order from newest to oldest.
				foreach (string version in new[] { "V4.7", "v4.6.2", "v4.6.1", "v4.6", "v4.5.2", "v4.5.1", "v4.5" })
				{
					string referencePath = baseReferencePath + version;
					if (Directory.Exists(referencePath))
					{
						result.Add(referencePath);

						// C#7 requires System.ValueTuple.dll when using value tuples, but that DLL doesn't ship with .NET 4.5-4.6.x.
						// So we have to reference a local copy of that DLL, which refers to System.Runtime.dll.  But we don't have
						// a local copy of System.Runtime.dll because .NET 4.x includes a "facade" for it.  So we need the Facades dir too.
						// http://stackoverflow.com/a/22822407/1882616
						// https://support.microsoft.com/en-us/help/2971005/error-message-when-you-compile-applications-to-target-the-.net-framework-4.5.2
						string facadesPath = Path.Combine(referencePath, "Facades");
						if (Directory.Exists(facadesPath))
						{
							result.Add(facadesPath);
						}

						break;
					}
				}

				// Note: There is no "Client Profile" version of .NET 4.5.
			}

			if (result.Count == 0)
			{
				string referencePath = baseReferencePath + string.Format("v{0}.{1}", dotNetVersion.Major, dotNetVersion.Minor);
				if (Directory.Exists(referencePath))
				{
					result.Add(referencePath);

					// If only the client profile is installed, then the base reference path may not
					// contain any assemblies. So add the client profile directory too if it exists.
					referencePath += @"\Profile\Client";
					if (Directory.Exists(referencePath))
					{
						result.Add(referencePath);
					}
				}
				else
				{
					// We're probably on a system that doesn't have the .NET SDK or VS installed, so the
					// reference assemblies aren't available.  In that case, we'll just point to the runtime
					// directories we need to satisfy the standard assembly references we add.
					string runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
					result.Add(runtimeDirectory);
					string wpfDirectory = Path.Combine(runtimeDirectory, "WPF");
					if (Directory.Exists(wpfDirectory))
					{
						result.Add(wpfDirectory);
					}
				}
			}

			// Look for other assembly folders configured for the current version of .NET.
			string runtimeAsmFoldersEx = string.Format(
				@"Software\Microsoft\.NETFramework\v{0}.{1}.{2}\AssemblyFoldersEx",
				dotNetVersion.Major,
				dotNetVersion.Minor,
				dotNetVersion.Build);

			// Cache the folders in parallel, but add them to the result list with user entries first.
			Task<IList<string>> userTask = Task.Run(() => CacheAssemblyFolders(runtimeAsmFoldersEx, Registry.CurrentUser));
			Task<IList<string>> machineTask = Task.Run(() => CacheAssemblyFolders(runtimeAsmFoldersEx, Registry.LocalMachine));
			result.AddRange(userTask.Result);
			result.AddRange(machineTask.Result);

			return result;
		}

		private static IList<string> CacheAssemblyFolders(string runtimeAsmFoldersEx, RegistryKey baseKey)
		{
			IList<string> result = new List<string>();

			using (RegistryKey foldersKey = baseKey.OpenSubKey(runtimeAsmFoldersEx))
			{
				if (foldersKey != null)
				{
					string[] folders = foldersKey.GetSubKeyNames();
					foreach (string folderKeyName in folders)
					{
						using (RegistryKey folderKey = foldersKey.OpenSubKey(folderKeyName))
						{
							if (folderKey != null)
							{
								string folder = (string)folderKey.GetValue(string.Empty);
								if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
								{
									result.Add(folder);
								}
							}
						}
					}
				}
			}

			return result;
		}

		private static ApartmentState GetRequiredApartmentState(MethodInfo main)
		{
			// See if we need to run this on an STA thread.  Some scripts need
			// this if they're going to invoke STA objects like OpenFileDialog.
			// If they need an STA thread, then they must mark the Main method
			// with the [STAThread] attribute.
			ApartmentState aptState = ApartmentState.MTA;
			if (main.GetCustomAttributes(typeof(STAThreadAttribute), false).Length > 0)
			{
				aptState = ApartmentState.STA;
			}

			return aptState;
		}

		[SuppressMessage(
			"Microsoft.Design",
			"CA1031:DoNotCatchGeneralExceptionTypes",
			Justification = "We don't want an exception to escape the thread's entry point method.")]
		private int ExecuteAssembly()
		{
			// Get the main entry point to the new assembly.
			MethodInfo? main = this.emittedAssembly?.EntryPoint;
			if (main == null)
			{
				throw new InvalidProgramException("The script's entry point (e.g., a Main method) could not be found.");
			}

			// Main's parameter can be void or string[].  We need to handle both cases.
			object[]? mainArgs = null;
			if (main.GetParameters().Length == 1 && this.scriptParameters != null)
			{
				mainArgs = new object[] { this.scriptParameters.Arguments };
			}

			// These will be set the by the thread's anonymous delegate.
			Exception? exception = null;
			int result = 0;

			// Always execute in a new worker thread so we can control its apartment model.
			// Remember: This code won't execute until Thread.Start is called!
			Thread thread = new(() =>
				{
					// Run the Main method.
					try
					{
						object invokeResult = main.Invoke(null, mainArgs);

						// If it returns a value, then pass it back.  Otherwise, use the Environment's ExitCode.
						if (main.ReturnType == typeof(int))
						{
							result = (int)invokeResult;
						}
						else
						{
							result = Environment.ExitCode;
						}
					}
					catch (TargetInvocationException ex)
					{
						// In .NET 2.0, Main.Invoke now throws a TargetInvocationException
						// if the Main method throws an exception.  But the caller doesn't need
						// to see "Exception has been thrown by the target of an invocation."
						// every time.  The caller needs to see the original exception that got
						// thrown by the script.
						if (ex.InnerException != null)
						{
							exception = ex.InnerException;
						}
						else
						{
							exception = ex;
						}
					}
					catch (Exception ex)
					{
						exception = ex;
					}
				});

			// Determine the apartment state that the thread needs to use.
			ApartmentState aptState = GetRequiredApartmentState(main);
			thread.SetApartmentState(aptState);

			// Run the thread and wait for it to finish.
			thread.Start();
			thread.Join();

			// Check if we need to rethrow an exception from the thread function.
			if (exception != null && this.scriptParameters != null)
			{
				// In debug runs, we'll rethrow a message containing all the exception detail
				// such as type, stack trace, message, etc.  Release runs just get the message.
				string message = this.scriptParameters.Debug ? exception.ToString() : exception.Message;
				throw Exceptions.Log(new ScriptException(message));
			}

			return result;
		}

		private Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
		{
			Assembly? result = null;

			// If we couldn't find the assembly in the "Load" context, look through
			// all assemblies in the current AppDomain because the assembly may
			// be in the "LoadFrom" context.
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (Assembly assembly in assemblies)
			{
				if (assembly.FullName == args.Name)
				{
					result = assembly;
					break;
				}
			}

			// If we still can't find the assembly, see if it exists in the base SharpScript folder.
			// This allows scripts to use the Menees.* assemblies as well as any other shared
			// assemblies a user wants to deploy into the base SharpScript folder.
			if (result == null && this.scriptParameters != null)
			{
				string sharpScriptFolder = this.scriptParameters.SharpScriptDirectory;
				int commaIndex = args.Name.IndexOf(',');
				string fileNameNoExt = commaIndex < 0 ? args.Name : args.Name.Substring(0, commaIndex);
				string dllName = Path.Combine(sharpScriptFolder, fileNameNoExt + ".dll");
				if (File.Exists(dllName))
				{
					result = Assembly.LoadFrom(dllName);
				}
			}

			return result;
		}

		#endregion
	}
}
