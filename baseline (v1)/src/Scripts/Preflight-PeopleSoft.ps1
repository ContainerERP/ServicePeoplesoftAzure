param(
  # Oracle / SQL*Plus
  [Parameter(Mandatory)] [string]$TnsAlias,
  [Parameter(Mandatory)] [string]$DbUserId,
  [Parameter(Mandatory)] [string]$DbPassword,
  [Parameter(Mandatory)] [string]$SqlplusPath,

  # Data Mover
  [Parameter(Mandatory)] [string]$PsdmtxPath, 
  [string]$DmScriptPath,
  # App Designer (pside.exe)
  [Parameter(Mandatory)] [string]$PsidePath,
  [Parameter(Mandatory)] [string]$PsOperator,
  [Parameter(Mandatory)] [string]$PsOperatorPwd,
  [Parameter(Mandatory)] [string]$ConnectId,
  [Parameter(Mandatory)] [string]$ConnectPwd,

  # PSIDE export
  [Parameter(Mandatory)] [string]$PsIdeExportDir,
  [Parameter(Mandatory)] [string]$PsIdeProject,

  # Output summary JSON
  [Parameter(Mandatory)] [string]$JsonPath
)

# ---------------- helpers ----------------

function Write-Log([string]$msg){ $ts=[DateTime]::Now.ToString("yyyy-MM-dd HH:mm:ss.fff"); Write-Host "[$ts] $msg" }

function Run-Sqlplus {
  param(
    [string]$SqlplusPath,[string]$Conn,[int]$TimeoutSec=30
  )
  Write-Log "=== SQLPLUS: $Conn ==="

  $psi = [Diagnostics.ProcessStartInfo]::new($SqlplusPath,"-L -s $Conn")
  $psi.UseShellExecute        = $false
  $psi.RedirectStandardInput  = $true
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError  = $true
  $psi.CreateNoWindow         = $true
  $p = [Diagnostics.Process]::new()
  $p.StartInfo = $psi
  $null = $p.Start()

  $stdin = $p.StandardInput
  $stdin.NewLine = "`r`n"       # CRLF for sqlplus
  $stdin.WriteLine("whenever sqlerror exit 1")
  $stdin.WriteLine("set heading off feedback off verify off termout on")
  $stdin.WriteLine("select 1 from dual;")
  $stdin.WriteLine("exit")
  $stdin.Close()

  $null = $p.WaitForExit($TimeoutSec*1000)
  if (-not $p.HasExited) { try{$p.Kill()}catch{} }

  return @{
    ok   = ($p.ExitCode -eq 0)
    ms   = 0
    exit = $p.ExitCode
    out  = $p.StandardOutput.ReadToEnd()
    err  = $p.StandardError.ReadToEnd()
  }
}

function Run-Psdmtx {
  param(
    [string]$PsdmtxPath,[string]$Db,[string]$Op,[string]$Pwd,
    [string]$ConnId,[string]$ConnPwd,
    [string]$Dms, [int]$TimeoutSec=120
  )
  Write-Log "=== PSDMTX login test ==="
  $args = @(
    "-CT","ORACLE","-CS","",
    "-CD",$Db,"-CO",$Op,"-CP",$Pwd,
    "-CI",$ConnId,"-CW",$ConnPwd,
    "-FP",$Dms
  ) -join ' '

  $psi = [Diagnostics.ProcessStartInfo]::new($PsdmtxPath,$args)
  $psi.UseShellExecute  = $false
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError  = $true
  $psi.CreateNoWindow   = $true
  $p = [Diagnostics.Process]::new()
  $p.StartInfo = $psi
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $null = $p.Start()
  if (-not $p.WaitForExit($TimeoutSec*1000)) { try{$p.Kill()}catch{} }
  $sw.Stop()

  return @{
    ok   = ($p.ExitCode -eq 0)
    ms   = [int]$sw.Elapsed.TotalMilliseconds
    exit = $p.ExitCode
    out  = $p.StandardOutput.ReadToEnd()
    err  = $p.StandardError.ReadToEnd()
  }
}

function Get-DirSize([string]$Path){
  if (-not (Test-Path $Path)) { return 0L }
  (Get-ChildItem -LiteralPath $Path -Recurse -Force -File -ErrorAction SilentlyContinue |
    Measure-Object -Property Length -Sum).Sum
}

function Wait-ForDirReady {
  param(
    [string]$Path,[int]$AppearTimeoutSec=180,[int]$QuiesceSec=5,[int64]$MinBytes=1024
  )
  $sw=[Diagnostics.Stopwatch]::StartNew()
  while ($sw.Elapsed.TotalSeconds -lt $AppearTimeoutSec) {
    if (Test-Path $Path) {
      $sz = Get-DirSize $Path
      if ($sz -ge $MinBytes) { break }
    }
    Start-Sleep -Milliseconds 500
  }
  if (-not (Test-Path $Path)) { return $false }

  $last=-1; $stable=0
  while ($stable -lt $QuiesceSec) {
    $now = Get-DirSize $Path
    if ($now -eq $last) { $stable++ } else { $stable=0; $last=$now }
    Start-Sleep 1
  }
  return $true
}

