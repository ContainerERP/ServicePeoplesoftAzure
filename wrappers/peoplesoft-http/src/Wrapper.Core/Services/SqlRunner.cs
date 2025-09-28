using Microsoft.Extensions.Options;
using PsftRestWrapper.Models;

namespace PsftRestWrapper.Services
{
    /// <summary>
    /// Runs ad-hoc SQL through SQL*Plus and returns raw stdout/stderr.
    /// </summary>
    public sealed class SqlRunner
    {
        private readonly IPathResolver _paths;
        private readonly ShellRunner _sh;
        private readonly PsoftSettings _cfg;
        private readonly ILogger<SqlRunner> _log;

        public SqlRunner(
            IPathResolver paths,
            ShellRunner sh,
            IOptions<PsoftSettings> cfg,
            ILogger<SqlRunner> log)
        {
            _paths = paths;
            _sh = sh;
            _cfg = cfg.Value;
            _log = log;
        }

        /// <summary>
        /// Execute a snippet with SQL*Plus and return raw process output.
        /// The snippet should include a final SELECT or text you want to read on stdout.
        /// </summary>
        /// <param name="auth">DB connect (Db, User, Pwd)</param>
        /// <param name="selectSql">SQL to run (we'll add session settings and EXIT)</param>
        /// <param name="timeoutMs">Per-execution timeout</param>
        public async Task<ShellRunner.Result> RunTextAsync(
            DbAuth auth,
            string selectSql,
            int timeoutMs,
            CancellationToken ct)
        {
            // Temp script under content-root/data/temp
            var tmpDir = _paths.Resolve("data/temp");
            Directory.CreateDirectory(tmpDir);

            var sqlPath = Path.Combine(tmpDir, $"chk_{Guid.NewGuid():N}.sql");

            // Keep the session quiet; leave results visible on stdout so we can parse
            var script = $@"
WHENEVER SQLERROR EXIT 2
SET HEADING OFF FEEDBACK OFF VERIFY OFF PAGESIZE 0 ECHO OFF TRIMSPOOL ON
SET TERMOUT ON
{selectSql}
EXIT
";

            await File.WriteAllTextAsync(sqlPath, script, ct);

            try
            {
                // sqlplus -s -L user/pwd@db @<file.sql>
                var exe = _paths.Resolve(_cfg.SqlPlusPath); 
                var conn = $"{auth.User}/{auth.Pwd}@{auth.Db}";
                var args = $"-s -L {conn} @{sqlPath}";

                _log.LogDebug("SQL*Plus exe='{Exe}', args='{Args}'", exe, args);
                var res = await _sh.RunAsync(exe, args, timeoutMs, ct);

                // Caller decides success by inspecting stdout (e.g., token 'PSFTAPI_OK')
                return res;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "SQL*Plus execution failed.");
                return new ShellRunner.Result(false, "", ex.Message, -1, 0);
            }
            finally
            {
                // Best-effort cleanup
                try { File.Delete(sqlPath); } catch { /* ignore */ }
            }
        }
    }
}

