using System.Collections.Generic;
using PsftRestWrapper.Services;

namespace PsftRestWrapper.Models
{
    /// <summary>
    /// Minimal adapter so existing code that depends on Models.Manifest keeps working.
    /// Internally delegates to Services.ArgManifest.
    /// </summary>
    public sealed class Manifest
    {
        private readonly ArgManifest _inner;

        public Manifest(ArgManifest inner) => _inner = inner;

        /// <summary>
        /// Returns a fully expanded, verbatim PSIDE argument string for a given manifest key.
        /// </summary>
        public string GetArgs(string stepKey, IDictionary<string, string> tokens) =>
            _inner.GetArgs(stepKey, tokens);
    }
}
