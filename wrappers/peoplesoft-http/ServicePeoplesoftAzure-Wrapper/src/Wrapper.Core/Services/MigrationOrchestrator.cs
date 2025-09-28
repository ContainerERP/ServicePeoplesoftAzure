
using Microsoft.Extensions.Options;
using PsftRestWrapper.Models; 
using ModelsStepChecksSettings = PsftRestWrapper.Models.StepChecksSettings;
namespace PsftRestWrapper.Services
{
    public class MigrationOrchestrator
    {
        private readonly ShellRunner _sh;
        private readonly StepCheckService _check;
        private readonly IPathResolver _paths;
        private readonly PsoftSettings _psoft;
        private readonly ModelsStepChecksSettings  _checks;
        private readonly ArgManifest _manifest;
        private readonly ILogger<MigrationOrchestrator> _log;

        public MigrationOrchestrator(
            ShellRunner sh,
            StepCheckService check,
            IPathResolver paths,
           IOptions<PsoftSettings> psoft,
           IOptions<ModelsStepChecksSettings> checks,
            ArgManifest manifest,
            ILogger<MigrationOrchestrator> log)
        {
            _sh = sh;
            _check = check;
            _paths = paths;
            _psoft = psoft.Value;        // Psoft.PsidePath, ArgsManifestPath, dirs, etc. :contentReference[oaicite:6]{index=6}
            _checks = checks.Value;      // StepChecks.* (timeouts/poll/sql)                       :contentReference[oaicite:7]{index=7}
            _manifest = manifest;        // Arg templates (EmptyContainer, copyProject, …)         :contentReference[oaicite:8]{index=8}
            _log = log;
        }

        // Expand once per step, start PSIDE, poll DB, cancel PSIDE best-effort, return DB truth.
        public async Task<StepLog> RunStepAsync(
            StepName step,
            ProjectMigrationRequest req,
            CancellationToken ct = default)
        {
            // 1) Expand args from manifest
            if (!TryExpandArgs(step, req, out var args))
                return new StepLog(step.ToString(), false, 0, "No PSIDE args configured.", "", -1);

            // 2) Fire PSIDE (do NOT await) — we poll DB instead
            using var shellCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var budgetMs = _checks.TimeoutMs > 0 ? _checks.TimeoutMs : 8 * 60 * 1000; // safety
            var psideExe = _paths.Resolve(_psoft.PsidePath); // e.g. psft_portable/pside.exe :contentReference[oaicite:9]{index=9}
            var shellTask = _sh.RunAsync(psideExe, args, budgetMs, shellCts.Token, _paths.ContentRoot);

            // 3) DB validation (single source of truth)
            var (ok, detail, probes, elapsed) = await _check.WaitForStepAsync(step, req, ct);

            // 4) Stop PSIDE if still running (best effort)
            try { shellCts.Cancel(); } catch { }

            // 5) Return (DB result only; PSIDE success/failure is ignored on purpose)
            var outMsg = $"[validate] ok probes={probes} elapsed={elapsed:g} detail={detail}";
            return new StepLog(step.ToString(), ok, 0, outMsg, "", 0);
        }

        public async Task<IReadOnlyList<StepLog>> RunEndToEndAsync(
            ProjectMigrationRequest req,
            CancellationToken ct = default)
        {
            var steps = new[] { StepName.CopyProject, StepName.Compare, StepName.Build };
            var logs = new List<StepLog>();
            foreach (var s in steps)
            {
                var log = await RunStepAsync(s, req, ct);
                logs.Add(log);
                if (!log.Ok) break;
            }
            return  logs;
        }

        // --- helpers ---------------------------------------------------------

        private bool TryExpandArgs(StepName step, ProjectMigrationRequest req, out string args)
        {
            args = "";

            // Choose manifest key (support both "CopyProject" and "copyProject" in JSON).
            string manifestKey = step switch
            {
                StepName.EmptyContainer => "EmptyContainer",
                StepName.CopyProject => "CopyProject",
                StepName.Compare => "Compare",
                StepName.Build => "Build",
                _ => step.ToString()
            };

            // Variables available to templates
            var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Project"] = req.Project,
                ["SourceDb"] = req.SourceDb ?? "",
                ["SourceUser"] = req.SourceUser ?? "",
                ["SourcePwd"] = req.SourcePwd ?? "",
                ["TargetDb"] = req.TargetDb ?? "",
                ["TargetUser"] = req.TargetUser ?? "",
                ["TargetPwd"] = req.TargetPwd ?? "",
                ["ConnectId"] = req.ConnectId ?? "",
                ["ConnectPwd"] = req.ConnectPwd ?? "",
                ["ReportDir"] = _paths.Resolve(_psoft.ReportDir),
                ["ExportDir"] = _paths.Resolve(_psoft.ExportDir),
                ["BuildCfg"] = _paths.Resolve(_psoft.BuildCfgPath),
                ["CopyLog"] = _paths.MakeLogPath("Copy"),
                ["CompareLog"] = _paths.MakeLogPath("Compare"),
                ["BuildLog"] = _paths.MakeLogPath("Build")
            };

            // Try the canonical key first, then a lower-case variant to match "copyProject".
            if (!_manifest.TryExpand(manifestKey, vars, out var expanded) &&
     !_manifest.TryExpand(manifestKey.ToLowerInvariant(), vars, out expanded))
            {
                return false;
            }

            args = string.Join(" ", expanded);  // pass-through 
            _log.LogDebug("Step {Step} args => {Args}", step, args);
            return true;
             
            
        }
    }
}

