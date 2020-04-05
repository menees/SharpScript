namespace SharpScript
{
	#region Using Directives

	using System;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Drawing;
	using System.IO;
	using System.Reflection;
	using System.Windows.Forms;
	using Menees;
	using Menees.Windows.Forms;

	#endregion

	[SuppressMessage(
		"Microsoft.Design",
		"CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
		Justification = "The disposable members are cleaned up elsewhere.")]
	internal sealed class WindowsApplication : ScriptApplication
	{
		#region Private Data Members

		private NotifyIcon trayIcon;
		private MenuItem attachMenu;

		#endregion

		#region Protected Overrides

		protected override bool InConsole => false;

		protected override void ScriptExecuting(IAsyncResult result)
		{
			if (this.Quiet)
			{
				ScriptExecutingCore();
			}
			else
			{
				this.ScriptExecutingGUI();
			}
		}

		protected override void ScriptExecutionFinished()
		{
			Application.Exit();
		}

		protected override void ShowError(string message)
		{
			MessageBox.Show(message, ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
		}

		protected override void ShowInformation(string message)
		{
			MessageBox.Show(message, ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		#endregion

		#region Main Method

		[STAThread]
		private static int Main(string[] args)
		{
			WindowsUtility.InitializeApplication(nameof(SharpScript), null);
			WindowsApplication app = new WindowsApplication();
			return app.Run(args);
		}

		#endregion

		#region Private Methods

		private static void ScriptExecutingCore()
		{
			// Pump messages until ScriptExecutionFinished calls Application.Exit.
			Application.Run();
		}

		private static void Help_Click(object sender, EventArgs e)
		{
			try
			{
				// TODO: Make this open https://github.com/bmenees/SharpScript. [Bill, 4/4/2020]
				string fileName = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".docx");
				WindowsUtility.ShellExecute(null, fileName);
			}
			catch (InvalidOperationException)
			{
			}
			catch (Win32Exception)
			{
			}
		}

		private static void About_Click(object sender, EventArgs e)
		{
			WindowsUtility.ShowAboutBox(null, Assembly.GetExecutingAssembly());
		}

		private void CancelExecution_Click(object sender, EventArgs e)
		{
			if (MessageBox.Show(GetResourceText("CancelExecution"), ApplicationName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
			{
				// Clean up the tray icon before we cancel the script.  If the
				// cancel process has to forcibily terminate the process, I don't
				// want to leave an orphaned tray icon.  This also has the nice
				// side-effect of not letting them click cancel twice.
				if (this.trayIcon != null)
				{
					this.trayIcon.Visible = false;
				}

				this.CancelScriptExecution();
			}
		}

		private void AttachDebugger_Click(object sender, EventArgs e)
		{
			this.AttachDebugger();
		}

		private void BreakInDebugger_Click(object sender, EventArgs e)
		{
			this.BreakInDebugger();
		}

		private void Context_Popup(object sender, EventArgs e)
		{
			if (this.attachMenu != null)
			{
				this.attachMenu.Enabled = !this.IsDebuggerAttached;
			}
		}

		private void ScriptExecutingGUI()
		{
			// Use a different tray icon for each file type.
			string iconResourceName;
			switch (this.ScriptType)
			{
				case ScriptType.VB:
					iconResourceName = "Images.TrayIconVB.ico";
					break;
				default:
					iconResourceName = "Images.TrayIcon.ico";
					break;
			}

			using (this.trayIcon = new NotifyIcon())
			using (Icon icon = new Icon(typeof(WindowsApplication), iconResourceName))
			using (ContextMenu mnuContext = new ContextMenu())
			{
				// Setup the context menu.
				mnuContext.MenuItems.Add(GetResourceText("CancelExecutionMenuItem"), this.CancelExecution_Click);
				mnuContext.MenuItems[0].DefaultItem = true;
				if (this.Debug)
				{
					mnuContext.MenuItems.Add("-");
					this.attachMenu = new MenuItem(GetResourceText("AttachDebuggerMenuItem"), this.AttachDebugger_Click);
					mnuContext.MenuItems.Add(this.attachMenu);
					mnuContext.MenuItems.Add(GetResourceText("BreakInDebuggerMenuItem"), this.BreakInDebugger_Click);
				}

				mnuContext.MenuItems.Add("-");
				mnuContext.MenuItems.Add(GetResourceText("HelpMenuItem"), Help_Click);
				mnuContext.MenuItems.Add(GetResourceText("AboutMenuItem"), About_Click);

				mnuContext.Popup += this.Context_Popup;

				// Setup the tray icon.
				this.trayIcon.Icon = icon;

				// Show the process ID in the ToolTip in case the user
				// is running multiple instances of the same script.
				this.trayIcon.Text = $"{ApplicationName} - {this.ScriptName} ({ApplicationInfo.ProcessId})";
				this.trayIcon.ContextMenu = mnuContext;
				this.trayIcon.Visible = true;
				this.trayIcon.DoubleClick += this.CancelExecution_Click;

				// Now that the GUI is displayed, run the core logic.
				ScriptExecutingCore();
			}

			// Once we dispose of the tray icon, we need to let it go.
			this.trayIcon = null;
			this.attachMenu = null;
		}

		#endregion
	}
}
