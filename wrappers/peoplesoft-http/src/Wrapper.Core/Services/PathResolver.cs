using System.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PsftRestWrapper.Services
{
    public interface IPathResolver
    {
        string ContentRoot { get; }

        // ✅ Add this
        string Resolve(string path);

        // keeps your log helper
        string MakeLogPath(string stem);
    }

    public sealed class PathResolver : IPathResolver
    {
        private readonly IHostEnvironment _env;
        private readonly ILogger<PathResolver> _log;

        public PathResolver(IHostEnvironment env, ILogger<PathResolver> log)
        {
            _env = env;
            _log = log;
        }

        public string ContentRoot => _env.ContentRootPath;

        public string Resolve(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path ?? string.Empty;
            if (Path.IsPathRooted(path)) return path;
            return Path.GetFullPath(Path.Combine(ContentRoot, path));
        }

        public string MakeLogPath(string stem)
        {
            var dir = Path.Combine(ContentRoot, "data", "temp");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"{stem}_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}.log");
        }
    }
}
