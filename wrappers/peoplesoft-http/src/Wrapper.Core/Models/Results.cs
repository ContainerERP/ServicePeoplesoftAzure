 

namespace PsftRestWrapper.Models
{
    public sealed record StepLog(
     string Name,
     bool Ok,
     int Ms,
     string StdOut,
     string StdErr,
     int ExitCode);
}