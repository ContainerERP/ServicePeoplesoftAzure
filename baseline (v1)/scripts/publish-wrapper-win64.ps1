param(
  [string]$ProjPath = "wrappers\peoplesoft-http\ServicePeoplesoftAzure-Wrapper.csproj",
  [string]$OutDir = "publish\wrapper-win64"
)
dotnet publish $ProjPath -c Release -r win-x64 --self-contained:false -o $OutDir
Write-Host "Published to $OutDir"
