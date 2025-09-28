using System.Diagnostics;

namespace PsftRestWrapper.Services
{
    public class ShellRunner
    {
        private readonly ILogger<ShellRunner> _log;
        private readonly IPathResolver _paths;

        public ShellRunner(ILogger<ShellRunner> log, IPathResolver paths)
        {
            _log = log;
            _paths = paths;
        }

        public record Result(bool Ok, string Out, string Err, int ExitCode, int Ms);
        public async Task<Result> RunAsync(
       string file,
       string args,
       int timeoutMs,
       CancellationToken ct,
       string? workingDir = null)
        {
            var exe = _paths.Resolve(file);
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,              // pass through AS-IS (no extra quoting)
                WorkingDirectory = workingDir ?? _paths.ContentRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            
            };

            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var sw = Stopwatch.StartNew();
            p.Start();

            // Read both pipes asynchronously to avoid deadlocks
            var outTask = p.StandardOutput.ReadToEndAsync();
            var errTask = p.StandardError.ReadToEndAsync();

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(Math.Max(1000, timeoutMs)); // safety floor

            try
            {
                await p.WaitForExitAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { /* best effort */ }
            }

            var outText = await outTask;
            var errText = await errTask;

            sw.Stop();
            var ok = p.ExitCode == 0;
            return new Result(ok, outText, errText, p.ExitCode, (int)sw.ElapsedMilliseconds);
        }

        private void TryKill(Process p)
        {
            try { if (!p.HasExited) { p.Kill(entireProcessTree: true); p.WaitForExit(2000); } }
            catch { /* best effort */ }
        }
    }
}
