![windows build](https://github.com/menees/SharpScript/workflows/windows%20build/badge.svg)

# SharpScript
SharpScript provides .NET scripting using C# and VB.NET, and it's been around since 2004. Its functionality is similar to what WScript provides for COM scripting, but SharpScript lets you use .NET, so it’s significantly more powerful, type-safe, and object-oriented than previous scripting choices.

SharpScript includes a GUI version (SharpScript.exe) and a console version (SharpScriptConsole.exe).  The GUI version provides a tray icon that allows you to monitor or cancel a script’s execution.  Both versions allow you to run debug versions of your scripts so you can attach to them with a debugger if necessary.  The GUI version even provides menu items on the tray icon to make it easy to attach a debugger or break in the debugger.

SharpScript is written in C# and requires .NET Framework 4.8.  To use the latest C# or VB language features, you also need to install the latest [Build Tools For Visual Studio](https://visualstudio.microsoft.com/downloads/) (e.g., [2022](https://aka.ms/vs/17/release/vs_BuildTools.exe)) from Microsoft, which includes [MSBuild](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild) and the Roslyn compilers.

This software is CharityWare.  If you like it and use it, I ask that you donate something to the charity of your choice.  I'll never know if you follow this policy, but the good karma from following it will be well worth your investment.

## Script File Types
Running ShellSetup.bat executes ShellSetup.scs, which registers support for C# and VB.NET SharpScripts in Windows Explorer:
* .scs – C# script
* .svb – Visual Basic.NET script

Both of these file types have right-click menu options in Windows Explorer to:
* Run – Execute the script in “release” mode
* Run Debug – Execute the script in “debug” mode
* Edit – Open the script in Notepad.

If you have Visual Studio 20xx installed, then .scs and .svb SharpScript files can displayed with correct syntax highlighting when they’re opened in VS if you manually configure them under Tools -> Options -> Text Editor -> File Extension:
* scs – Microsoft Visual C#
* svb – Microsoft Visual Basic

## Coding
Writing a SharpScript is pretty much like writing a standard .NET application except you don’t need project files or anything fancy to compile and run them.  See the Scripts directory underneath the base SharpScript folderfor some “Hello, World” samples in several languages.  Here’s a C# version:

``` C#
using SharpScript;

class Test
{
	public static void Main(string[] args)
	{
		Script.Echo("Hello, World, from C#.");
	}
}
```

### Referencing Assemblies with #reference and #com_reference
SharpScripts running in SharpScriptConsole.exe reference the following assemblies by default:
* Mscorlib.dll
* System.dll
* System.Core.dll
* System.Data.dll
* System.Data.DataSetExtensions.dll
* System.Xml.dll
* System.Xml.Linq.dll
* SharpScript.Common.dll
* Microsoft.CSharp.dll (for C# scripts)

SharpScripts running in SharpScript.exe (the GUI version) reference all of the above assemblies plus:
* System.Drawing.dll
* System.Windows.Forms.dll
* System.Deployment.dll
* PresentationCore.dll
* PresentationFramework.dll
* WindowsBase.dll

To reference other assemblies you need to include a special `#reference` directive in a single-line comment within your script.  The syntax for `#reference` is:

```
single_line_comment_delimiter #reference "assemblyname"
```

The assembly name can be a file name, a relative path, or a fully-qualified path.  It can also contain environment variables, which will be expanded at compile-time.  An example usage in C# would be:

``` C#
//#reference "System.Design.dll"
```

An example usage in VB.NET would be:

``` VB
'#reference "System.Design.dll"
```

Both of these examples cause the generated script assembly to have a reference to the System.Design.dll.  Then any of the types defined within System.Design.dll can be used in the script.

SharpScript also supports a `#com_reference` directive that lets you refer to COM type libraries.  You can refer to type libraries in DLLs, TLBs, OLBs, or any other type of file supported by Windows’s LoadTypeLibEx function.  The referenced type library will automatically be wrapped by an Interop assembly.  The syntax for `#com_reference` is:

```
single_line_comment_delimiter #com_reference "TypeLibName[,Namespace]"
```

The namespace to use in the generated Interop assembly can be specified in the `#com_reference` directive.  If the namespace isn’t specified, then it will default to the type library name minus any extension.  Some example usages in C# would be:

``` C#
//#com_reference "ScrRun.dll, WScript"
//#com_reference "Msxml4.dll"
```

_Note_: Script directives must be placed at the top of the script file in single-line comments.  SharpScript will stop looking for directives when it encounters the first non-empty, non-whitespace, non-directive, non-single-line-comment line in the file.

### Other Script Directives
SharpScript supports some additional script directives using the standard directive syntax:

```
single_line_comment_delimiter #directiveName directiveValue
```

| Name | Value | Description |
|---|---|---|
| debug | None | Forces the script to be compiled in debug mode even if the //D command\-line option isn’t used\. C\# example: |
| | | `// #debug` |
| r | Assembly name | Shorthand notation for \#reference\. C\# example: |
| | | `// #r "System.Design.dll"` |
| com | Type library name | Shorthand notation for \#com\_reference\. C\# example: |
| | | `// #com "ScrRun.dll, WScript"` |
| compiler | Roslyn or CodeDom | Forces SharpScript to use the specified compiler\. If a \#compiler directive isn’t specified, then Roslyn is the default, which provides the latest language features \(e\.g\. for C\#6/VB14 or later\)\. However, Roslyn is slightly slower to compile than the older CodeDom \(which only supports up to C\#5/VB11\), so you might want to use CodeDom if startup performance is paramount\. C\# example: |
| | | `// #compiler Roslyn` |

### Strict Compiler Options
SharpScript compiles scripts with the strictest possible options enabled for each compiler type.  This includes warning level 4, treat warnings as errors, option explicit, option strict, etc.  In debug builds it also turns on integer checking.  I use this level of strictness because it’s what I prefer in my “real” development projects.  I’ve found that strictness leads to better programs and looseness leads to sloppy programs.

If you want looseness, you should probably stick with a WScript-compatible language.  Or if you’re a glutten for punishment you could use a VB.NET SharpScript and turn Option Explict off and Option Strict off in your script file.

#### Script Class API
SharpScript provides a static Script class that exposes several useful properties and methods.  The Script class lets you communicate with the user, check whether you’re running in the console and/or in debug mode, get the script filename and arguments, etc.  The Script class is similar to the “WScript” global named object that is available to COM scripts running in the WScript or CScript processors.
##### Properties
`public static bool Debug`
> Gets whether the script is running in “debug” mode.

`public static bool InConsole`
> Gets whether the script is running in a command console.

`public static bool IsExecuting`
>Gets whether the script is currently executing.  This is always true when SharpScript.exe or SharpScriptConsole.exe runs your script.  However, if you reference the SharpScript.Common.dll assembly from other programs and try to use the Script class, then this property will return false.  If this returns false, then all of the other properties and methods on the Script object will throw an InvalidOperationException if you try to use them.

`public static DateTime StartTime`
> Gets the local date and time the script was started.

`public static string FileName`
> Gets the full name and path to the script file.

`public static string Name`
> Gets the name of the script minus the path and extension.

`public static TimeSpan Timeout`
> Gets the maximum time that the script can run before it is automatically cancelled.  This defaults to zero, which means no timeout.  This property can only be set by the command-line switch //T.

`public static bool Quiet`
> Gets whether the script is running in “quiet” mode.  The GUI version will not show a system tray icon if this is true.  This does not effect MsgBox, InputBox, or Echo calls.

`public static string SharpScriptDirectory`
> Gets the full path to the folder containing SharpScript.exe and SharpScriptConsole.exe.

##### Methods
`public static DialogResult MsgBox(string strPrompt [,MessageBoxButtons eButtons [,MessageBoxIcon eIcon [,string strCaption]]])`
> Displays a message box using the specified options.  The last three parameters are optional.
>
> This method defaults the caption to the Script.Name property.   This makes it the best method for simple message boxes because MessageBox.Show and VB’s MsgBox method default the caption to the “application name”, which in a release mode SharpScript is a randomly generated name (e.g., “pxvjqx5n”).

`public static string InputBox(string strPrompt [,string strCaption [,string strDefault]])`
> Displays a dialog that lets the user enter a single line of text.  The last two parameters are optional.
>
> This does the same thing as VB’s InputBox method.  If the user presses Cancel on the dialog, then an empty string is returned.

`public static string[] GetArguments()`
> Gets the command-line arguments that were passed into the script.  This array does not include the script file name or any double-backslash parameters because those were processed by SharpScript.

`public static void Cancel()`
> Cancels execution of the script.

`public static void Echo(params object[] arValues)`
> In SharpScript.exe (the GUI version), this displays a message box with each value separated by a newline.  In SharpScriptConsole.exe (the console version), this displays each value in the console window separated by a newline.

`public static void Sleep(int iMilliSeconds)`
> Pauses script execution for the specified number of milliseconds.

## Debugging
SharpScript supports running scripts in debug mode.  If a script is launched with with //D command-line switch, then debug information will be generated for it.  Then Visual Studio or the CLR Debugger can be used to debug your script.

You can easily launch a script in debug mode from Windows Explorer by right-clicking a SharpScript file and selecting “Run Debug”.  That will run the script in debug mode, and the tray icon menu will contain items that allow you to easily attach a debugger or break in the debugger.

## Command Line Switches
SharpScript.exe and SharpScriptConsole.exe support command line switches.  The switches are the same for both programs.

Note: The command-line switches for SharpScript and SharpScriptConsole must begin with double backslashes.  Any switches that begin with a single backslash are passed on to the script as script arguments.

```
Usage: SharpScript ScriptFileName [Options...] [ScriptArgs...]

Options:
//D    Generate debug information for the script.
//Q    Quiet mode.  The GUI version won't show a system tray icon.
//T:nn Time out in seconds: Maximum time a script is permitted to run.
```

Any other command line switches that begin with double backslashes (e.g. //?) will cause a help message to display that lists the valid command line switches.
