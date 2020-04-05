namespace SharpScript
{
	#region Using Directives

	using System;
	using System.CodeDom.Compiler;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using System.Text;
	using System.Threading.Tasks;

	#endregion

	internal sealed class CodeDomCompiler : ScriptCompiler
	{
		#region Private Data Members

		private CompilerResults results;

		#endregion

		#region Constructors

		public CodeDomCompiler(
			ScriptTypeProvider stp,
			ScriptDirectives directives,
			ScriptParameters parameters,
			Task<IEnumerable<string>> assemblyFoldersTask)
			: base(stp, directives, parameters, assemblyFoldersTask)
		{
		}

		#endregion

		#region Protected Properties

		// The CodeDom compilers add the mscorlib reference automatically.
		protected override IEnumerable<string> StandardReferences
			=> base.StandardReferences.Except(new[] { "mscorlib.dll" });

		#endregion

		#region Public Methods

		public override Assembly Compile(bool throwOnError)
		{
			// Create the correct compiler in parallel while we read the script.
			Task<CodeDomProvider> compilerTask = Task.Run(() => CreateCompiler(this.TypeProvider.ScriptType));

			// Configure the compiler and add all assembly references.
			CompilerParameters compileParams = this.SetupCompilerOptions();
			StringCollection references = compileParams.ReferencedAssemblies;
			foreach (string reference in this.CreateReferences(fileName => fileName))
			{
				references.Add(reference);
			}

			// Compile the script.
			CodeDomProvider compiler = compilerTask.Result;
			this.results = compiler.CompileAssemblyFromFile(compileParams, this.Parameters.FileName);
			this.AddCompilerOutputFiles();

			Assembly result = null;
			if (this.VerifyCompile(throwOnError))
			{
				result = this.results.CompiledAssembly;
			}

			return result;
		}

		#endregion

		#region Private Methods

		private static CodeDomProvider CreateCompiler(ScriptType scriptType)
		{
			// Determine the current .NET version.  Due to the app.config file's
			// <supportedRuntime> element, it may not be the same version of
			// .NET that SharpScript was built against.  It could be newer.
			Version dotNetVersion = Environment.Version;

			// For .NET 3.5 (actually in .NET 2.0 SP1), Microsoft added special new constructors
			// to the C# and VB CodeDomProvider classes, and that's the only programmatic
			// way to force them to use specific version language features.  So I'm hardcoding
			// these constructor calls rather than adding even more configurable options for
			// special constructor invocation.
			Dictionary<string, string> providerOptions = new Dictionary<string, string>();
			providerOptions.Add("CompilerVersion", string.Format("v{0}.{1}", dotNetVersion.Major, dotNetVersion.Minor));

			CodeDomProvider result;
			if (scriptType == ScriptType.VB)
			{
				result = new Microsoft.VisualBasic.VBCodeProvider(providerOptions);
			}
			else
			{
				result = new Microsoft.CSharp.CSharpCodeProvider(providerOptions);
			}

			return result;
		}

		private CompilerParameters SetupCompilerOptions()
		{
			CompilerParameters compileParams = new CompilerParameters
			{
				// We have to do this so we'll have an EntryPoint.
				GenerateExecutable = true,
			};

			// If we're including debug information, then we need to generate
			// the file on disk and not just in memory.
			bool debug = this.Parameters.Debug;
			compileParams.GenerateInMemory = !debug;
			compileParams.IncludeDebugInformation = debug;
			if (debug)
			{
				// I'm appending ".exe" rather than changing the file's extension.
				// This makes it possible to have two script files with different
				// extensions running in the same directory without clobbering each
				// other's output.  That can happen in the Samples directory.
				compileParams.OutputAssembly = this.Parameters.FileName + ".exe";
			}

			// Make the script options as strict as possible.  If people want
			// weak checking, they should use WScript.
			compileParams.TreatWarningsAsErrors = true;
			const int V = 4;
			compileParams.WarningLevel = V;

			// Set the custom compile options for the current build mode.
			StringBuilder options = new StringBuilder(compileParams.CompilerOptions);

			// Set the executable to the correct target type.
			// This isn't strictly necessary, but if the temporary
			// exe gets left out there and a user tries to run it,
			// I'd like it to have the correct type.
			if (this.Parameters.InConsole)
			{
				options.Append(" /target:exe");
			}
			else
			{
				options.Append(" /target:winexe");
			}

			// Get custom compile options for the provider type.
			options.Append(' ');
			if (debug)
			{
				options.Append(this.TypeProvider.DebugCompileOptions);
			}
			else
			{
				options.Append(this.TypeProvider.ReleaseCompileOptions);
			}

			compileParams.CompilerOptions = options.ToString();

			return compileParams;
		}

		private bool VerifyCompile(bool throwOnError)
		{
			int numErrors = this.results.Errors.Count;
			if (numErrors > 0)
			{
				string[] scriptLines = File.ReadAllLines(this.Parameters.FileName);

				const int BufferSize = 1024;
				StringBuilder sb = new StringBuilder(BufferSize);
				sb.Append(Properties.Resources.CompileErrors);
				sb.Append(":\r\n");

				foreach (CompilerError error in this.results.Errors)
				{
					int lineNumber = error.Line;

					if (lineNumber > 0)
					{
						string format = error.Column >= 1 ? Properties.Resources.LineCharacter : Properties.Resources.LinePrefix;
						sb.AppendFormat(format, lineNumber, error.Column);
					}

					// Show the standard error information.
					string type = error.IsWarning ? Properties.Resources.Warning : Properties.Resources.Error;
					sb.AppendFormat("{0} {1}: {2}\r\n", type, error.ErrorNumber, error.ErrorText);

					// Show the line text with each error message.  But check the bounds because
					// I've seen the VB compiler return a line number equal to scriptLines.Length + 3.
					if (lineNumber > 0 && lineNumber <= scriptLines.Length)
					{
						sb.Append('\t');
						string line = scriptLines[lineNumber - 1].Trim();
						sb.Append(line);
						sb.Append("\r\n");
					}

					sb.Append("\r\n");
				}

				sb.Append(Properties.Resources.TryRoslyn);

				this.FailCompile(sb.ToString(), throwOnError);
			}

			return numErrors == 0;
		}

		private void AddCompilerOutputFiles()
		{
			// Add the assembly.
			string asmPath = this.results.PathToAssembly;
			if (asmPath != null && asmPath.Length > 0)
			{
				this.AddOutputFile(asmPath);

				// Add the debug symbols if necessary.  They're never
				// listed as one of the temporary files.
				if (this.Parameters.Debug)
				{
					this.AddOutputFile(Path.ChangeExtension(asmPath, ".pdb"));
				}
			}

			// Add any temporary files.
			foreach (string fileName in this.results.TempFiles)
			{
				if (fileName != null && fileName.Length > 0)
				{
					this.AddOutputFile(fileName);
				}
			}
		}

		#endregion
	}
}
