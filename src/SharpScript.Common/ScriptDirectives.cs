namespace SharpScript
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using Menees;

	#endregion

	internal partial class ScriptDirectives
	{
		#region Private Data Members

		private readonly List<string> references = new();
		private readonly List<string> outputFiles = new();

		#endregion

		#region Constructors

		public ScriptDirectives(ScriptTypeProvider stp, ScriptParameters scriptParams)
		{
			using (StreamReader reader = new(scriptParams.FileName))
			{
				int lineIndex = 0;
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					StringComparison comparison = stp.CaseSensitive ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;

					string startTrimmedLine = line.TrimStart();
					if (GetDirectiveNameValue(startTrimmedLine, stp, comparison, out string directiveName, out string directiveValue))
					{
						switch (stp.CaseSensitive ? directiveName : directiveName.ToLower())
						{
							case "reference":
							case "r":
								string asmName = GetReferenceName(directiveValue, Properties.Resources.InvalidScriptReference, lineIndex);
								this.references.Add(asmName);
								break;

							case "com_reference":
							case "com":
								string tlbName = GetReferenceName(directiveValue, Properties.Resources.InvalidScriptComReference, lineIndex);
								this.AddComReference(tlbName);
								break;

							case "debug":
								if (!string.IsNullOrEmpty(directiveValue))
								{
									throw DirectiveException(Properties.Resources.InvalidScriptDebugDirective, lineIndex);
								}

								scriptParams.Debug = true;
								break;

							case "compiler":
								this.Compiler = GetCompilerType(directiveValue, Properties.Resources.InvalidCompilerReference, lineIndex, stp.CaseSensitive);
								break;
						}
					}
					else if (!string.IsNullOrWhiteSpace(startTrimmedLine) && !startTrimmedLine.StartsWith(stp.ReferenceLineCommentDelimiter))
					{
						// Stop scanning when we get to the first non-empty, non-whitespace, non-directive, non-comment line.
						break;
					}

					lineIndex++;
				}
			}
		}

		#endregion

		#region Public Properties

		public IEnumerable<string> References => this.references;

		public IEnumerable<string> OutputFiles => this.outputFiles;

		public CompilerType? Compiler { get; }

		#endregion

		#region Private Methods

		private static bool GetDirectiveNameValue(
			string line,
			ScriptTypeProvider stp,
			StringComparison comparison,
			out string directiveName,
			out string directiveValue)
		{
			bool result = false;
			directiveName = string.Empty;
			directiveValue = string.Empty;

			if (line.StartsWith(stp.ReferenceLineCommentDelimiter, comparison))
			{
				line = line.Remove(0, stp.ReferenceLineCommentDelimiter.Length).TrimStart();
				if (line.StartsWith("#", comparison) && line.Length > 1)
				{
					int spacePos = line.IndexOfAny(new char[] { ' ', '\t' }, 1);
					if (spacePos >= 0)
					{
						directiveName = line.Substring(1, spacePos - 1);
						if (spacePos + 1 < line.Length)
						{
							directiveValue = line.Substring(spacePos + 1);
						}
					}
					else
					{
						directiveName = line.Substring(1);
					}

					result = true;
				}
			}

			return result;
		}

		private static string GetReferenceName(string directiveValue, string errorMessageFormat, int line)
		{
			string? referenceName = null;
			directiveValue = directiveValue.Trim();
			if (directiveValue.StartsWith("\"") && directiveValue.EndsWith("\""))
			{
				referenceName = directiveValue.Substring(1, directiveValue.Length - 2);
			}

			if (referenceName.IsEmpty())
			{
				throw DirectiveException(errorMessageFormat, line);
			}

			return referenceName;
		}

		private static CompilerType GetCompilerType(string directiveValue, string errorMessageFormat, int line, bool caseSensitive)
		{
			// Make the double quotes and whitespace optional.
			directiveValue = directiveValue.Trim().Trim('"').Trim();

			if (!Enum.TryParse(directiveValue, !caseSensitive, out CompilerType result))
			{
				string names = string.Join(", ", Enum.GetNames(typeof(CompilerType)));
				throw DirectiveException(errorMessageFormat, line, names);
			}

			return result;
		}

		private static ScriptException DirectiveException(string errorMsgFmt, int line, params object[] other)
		{
			object[] args = new object[] { line + 1 }.Concat(other ?? Enumerable.Empty<object>()).ToArray();
			string message = string.Format(errorMsgFmt, args);
			return Exceptions.Log(new ScriptException(message));
		}

		private void AddComReference(string tlbName)
		{
			string? namespaceName = null;

			// See if the reference contains a namespace.
			string[] names = tlbName.Split(',', ';');
			if (names.Length == 2)
			{
				tlbName = names[0];
				namespaceName = names[1].Trim();
			}

			TypeLibImporter importer = new(tlbName, namespaceName);

			// Add all of the interop assemblies as references.
			string[] interopAssemblies = importer.GetInteropAssemblies();
			this.references.AddRange(interopAssemblies);

			// Add any generated files to the output files.
			string[] outputFiles = importer.GetOutputFiles();
			this.outputFiles.AddRange(outputFiles);
		}

		#endregion
	}
}
