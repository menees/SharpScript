namespace SharpScript
{
	#region Using Directives

	using System;
	using System.Runtime.InteropServices;
	using Menees;

	#endregion

	internal sealed class ConsoleApplication : ScriptApplication
	{
		#region Protected Overrides

		protected override bool InConsole => true;

		protected override void ScriptExecuting(IAsyncResult result)
		{
			result.AsyncWaitHandle.WaitOne();
		}

		protected override void ScriptExecutionFinished()
		{
			// There is nothing to do here.
		}

		protected override void ShowError(string message)
		{
			// If the message doesn't already say "error",
			// then point out to the user that an error occurred.
			if (message.ToLower().IndexOf("error") < 0)
			{
				Console.Write("ERROR: ");
			}

			Console.WriteLine(message);
		}

		protected override void ShowInformation(string message)
		{
			Console.WriteLine(message);
		}

		#endregion

		#region Main Method

		[STAThread]
		private static int Main(string[] args)
		{
			ApplicationInfo.Initialize("SharpScriptConsole");
			ConsoleApplication app = new ConsoleApplication();
			app.InstallCtrlHandler();
			return app.Run(args);
		}

		#endregion

		#region Private Methods

		private void InstallCtrlHandler()
		{
			// This sets up a custom Ctrl+C handler so we can cancel
			// the script execution if necessary.
			Console.CancelKeyPress += new ConsoleCancelEventHandler(this.Console_CancelKeyPress);
		}

		private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			// If Ctrl+C is pressed, we can cancel the cancel if the user wants to.
			// If Ctrl+Break is pressed, we can't cancel the cancel, so don't prompt.
			if (e.SpecialKey == ConsoleSpecialKey.ControlC)
			{
				Console.WriteLine();
				Console.Write(GetResourceText("CancelExecution"));
				Console.Write(" ");
				Console.Write(GetResourceText("YesNoPrompt"));
				Console.Write(": ");

				string yes = GetResourceText("Yes");
				ConsoleKeyInfo keyInfo = Console.ReadKey();
				if (keyInfo.KeyChar != '\0' && string.Compare(new string(keyInfo.KeyChar, 1), yes, true) == 0)
				{
					this.CancelScriptExecution();
				}
				else
				{
					Console.WriteLine();
					e.Cancel = true;
				}
			}
			else if (e.SpecialKey == ConsoleSpecialKey.ControlBreak)
			{
				this.CancelScriptExecution();
			}
		}

		#endregion
	}
}
