namespace PsftRestWrapper.Models
{
    public sealed class HeartbeatSettings
    {
        public int TimeoutMs { get; set; } = 10_000;
        public int Retries { get; set; } = 0;
    }

}
