
using global::PsftRestWrapper.Models;
using Microsoft.Extensions.Options;


namespace PsftRestWrapper.Services
{
 

    public sealed class DbHeartbeatService
    {
        private readonly ShellRunner _sh;
        private readonly ILogger<DbHeartbeatService> _log;
        private readonly PsoftSettings _cfg;  // for SqlPlusPath + TempDir
        private readonly IPathResolver _paths;
        public DbHeartbeatService(ShellRunner sh, IOptions<PsoftSettings> cfg, IPathResolver paths, ILogger<DbHeartbeatService> log)
        { _sh = sh; _cfg = cfg.Value; _paths = paths; _log = log; }

        /// <summary>
        /// Quick probe to verify the target DB is reachable and auth works.
        /// </summary>
        public async Task<(bool Ok, string Error)> CheckTargetAsync(ProjectMigrationRequest r, int timeoutMs, CancellationToken ct = default)
        {
            var tempDir = _paths.Resolve(_cfg.TempDir);
            Directory.CreateDirectory(tempDir);
            var sqlPath = Path.Combine(tempDir, $"hb_{Guid.NewGuid():N}.sql");

            var script = """
            WHENEVER SQLERROR EXIT 1
            set heading off feedback off verify off pagesize 0 termout off echo off
            select 1 from dual;
            exit
            """;
            await File.WriteAllTextAsync(sqlPath, script, ct);

            try
            {
                var args = $"-L -s {r.DbUser}/{r.DbPwd}@{r.TargetDb} @{sqlPath}";
                /*var res = await _sh.RunAsync(_cfg.SqlPlusPath, args, timeoutMs, ct);*/

                var sqlPlus = _paths.Resolve(_cfg.SqlPlusPath);
                var res = await _sh.RunAsync(sqlPlus, args, timeoutMs, ct);

                if (!res.Ok)
                {
                    var reason = string.IsNullOrWhiteSpace(res.Err) ? res.Out : res.Err;
                    return (false, $"sqlplus heartbeat failed: {reason}".Trim());
                }
                return (true, "");
            }
            catch (OperationCanceledException)
            {
                return (false, "Heartbeat canceled or timed out.");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Heartbeat exception.");
                return (false, ex.Message);
            }
            finally
            {
                try { File.Delete(sqlPath); } catch { /* best effort */ }
            }
        }
    }

}
