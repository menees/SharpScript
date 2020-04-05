namespace SharpScript
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using System.Text;
	using System.Threading.Tasks;
	using Menees;

	#endregion

	internal abstract class ScriptCompiler
	{
		#region Private Data Members

		private static readonly string[] DefaultStandardReferences =
			{
				"mscorlib.dll",
				"System.dll",
				"System.Data.dll",
				"System.Xml.dll",
				"System.Core.dll",
				"System.Data.DataSetExtensions.dll",
				"System.Xml.Linq.dll",
			};

		private static readonly string[] DefaultGuiReferences =
			{
				"System.Windows.Forms.dll",
				"System.Drawing.dll",
				"System.Deployment.dll",
				"PresentationCore.dll",
				"PresentationFramework.dll",
				"WindowsBase.dll",
				"System.Xaml.dll",
			};

		private List<string> outputFiles = new List<string>();
		private Task<IEnumerable<string>> assemblyFoldersTask;

		#endregion

		#region Constructors

		protected ScriptCompiler(
			ScriptTypeProvider stp,
			ScriptDirectives directives,
			ScriptParameters parameters,
			Task<IEnumerable<string>> assemblyFoldersTask)
		{
			this.outputFiles.AddRange(directives.OutputFiles);
			this.TypeProvider = stp;
			this.Directives = directives;
			this.Parameters = parameters;
			this.assemblyFoldersTask = assemblyFoldersTask;
		}

		#endregion

		#region Public Properties

		public ScriptException CompileError { get; private set; }

		#endregion

		#region Protected Properties

		protected ScriptTypeProvider TypeProvider { get; }

		protected ScriptDirectives Directives { get; }

		protected ScriptParameters Parameters { get; }

		protected virtual IEnumerable<string> AssemblyFolders => this.assemblyFoldersTask.Result;

		protected virtual IEnumerable<string> StandardReferences => DefaultStandardReferences;

		protected virtual IEnumerable<string> GuiReferences => DefaultGuiReferences;

		#endregion

		#region Public Methods

		public static void CleanUpOutputFiles(IEnumerable<string> outputFiles)
		{
			if (outputFiles != null)
			{
				try
				{
					Parallel.ForEach(
						outputFiles,
						fileName =>
						{
							if (File.Exists(fileName))
							{
							// This will NOT throw an exception if the file can't be deleted.
							FileUtility.TryDeleteFile(fileName);
							}
						});
				}
				catch (AggregateException ex)
				{
					Log.Error(typeof(ScriptCompiler), "Error cleaning up output files.", ex);
				}
			}
		}

		public abstract Assembly Compile(bool throwOnError);

		public string[] GetOutputFiles() => this.outputFiles.ToArray();

		#endregion

		#region Protected Methods

		protected IEnumerable<T> CreateReferences<T>(Func<string, T> convertQualifiedName)
		{
			// Add the same default assembly references that VS uses for new projects (plus C#/VB and SharpScript refs).
			List<string> referenceNames = new List<string>(DefaultStandardReferences.Length + DefaultGuiReferences.Length + 2);
			referenceNames.AddRange(this.StandardReferences);
			referenceNames.AddRange(this.TypeProvider.SpecialReferences);

			if (!this.Parameters.InConsole)
			{
				// Give GUI apps the default references for both WinForms and WPF apps.
				referenceNames.AddRange(this.GuiReferences);
			}

			List<T> references = new List<T>();
			this.AddAssemblyReferences(references, referenceNames, convertQualifiedName);

			// Add the SharpScript.Common assembly.
			references.Add(convertQualifiedName(Assembly.GetExecutingAssembly().Location));

			// Add assembly references from within the file (e.g., //#reference "MyAssembly.dll").
			string scriptBaseDirectory = Path.GetDirectoryName(this.Parameters.FileName);
			Parallel.ForEach(
				this.Directives.References,
				asmRef =>
				{
					string qualifiedAsmRef = this.GetQualifiedAssemblyReference(asmRef, scriptBaseDirectory);
					T reference = convertQualifiedName(qualifiedAsmRef);
					lock (references)
					{
						references.Add(reference);
					}
				});

			return references;
		}

		protected void AddOutputFile(string fileName)
		{
			this.outputFiles.Add(fileName);
		}

		protected void FailCompile(string exceptionMessage, bool throwOnError)
		{
			// Clean up the output files before we throw since the caller never had a chance to get them.
			CleanUpOutputFiles(this.outputFiles);

			this.CompileError = new ScriptException(exceptionMessage);
			if (throwOnError)
			{
				throw Exceptions.Log(this.CompileError);
			}
		}

		#endregion

		#region Private Methods

		private void AddAssemblyReferences<T>(
			IList<T> references,
			IEnumerable<string> fileNames,
			Func<string, T> convertQualifiedName)
		{
			Parallel.ForEach(
				fileNames,
				fileName =>
				{
					string qualifiedName = this.QualifyFromAssemblyFolders(fileName);
					T reference = convertQualifiedName(qualifiedName);
					lock (references)
					{
						references.Add(reference);
					}
				});
		}

		private string GetQualifiedAssemblyReference(string asmRef, string scriptBaseDirectory)
		{
			// First expand any environment variable references so assembly refs can be system-independent.
			asmRef = Environment.ExpandEnvironmentVariables(asmRef);

			// If the assembly reference is already fully qualified, then we don't
			// have to do anything else.
			if (!Path.IsPathRooted(asmRef))
			{
				// See if the file exists under the script base directory.  If so, then
				// we want to use it.  This way we can compile against local "xcopied"
				// assemblies that aren't registered anywhere.
				//
				// Adjust the process's CurrentDirectory and use GetFullPath, so we
				// can correctly resolve relative paths to references.
				bool found = false;
				string originalCurrentDirectory = Environment.CurrentDirectory;
				Environment.CurrentDirectory = scriptBaseDirectory;
				try
				{
					string qualifiedAsmRef = Path.GetFullPath(asmRef);
					if (File.Exists(qualifiedAsmRef))
					{
						asmRef = qualifiedAsmRef;
						found = true;
					}
					else
					{
						// See if it's a shared assembly under the SharpScript folder.
						Environment.CurrentDirectory = this.Parameters.SharpScriptDirectory;
						qualifiedAsmRef = Path.GetFullPath(asmRef);
						if (File.Exists(qualifiedAsmRef))
						{
							asmRef = qualifiedAsmRef;
							found = true;
						}
					}
				}
				finally
				{
					Environment.CurrentDirectory = originalCurrentDirectory;
				}

				if (!found)
				{
					// The assembly name wasn't fully qualified, and it doesn't exist
					// when we qualify it from the script base directory or SharpScript
					// directory.  So we'll see if the assembly exists under any of the
					// configured AssemblyFolders in the registry.
					asmRef = this.QualifyFromAssemblyFolders(asmRef);
				}
			}

			return asmRef;
		}

		private string QualifyFromAssemblyFolders(string asmRef)
		{
			string result = asmRef;

			// This isn't parallelized because we want to match in order
			// (i.e., earlier folders are preferable to later folders).
			foreach (string folder in this.AssemblyFolders)
			{
				string qualifiedRef = Path.Combine(folder, asmRef);
				if (File.Exists(qualifiedRef))
				{
					result = qualifiedRef;
					break;
				}
			}

			return result;
		}

		#endregion
	}
}