function Run-PsideExport {
  param(
    [string]$PsidePath,[string]$Db,[string]$Op,[string]$Pwd,[string]$ConnId,[string]$ConnPwd,
    [string]$Project,[string]$ExportRoot,[string]$LogFile,
    [int]$ProcTimeoutSec=300,[int]$AppearTimeoutSec=180
  )
  Write-Log "=== PSIDE export: $Project -> $ExportRoot ==="

  $args = @(
    "-HIDE","-QUIET","-SS","-SN",
    "-CT","ORACLE","-CS","",
    "-CD",$Db,"-CO",$Op,"-CP",$Pwd,
    "-CI",$ConnId,"-CW",$ConnPwd,
    "-PJTF",$Project,
    "-FP",$ExportRoot,
    "-LF",$LogFile
  ) -join ' '

  # behave like CMD: no redirection
  $psi=[Diagnostics.ProcessStartInfo]::new($PsidePath,$args)
  $psi.UseShellExecute  = $true
  $psi.CreateNoWindow   = $true
  $psi.WorkingDirectory = "C:\"
  $p=[Diagnostics.Process]::new()
  $p.StartInfo = $psi
  $sw=[Diagnostics.Stopwatch]::StartNew()
  $null = $p.Start()
  if (-not $p.WaitForExit($ProcTimeoutSec*1000)) { try{$p.Kill()}catch{} }
  $sw.Stop()

  $exportDir = Join-Path $ExportRoot $Project
  $ready = Wait-ForDirReady -Path $exportDir -AppearTimeoutSec $AppearTimeoutSec -QuiesceSec 5 -MinBytes 1024
  $size  = Get-DirSize $exportDir

  return @{
    ok   = $ready
    ms   = [int]$sw.Elapsed.TotalMilliseconds
    exit = $p.ExitCode
    out  = "export ready: $exportDir (size=$size bytes)"
    err  = ""
  }
}

# ---------------- run checks ----------------

$logName = "preflight_{0:yyyyMMdd_HHmmss}.log" -f (Get-Date)
$logPath = Join-Path $env:TEMP $logName
Write-Log "Log: $logPath"
Write-Log ("Binaries: sqlplus=[{0}] psdmtx=[{1}] pside=[{2}]" -f $SqlplusPath,$PsdmtxPath,$PsidePath)

# 1) sqlplus
$plainConn = "$DbUserId/$DbPassword@$TnsAlias"
$sp = Run-Sqlplus -SqlplusPath $SqlplusPath -Conn $plainConn -TimeoutSec 30
Write-Log "[SQLPLUS] ok=$($sp.ok) ms=$($sp.ms) exit=$($sp.exit) out=$($sp.out.Trim()) err=$($sp.err.Trim())"

# 2) psdmtx (quick login test using any tiny DMS or just a path you know is OK) 
$autoDm = $false
 
if ([string]::IsNullOrWhiteSpace($DmScriptPath)) {
    $DmScriptPath = Join-Path $env:TEMP "dm_login_test.dms"
    $dmScript = @"
SET OUTPUT $DmExportFile;
EXPORT PS_PERSONAL_DATA WHERE EMPLID = 'HELLOWORLD';
"@
    Set-Content -Path $DmScriptPath -Encoding ASCII -Value $dmScript
    $autoDm = $true
    Write-Log "Generated Data Mover test DMS at: $DmScriptPath"
}

$dm = Run-Psdmtx -PsdmtxPath $PsdmtxPath -Db $TnsAlias -Op $PsOperator -Pwd $PsOperatorPwd `
                 -ConnId $ConnectId -ConnPwd $ConnectPwd -Dms  $DmScriptPath -TimeoutSec 60
Write-Log "[PSDMTX] ok=$($dm.ok) ms=$($dm.ms) exit=$($dm.exit) err=$($dm.err.Trim())"

# 3) pside export -> watch directory until stable
$ps = Run-PsideExport -PsidePath $PsidePath -Db $TnsAlias -Op $PsOperator -Pwd $PsOperatorPwd `
                      -ConnId $ConnectId -ConnPwd $ConnectPwd `
                      -Project $PsIdeProject -ExportRoot $PsIdeExportDir `
                      -LogFile (Join-Path $PsIdeExportDir "pside_export.log") `
                      -ProcTimeoutSec 300 -AppearTimeoutSec 180
Write-Log "[PSIDE] ok=$($ps.ok) ms=$($ps.ms) exit=$($ps.exit) details=$($ps.out)"

$summary = @{
  sqlplus = $sp
  psdmtx  = $dm
  pside   = $ps
  tns     = $TnsAlias
  project = $PsIdeProject
  export  = (Join-Path $PsIdeExportDir $PsIdeProject)
  timestamp = (Get-Date).ToString("o")
}

$exitCode = if ($sp.ok -and $dm.ok -and $ps.ok) { 0 } else { 1 }
Write-Log ("summary: sqlplus={0} psdmtx={1} pside={2} -> exit={3}" -f $sp.ok,$dm.ok,$ps.ok,$exitCode)

$summary | ConvertTo-Json -Depth 6 | Set-Content -Path $JsonPath -Encoding UTF8
Write-Log "JSON written: $JsonPath"
exit $exitCode
