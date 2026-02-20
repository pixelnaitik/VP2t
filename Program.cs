using System;
using System.Windows.Forms;
using VPT.Core;
using VPT.Forms;

namespace VPT;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        try
        {
            Logger.Log("Starting application.");
            ApplicationConfiguration.Initialize();
            Logger.Log("Application configuration initialized.");
            Application.Run(new Form1());
            Logger.Log("Application shutdown complete.");
        }
        catch (Exception ex)
        {
            Logger.Error("Unhandled fatal crash", ex);
            MessageBox.Show(ex.ToString(), "Crash", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
