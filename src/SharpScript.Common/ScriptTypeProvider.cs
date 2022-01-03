namespace SharpScript
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Reflection;
	using System.Text;
	using System.Threading.Tasks;
	using Menees;

	#endregion

	internal abstract class ScriptTypeProvider
	{
		#region Private Data Members

		private static readonly ScriptTypeProvider[] AllProviders = new ScriptTypeProvider[]
			{
				new ScriptCSharpProvider(),
				new ScriptVbProvider(),
			};

		private readonly string[] extensions;

		#endregion

		#region Constructors

		protected ScriptTypeProvider(
			string[] extensions,
			string referenceLineCommentDelimiter,
			bool caseSensitive,
			ScriptType scriptType)
		{
			this.ReferenceLineCommentDelimiter = referenceLineCommentDelimiter;
			this.CaseSensitive = caseSensitive;
			this.ScriptType = scriptType;

			int numExtensions = extensions.Length;
			this.extensions = new string[numExtensions];
			for (int i = 0; i < numExtensions; i++)
			{
				string extension = extensions[i].ToLower().Trim();
				if (!extension.StartsWith("."))
				{
					extension = "." + extension;
				}

				this.extensions[i] = extension;
			}
		}

		#endregion

		#region Public Properties

		public string[] Extensions => this.extensions;

		public string ReferenceLineCommentDelimiter
		{
			get;
		}

		public bool CaseSensitive
		{
			get;
		}

		public ScriptType ScriptType
		{
			get;
		}

		public abstract IEnumerable<string> SpecialReferences
		{
			get;
		}

		public abstract string DebugCompileOptions { get; }

		public abstract string ReleaseCompileOptions { get; }

		#endregion

		#region Public Methods

		public static ScriptTypeProvider GetProviderType(string fileName)
		{
			string fileExtension = Path.GetExtension(fileName).ToLower();

			foreach (ScriptTypeProvider provider in AllProviders)
			{
				foreach (string typeExtension in provider.Extensions)
				{
					if (typeExtension == fileExtension)
					{
						return provider;
					}
				}
			}

			// If we get here, then the extension is an unsupported type.
			string message = string.Format(Properties.Resources.UnsupportedExtension, fileExtension);
			throw Exceptions.Log(new ScriptException(message));
		}

		#endregion
	}
}
