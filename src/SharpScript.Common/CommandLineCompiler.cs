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
	using System.Threading.Tasks;
	using Menees.Shell;
	using Microsoft.VisualStudio.Setup.Configuration;

	#endregion

	internal sealed class CommandLineCompiler : ScriptCompiler
	{
		#region Private Data Members

		// VS 2017 is version 15, and it includes MSBuild v15.
		private const int MinMajorVersion = 15;
		private const int VsVersion = 2017;

		private readonly string roslynPath;

		#endregion

		#region Constructors

		public CommandLineCompiler(
			ScriptTypeProvider stp,
			ScriptDirectives directives,
			ScriptParameters parameters,
			Task<IEnumerable<string>> assemblyFoldersTask)
			: base(stp, directives, parameters, assemblyFoldersTask)
		{
			this.roslynPath = FindRoslynPath();
			if (string.IsNullOrEmpty(this.roslynPath))
			{
				throw new InvalidOperationException(
					$"Unable to find the Roslyn compilers with MSBuild.  Please install the \"Build Tools for Visual Studio {VsVersion}\" (or later).");
			}
		}

		#endregion

		#region Public Methods

		public override Assembly Compile(bool throwOnError)
		{
			StringBuilder args = new StringBuilder();
			string outputExeName = this.AppendArgs(args);

			string compilerExeName = this.TypeProvider.ScriptType == ScriptType.VB ? this.AppendVBArgs(args) : this.AppendCSharpArgs(args);
			Assembly result = null;
			ProcessStartInfo startInfo = new ProcessStartInfo(Path.Combine(this.roslynPath, compilerExeName), args.ToString());
			ConsoleOutputBuffer buffer = new ConsoleOutputBuffer(startInfo, true);
			if (buffer.HasProcessExited && buffer.ProcessExitCode == 0)
			{
				AssemblyName assemblyName = AssemblyName.GetAssemblyName(outputExeName);
				result = Assembly.Load(assemblyName);
			}
			else
			{
				string message = Properties.Resources.CompileErrors + ":\r\n" + buffer.GetText();
				this.FailCompile(message, throwOnError);
			}

			return result;
		}

		#endregion

		#region Private Methods

		[SuppressMessage(
			"Microsoft.Design",
			"CA1031:DoNotCatchGeneralExceptionTypes",
			Justification = "Microsoft's COM API and samples say to catch all exceptions for the SetupConfiguration objects.")]
		private static string FindRoslynPath()
		{
			string result = null;
			Version resultVersion = null;

			// We require the Roslyn compilers to be installed with MSBuild 15 (or later) using the "Build Tools for Visual Studio 2017" (or later).
			// But multiple side-by-side editions can be installed, so there's no environment variable to find them.  We have to use a COM API.
			// For more info see the comments in MegaBuild's VSVersionInfo.TryGetPathFromSetupConfiguration method.
			const int REGDB_E_CLASSNOTREG = unchecked((int)0x80040154);
			try
			{
				SetupConfiguration configuration = new SetupConfiguration();

				IEnumSetupInstances instanceEnumerator = configuration.EnumAllInstances();
				int fetched;
				ISetupInstance[] instances = new ISetupInstance[1];
				do
				{
					instanceEnumerator.Next(1, instances, out fetched);
					if (fetched > 0)
					{
						ISetupInstance instance = instances[0];
						if (instance != null
							&& Version.TryParse(instance.GetInstallationVersion(), out Version version)
							&& version.Major >= MinMajorVersion)
						{
							InstanceState state = ((ISetupInstance2)instance).GetState();
							if (state == InstanceState.Complete)
							{
								string versionPath = version.Major == MinMajorVersion ? $"{MinMajorVersion}.0" : "Current";
								string subPath = $@"MSBuild\{versionPath}\Bin\Roslyn";
								string roslynPath = instance.ResolvePath(subPath);
								if ((resultVersion == null || resultVersion < version) && Directory.Exists(roslynPath))
								{
									result = roslynPath;
									resultVersion = version;

									// Quit the inner 2-pass loop for MSBuild version matching,
									// but keep doing the outer loop looking for a newer VS version.
									break;
								}
							}
						}
					}
				}
				while (fetched > 0);
			}
			catch (COMException ex) when (ex.HResult == REGDB_E_CLASSNOTREG)
			{
				// The SetupConfiguration API is not registered, so assume no instances are installed.
			}
			catch (Exception)
			{
				// Heath Stewart (MSFT), the author of the SetupConfiguration API, says to treat any exception as "no instances installed."
			}

			return result;
		}

		private static void AppendQuotedFileName(StringBuilder args, string prefix, string fileName)
			=> args.Append(prefix).Append('"').Append(fileName).Append("\" ");

		private string AppendArgs(StringBuilder args)
		{
			string scriptFileName = this.Parameters.FileName;
			AppendQuotedFileName(args, null, scriptFileName);

			// Append new extensions instead of changing the script extension because in the Samples
			// directory we can have multiple scripts with the same name but different extensions, and
			// we want to be able to run them at the same time.
			string exeName = scriptFileName + ".exe";
			AppendQuotedFileName(args, "/out:", exeName);
			this.AddOutputFile(exeName);

			args.Append("/noconfig /nologo /langversion:latest /warnaserror+ /define:TRACE /platform:anycpu ");
			args.Append("/target:").Append(this.Parameters.InConsole ? string.Empty : "win").Append("exe ");
			if (this.Parameters.Debug)
			{
				string pdbName = scriptFileName + ".pdb";
				AppendQuotedFileName(args, "/pdb:", pdbName);
				this.AddOutputFile(pdbName);
				args.Append("/optimize- /debug+ /define:DEBUG ");
			}
			else
			{
				args.Append("/optimize+ /debug- ");
			}

			IEnumerable<string> references = this.CreateReferences(fileName => fileName);
			foreach (string reference in references)
			{
				AppendQuotedFileName(args, "/r:", reference);
			}

			return exeName;
		}

		private string AppendCSharpArgs(StringBuilder args)
		{
			if (this.Parameters.Debug)
			{
				args.Append("/checked ");
			}

			return "csc.exe";
		}

		private string AppendVBArgs(StringBuilder args)
		{
			args.Append("/optionstrict+ /optioninfer+ /optionexplicit+ /imports:");
			bool first = true;
			foreach (string import in ScriptVbProvider.GlobalImports)
			{
				if (first)
				{
					first = false;
				}
				else
				{
					args.Append(',');
				}

				args.Append(import);
			}

			args.Append(' ');
			if (!this.Parameters.Debug)
			{
				args.Append("/removeintchecks+ ");
			}

			return "vbc.exe";
		}

		#endregion
	}
}
