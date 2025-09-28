namespace PsftRestWrapper.Models;

public sealed class PsoftSettings
{
    public string PsidePath { get; set; } = @"psft_portable\pside.exe";
    public string SqlPlusPath { get; set; } = @"OracleClient\sqlplus.exe";
    public string TempDir { get; set; } = @"data\temp";
    public string ReportDir { get; set; } = @"data\reports";
    public string ExportDir { get; set; } = @"data\export";
    public string BuildCfgPath { get; set; } = @"data\ptbld.cfg";
    public string ArgsManifestPath { get; set; } = @"cfg\pside-args.json";
    public int TimeoutMs { get; set; } = 900000;
    public string SqlCheckScript { get; set; } = string.Empty;
}
