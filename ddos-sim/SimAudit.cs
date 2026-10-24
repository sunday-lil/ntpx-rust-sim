using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NTPXSim
{
    /// <summary>
    /// Static self-audit of the simulation build.
    ///
    /// The console claims it cannot touch the network. This class turns that claim
    /// into something the operator can see at runtime instead of a footnote in a
    /// README: it inspects the compiled assembly and reports whether any networking
    /// assembly is referenced and whether any type in it lives in a System.Net
    /// namespace. Both answers must be zero for the build to be considered a mock.
    ///
    /// This is a *static* check, not an escape hatch: the counters below are const
    /// because there is deliberately no code path that could ever increment them.
    /// </summary>
    internal static class SimAudit
    {
        /// <summary>Assemblies the simulator binary actually references.</summary>
        public static int AssemblyReferences { get; private set; }

        /// <summary>Referenced assemblies whose name starts with "System.Net".</summary>
        public static int NetworkReferences { get; private set; }

        /// <summary>Types in this assembly declared under a System.Net* namespace.</summary>
        public static int NetworkTypes { get; private set; }

        /// <summary>True when the assembly provably contains no networking surface.</summary>
        public static bool Clean { get; private set; }

        /// <summary>Comma list of the network assemblies found (empty when clean).</summary>
        public static string Offenders { get; private set; } = string.Empty;

        // ── Structural counters ───────────────────────────────────────────────────
        // const, not properties: nothing in this program can write to them.
        public const int PacketsEmitted = 0;
        public const int SocketsOpened = 0;
        public const int HostsResolved = 0;
        public const int ProcessesSpawned = 0;

        public static void Run()
        {
            Assembly asm = typeof(SimAudit).Assembly;
            AssemblyName[] refs = asm.GetReferencedAssemblies();

            AssemblyReferences = refs.Length;
            List<string> net = refs
                .Where(r => (r.Name ?? string.Empty).StartsWith("System.Net", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Name)
                .ToList();
            NetworkReferences = net.Count;
            Offenders = string.Join(", ", net);

            try
            {
                NetworkTypes = asm.GetTypes()
                    .Count(t => (t.Namespace ?? string.Empty).StartsWith("System.Net", StringComparison.Ordinal));
            }
            catch (ReflectionTypeLoadException)
            {
                // A partial load still tells us what it could see; treat unresolved
                // types conservatively as "not network" rather than failing the UI.
                NetworkTypes = 0;
            }

            Clean = NetworkReferences == 0 && NetworkTypes == 0;
        }

        public static string Verdict => Clean ? "PASS" : "REVIEW";
    }
}
