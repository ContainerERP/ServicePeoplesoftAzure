using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PsftRestWrapper.Services
{
    /// <summary>
    /// Loads PSIDE argument arrays from pside-args.json and produces a single, verbatim args string.
    /// No extra quoting/escaping is performed here by design (keep v1 simple).
    /// </summary>
    public sealed class ArgManifest
    {
        private readonly Dictionary<string, List<string>> _map;
        private readonly ILogger<ArgManifest> _log;

        public ArgManifest(Dictionary<string, List<string>> map, ILogger<ArgManifest> log)
        {
            _map = map ?? new();
            _log = log;
        }

        public static ArgManifest LoadFromFile(string path, ILogger<ArgManifest> log)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("Arg manifest file not found.", path);

            var json = File.ReadAllText(path);
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            var map = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json, opts)
                      ?? new Dictionary<string, List<string>>();

            log.LogInformation("Loaded ArgManifest: {Count} step(s) from {Path}", map.Count, path);
            return new ArgManifest(map, log);
        }

        /// <summary>
        /// Returns fully expanded argument string for the given step key.
        /// e.g. stepKey: "Compare" or "CopyProject" matching pside-args.json
        /// </summary>
        public string GetArgs(string stepKey, IDictionary<string, string> tokens)
        {
            if (string.IsNullOrWhiteSpace(stepKey))
                return string.Empty;

            if (!_map.TryGetValue(stepKey, out var parts) || parts is null || parts.Count == 0)
            {
                _log.LogWarning("No args configured for step '{StepKey}'.", stepKey);
                return string.Empty;
            }

            return string.Join(" ", parts.Select(p => ReplaceTokens(p, tokens)));
        }
        public bool TryExpand(string key, IReadOnlyDictionary<string, string> vars, out List<string> args)
        {
            args = default!;
            if (!_map.TryGetValue(key, out var tmpl)) return false;

            args = new List<string>(tmpl.Count);
            foreach (var token in tmpl)
            {
                var s = token;
                foreach (var kv in vars)
                    s = s.Replace($"{{{kv.Key}}}", kv.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                args.Add(s);
            }
            return args.Count > 0;
        }
        private static string ReplaceTokens(string input, IDictionary<string, string> tokens)
        {
            if (string.IsNullOrEmpty(input) || tokens is null || tokens.Count == 0)
                return input;

            foreach (var kv in tokens)
            {
                var token = "{" + kv.Key + "}";
                input = input.Replace(token, kv.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            return input;
        }
    }
}
