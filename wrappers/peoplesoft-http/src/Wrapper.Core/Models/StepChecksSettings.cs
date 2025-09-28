

// Models/StepChecksSettings.cs
using System.Collections.Generic;
namespace PsftRestWrapper.Models
{
    public sealed class StepChecksSettings
    {
        public int TimeoutMs { get; set; } = 300_000;   // total loop budget
        public int PollMs { get; set; } = 1_000;     // delay between probes

        public int ProbeMs { get; set; } = 20_000;    // <-- add this (per-probe SQL timeout)

        public Dictionary<string, StepCfg> ByStep { get; set; } = new();

        public sealed class StepCfg
        {
            public string Sql { get; set; } = "";
            public string DbRole { get; set; } = "target"; // "source" or "target"
        }
    }
}
