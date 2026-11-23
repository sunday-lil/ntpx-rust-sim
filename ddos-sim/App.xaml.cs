using System;
using System.Windows;

namespace NTPXSim
{
    public partial class App : Application
    {
        private const string Usage =
            "NTPX//RUST " + NTPXSim.MainWindow.BuildVersion + " (sim build) - non-functional visual mock\n" +
            "\n" +
            "usage:\n" +
            "  NTPX-RUST.exe                               launch the console\n" +
            "  NTPX-RUST.exe --snapshot <png> [--state <s>] render one frame offscreen, then exit\n" +
            "  NTPX-RUST.exe --selftest <txt>               run the phase-machine self-test, then exit\n" +
            "  NTPX-RUST.exe --version                      print the build version and exit\n" +
            "  NTPX-RUST.exe --help                         print this text and exit\n" +
            "\n" +
            "  states: engaged (default) | armed | idle | splash\n" +
            "  exit codes: 0 ok | 1 self-test failed | 2 bad arguments";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string snapshotPath = null;
            string state = "engaged";
            string selfTestPath = null;

            for (int i = 0; i < e.Args.Length; i++)
            {
                string raw = e.Args[i] ?? string.Empty;

                switch (raw.ToLowerInvariant())
                {
                    case "--snapshot":
                        if (!TryValue(e.Args, ref i, out snapshotPath)) return;
                        break;

                    case "--state":
                        if (!TryValue(e.Args, ref i, out state)) return;
                        state = state.Trim().ToLowerInvariant();
                        if (!IsKnownState(state))
                        {
                            Fail($"unknown --state '{state}' (expected engaged|armed|idle|splash)");
                            return;
                        }
                        break;

                    case "--selftest":
                        if (!TryValue(e.Args, ref i, out selfTestPath)) return;
                        break;

                    case "--version":
                    case "-v":
                        Console.WriteLine($"NTPX//RUST {NTPXSim.MainWindow.BuildVersion} (sim build)");
                        Shutdown(0);
                        return;

                    case "--help":
                    case "-h":
                    case "/?":
                        Console.WriteLine(Usage);
                        Shutdown(0);
                        return;

                    default:
                        if (raw.StartsWith("-", StringComparison.Ordinal))
                        {
                            Fail($"unknown option '{raw}'");
                            return;
                        }
                        break; // bare tokens are ignored
                }
            }

            if (snapshotPath != null && selfTestPath != null)
            {
                Fail("--snapshot and --selftest cannot be combined");
                return;
            }

            var window = new MainWindow(snapshotPath, state, selfTestPath);
            MainWindow = window;
            window.Show();
        }

        private static bool TryValue(string[] args, ref int index, out string value)
        {
            value = null;
            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
            {
                Fail($"'{args[index]}' needs a value");
                return false;
            }

            value = args[++index];
            return true;
        }

        private static bool IsKnownState(string state) =>
            state == "engaged" || state == "armed" || state == "idle" || state == "splash";

        private static void Fail(string message)
        {
            Console.Error.WriteLine("ntpx: " + message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(Usage);
            Current.Shutdown(2);
        }
    }
}
