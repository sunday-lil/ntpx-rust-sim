using System;
using System.Windows;

namespace NTPXSim
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string snapshotPath = null;
            string state = "engaged";
            string selfTestPath = null;

            for (int i = 0; i < e.Args.Length; i++)
            {
                string arg = (e.Args[i] ?? string.Empty).ToLowerInvariant();
                if (arg == "--snapshot" && i + 1 < e.Args.Length)
                {
                    snapshotPath = e.Args[++i];
                }
                else if (arg == "--state" && i + 1 < e.Args.Length)
                {
                    state = e.Args[++i];
                }
                else if (arg == "--selftest" && i + 1 < e.Args.Length)
                {
                    selfTestPath = e.Args[++i];
                }
            }

            var window = new MainWindow(snapshotPath, state, selfTestPath);
            MainWindow = window;
            window.Show();
        }
    }
}
