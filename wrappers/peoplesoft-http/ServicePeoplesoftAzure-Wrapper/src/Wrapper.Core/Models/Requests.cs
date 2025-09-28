
namespace PsftRestWrapper.Models;
public sealed record ProjectMigrationRequest(
    string Project,
    string SourceDb,
    string SourceUser,
    string SourcePwd,
    string TargetDb,
    string TargetUser,
    string TargetPwd,
    string ConnectId,
    string ConnectPwd,
    string DbUser,
   string DbPwd,
    bool ExportForUndo = true
);
