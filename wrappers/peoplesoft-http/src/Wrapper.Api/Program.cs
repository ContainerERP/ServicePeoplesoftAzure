using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PsftRestWrapper.Models;
using PsftRestWrapper.Services;
using System.IO;

// Alias to avoid name clash with Services.StepCheckService
using ModelsStepChecksSettings = PsftRestWrapper.Models.StepChecksSettings;

var builder = WebApplication.CreateBuilder(args);

// --- Settings (match appsettings.json) --------------------------------------
// Psoft (pside/sqlplus paths, temp/report dirs, manifest path)
builder.Services.Configure<PsoftSettings>(builder.Configuration.GetSection("Psoft"));
// Step checks (timeouts/poll/sql by step)
builder.Services.Configure<ModelsStepChecksSettings>(builder.Configuration.GetSection("StepChecks"));

builder.Services
    .AddOptions<ModelsStepChecksSettings>()
    .Bind(builder.Configuration.GetSection("StepChecks"))
    .Validate(s => s != null, "StepChecks section not found")
    .Validate(s => s.ProbeMs > 0 && s.PollMs > 0, "ProbeMs/PollMs must be > 0")
    .Validate(s => s.ByStep != null && s.ByStep.Count > 0, "ByStep must have commands")
    .ValidateOnStart();

// --- Path resolver (rooted at content root; works in containers) ------------
builder.Services.AddSingleton<IPathResolver>(sp =>
{
    var env = sp.GetRequiredService<IHostEnvironment>();
    var log = sp.GetRequiredService<ILogger<PathResolver>>();
    return new PathResolver(env, log);
});

// --- Arg manifest from ./cfg/pside-args.json --------------------------------
builder.Services.AddSingleton<ArgManifest>(sp =>
{
    var log = sp.GetRequiredService<ILogger<ArgManifest>>();
    var env = sp.GetRequiredService<IHostEnvironment>();
    var psoft = sp.GetRequiredService<IOptions<PsoftSettings>>().Value;
    var manifestPath = Path.Combine(env.ContentRootPath, psoft.ArgsManifestPath);
    return ArgManifest.LoadFromFile(manifestPath, log);
});

// --- Core services -----------------------------------------------------------
builder.Services.AddSingleton<ShellRunner>();
builder.Services.AddSingleton<SqlRunner>();
builder.Services.AddSingleton<StepCheckService>();
builder.Services.AddSingleton<MigrationOrchestrator>();
builder.Services.AddSingleton<DbHeartbeatService>(); // optional but useful

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
 

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI(); 

app.MapGet("/diag/stepchecks", (IOptions<StepChecksSettings> o) =>
    Results.Json(new
    {
        o.Value.TimeoutMs,
        o.Value.ProbeMs,
        o.Value.PollMs,
        Steps = o.Value.ByStep?.Keys
    }));

// POST /api/migrate/step/{step}
app.MapPost("/api/migrate/step/{step}", async (
    string step, ProjectMigrationRequest req, MigrationOrchestrator orch, CancellationToken ct) =>
{
    var s = Enum.Parse<StepName>(step, ignoreCase: true);
    var log = await orch.RunStepAsync(s, req, ct);
    return Results.Json(log);
});

// POST /api/migrate  (optional end-to-end wrapper)
app.MapPost("/api/migrate", async (
    ProjectMigrationRequest req, MigrationOrchestrator orch, CancellationToken ct) =>
{
    var result = await orch.RunEndToEndAsync(req, ct);
    return Results.Json(result);
});

app.Run();
