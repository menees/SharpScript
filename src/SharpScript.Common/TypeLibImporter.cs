namespace SharpScript
{
	#region Using Directives

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.IO;
	using System.Reflection;
	using System.Reflection.Emit;
	using System.Runtime.InteropServices;

	#endregion

	[SuppressMessage(
		"Microsoft.Design",
		"CA1060:MovePInvokesToNativeMethodsClass",
		Justification = "This contains the only P/Invoke in the whole project, so it doesn't need a separate class.")]
	internal sealed class TypeLibImporter : ITypeLibImporterNotifySink
	{
		#region Private Data Members

		private readonly List<string> outputFiles = new List<string>();
		private readonly string tlbFullName;
		private string namespaceName;

		#endregion

		#region Constructor

		public TypeLibImporter(string tlbFullName, string namespaceName)
		{
			// Do some initial setup.
			this.tlbFullName = tlbFullName;
			this.SetNamespace(namespaceName);

			// Load the type library and get an ITypeLib.
			LoadTypeLibEx(tlbFullName, RegKind.None, out object typeLib);
			if (typeLib == null)
			{
				throw new ArgumentException(string.Format(Properties.Resources.FailedLoadTypeLib, tlbFullName));
			}

			// Generate the Interop assembly.
			this.ConvertTlb(typeLib);
		}

		#endregion

		#region Private Enums

		private enum RegKind
		{
			Default = 0,
			Register = 1,
			None = 2,
		}

		#endregion

		#region Private Properties

		private string TypeLibraryName
		{
			get
			{
				string tlbFileName = Path.GetFileName(this.tlbFullName);
				return Path.GetFileNameWithoutExtension(tlbFileName);
			}
		}

		#endregion

		#region Public Methods

		public string[] GetInteropAssemblies() => this.GetOutputFiles();

		public string[] GetOutputFiles() => this.outputFiles.ToArray();

		#endregion

		#region ITypeLibImporterNotifySink Members

		public void ReportEvent(ImporterEventKind eventKind, int eventCode, string eventMsg)
		{
			Debug.WriteLine(string.Format("{0}: {1} ({2})", eventKind, eventMsg, eventCode));
		}

		public Assembly ResolveRef(object typeLib) => this.ConvertTlb(typeLib);

		#endregion

		#region Private Imports

		[DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
		private static extern void LoadTypeLibEx(string typeLibName, RegKind regKind, [MarshalAs(UnmanagedType.Interface)] out object typeLib);

		#endregion

		#region Private Methods

		private void SetNamespace(string namespaceName)
		{
			if (namespaceName == null)
			{
				namespaceName = this.TypeLibraryName;
			}

			this.namespaceName = namespaceName;
		}

		private string GenerateAssemblyName()
		{
			string result;

			// If this is the first output file, we'll just do Interop.XXX.dll.
			// If it is a subsequent reference, then we'll have to add in a
			// counter.
			int numOutputFiles = this.outputFiles.Count;
			if (numOutputFiles == 0)
			{
				result = string.Format("Interop.{0}.dll", this.TypeLibraryName);
			}
			else
			{
				result = string.Format("Interop.{0}.Ref{1}.dll", this.TypeLibraryName, numOutputFiles);
			}

			return result;
		}

		private Assembly ConvertTlb(object typeLib)
		{
			string outputAssembly = this.GenerateAssemblyName();

			// AssemblyBuilder.Save does not support a path, but the
			// ConvertTypeLibToAssembly method does.
			string outputAsmFullName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, outputAssembly);

			TypeLibConverter converter = new TypeLibConverter();
			AssemblyBuilder ab = converter.ConvertTypeLibToAssembly(
				typeLib,
				outputAsmFullName,
				TypeLibImporterFlags.TransformDispRetVals,
				this,
				null,
				null,
				this.namespaceName,
				null);
			ab.Save(outputAssembly);

			this.outputFiles.Add(outputAsmFullName);

			return ab;
		}

		#endregion
	}
}