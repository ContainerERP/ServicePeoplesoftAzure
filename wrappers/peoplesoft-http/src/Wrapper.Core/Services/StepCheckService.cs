using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PsftRestWrapper.Models;

namespace PsftRestWrapper.Services
{
    public sealed class StepCheckService
    {
        private readonly StepChecksSettings _cfg;
        private readonly SqlRunner _sql;
        private readonly ILogger<StepCheckService> _log;

        // success token we look for in stdout
        private const string ReadyToken = "PSFTAPI_READY";
        // kept for completeness if you ever want to use it in logs
        private const string WaitToken = "PSFTAPI_WAIT";

        public StepCheckService(
            IOptions<StepChecksSettings> cfg,
            SqlRunner sql,
            ILogger<StepCheckService> log)
        {
            _cfg = cfg.Value;
            _sql = sql;
            _log = log;
        }

        /// <summary>
        /// Polls the DB (after PSIDE has been launched by the orchestrator)
        /// until the step-specific SQL prints PSFTAPI_READY on stdout.
        /// </summary>
        public async Task<(bool Ok, int Probes, TimeSpan Elapsed, string Detail)>
            WaitForStepAsync(StepName step, ProjectMigrationRequest req, CancellationToken ct)
        {
            // --- Resolve the step’s config ---
            var stepKey = step.ToString();
            var cfgForStep = GetCfgForStep(stepKey);

            if (cfgForStep == null || string.IsNullOrWhiteSpace(cfgForStep.Sql))
            {
                var msg = $"[validate] ok probes=0 elapsed=0:00:00 detail=No SQL configured for step '{stepKey}'.";
                _log.LogWarning("{Step} {Detail}", step, msg);
                return (false, 0, TimeSpan.Zero, msg);
            }

            // Window (seconds) the SQL wants to use for “newer than” checks
            var secondsWindow = 300; // or keep in settings if you prefer
            var probeSql = GetProbeSql(stepKey, cfgForStep.Sql!, req.Project ?? "", secondsWindow);

            if (string.IsNullOrWhiteSpace(probeSql))
            {
                var msg = $"[validate] ok probes=0 elapsed=0:00:00 detail=SQL for step '{stepKey}' resolved empty.";
                _log.LogWarning("{Step} {Detail}", step, msg);
                return (false, 0, TimeSpan.Zero, msg);
            }

            
             
          /*  var auth = role == "source"
                ? new DbAuth(
                    Db: req.SourceDb ?? req.TargetDb ?? "",
                    User: req.TargetUser ?? req.DbUser ?? "",
                    Pwd: req.SourcePwd ?? req.DbPwd ?? "")
                : new DbAuth(
                    Db: req.TargetDb ?? req.SourceDb ?? "",
                    User: req.TargetUser ?? req.DbUser ?? "",
                    Pwd: req.TargetPwd ?? req.DbPwd ?? "");
          */


            // Pick role (defaults to target)
            var role = (cfgForStep.DbRole ?? "target").Trim().ToLowerInvariant();

            // DB name switches by role; user/pwd always come from DbUser/DbPwd
            // This might change in my case has same userid and password to all environments.
            var db = role == "source"
                ? (req.SourceDb ?? req.TargetDb ?? "")
                : (req.TargetDb ?? req.SourceDb ?? "");

            var user = req.DbUser ?? "";
            var pwd = req.DbPwd ?? "";

            // Build auth
            var auth = new DbAuth(Db: db, User: user, Pwd: pwd);
            // --- Budgets (with sane fallbacks) ---
            var totalCap = TimeSpan.FromMilliseconds(_cfg.TimeoutMs <= 0 ? 300_000 : _cfg.TimeoutMs);
            var pollDelay = TimeSpan.FromMilliseconds(_cfg.PollMs <= 0 ? 1_000 : _cfg.PollMs);
            var probeCap = TimeSpan.FromMilliseconds(_cfg.ProbeMs <= 0 ? 20_000 : _cfg.ProbeMs);

            // --- Poll loop ---
            var sw = Stopwatch.StartNew();
            var probes = 0;
            string lastOut = "", lastErr = "";

            _log.LogDebug("[{Step}] begin polling: totalCap={Total}, probeCap={Probe}, pollDelay={Poll}.",
                step, totalCap, probeCap, pollDelay);

            while (sw.Elapsed < totalCap && !ct.IsCancellationRequested)
            {
                probes++;

                // Respect remaining time per probe
                var remaining = totalCap - sw.Elapsed;
                var thisProbeTO = remaining < probeCap ? remaining : probeCap;
                if (thisProbeTO <= TimeSpan.Zero) break;

                var res = await _sql.RunTextAsync(auth, probeSql, (int)thisProbeTO.TotalMilliseconds, ct);
                lastOut = res.Out ?? string.Empty;
                lastErr = res.Err ?? string.Empty;

                // Success: stdout contains the ready token (case-insensitive)
                if (res.Ok && lastOut.IndexOf(ReadyToken, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var detail = $"[validate] ok probes={probes} elapsed={sw.Elapsed:g} detail=Success";
                    _log.LogInformation("{Step} {Detail}", step, detail);
                    return (true, probes, sw.Elapsed, detail);
                }

                await Task.Delay(pollDelay, ct);
            }

            // Timed out / not ready
            var fail = $"[validate] ok probes={probes} elapsed={sw.Elapsed:g} detail=Timeout/NotReady";
            _log.LogWarning("{Step} {Detail} lastOut='{Out}' lastErr='{Err}'", step, fail, lastOut, lastErr);
            return (false, probes, sw.Elapsed, fail);
        }

        // ---------------- helpers ----------------

        private StepChecksSettings.StepCfg? GetCfgForStep(string stepKey)
        {
            if (_cfg?.ByStep == null) return null;
            return _cfg.ByStep.TryGetValue(stepKey, out var cfg) ? cfg : null;
        }

        /// <summary>
        /// Load the step’s SQL (inline or file:relative/path.sql) and replace tokens.
        /// </summary>
        private static string GetProbeSql(string stepKey, string raw, string project, int seconds)
        {
            string text = raw;

            if (text.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            {
                var rel = text.Substring("file:".Length).Trim();
                // Resolve under the app base directory so it works on Windows and in containers
                var baseDir = AppContext.BaseDirectory;
                var path = Path.IsPathRooted(rel) ? rel : Path.Combine(baseDir, rel);
                if (File.Exists(path))
                {
                    text = File.ReadAllText(path);
                }
                else
                {
                    // keep it empty so the caller treats as “not configured”
                    return string.Empty;
                }
            }

            // Token replacement
            text = text
                .Replace("{Project}", project ?? "", StringComparison.OrdinalIgnoreCase)
                .Replace("{Seconds}", seconds.ToString(), StringComparison.OrdinalIgnoreCase);

            return text;
        }
    }
}
