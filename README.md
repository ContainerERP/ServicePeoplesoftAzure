# ServicePeoplesoftAzure + HTTP Wrapper

Migrate & build PeopleSoft projects via simple HTTP steps:  
**EmptyContainer → Compare → CopyProject → Build**  
(_Future_: **EmptyContainer** Undo a migration)

This repo runs **two Dockerized versions** side-by-side:
- **v1** (older Dockerfile) – shell-script only, baseline container test. (../README.md)
- **v2** (current, recommended) – API-driven, Swagger-exposed.
---

## ⚙️ Prerequisites

- Windows 11/Server with **Docker Desktop** (Windows containers).
- **Oracle Client** installed locally at:  
  `C:\Program Files\Oracle Client for Microsoft Tools\`  
  Must include  
  - `oci.dll`
  - `oraociei19.dll`
  - `oraons.dll`
  - `sqlplus.exe`

 **PeopleSoft Client tools**  
  Location: `C:\psft_client\bin\client\winx86\`  
  Must include at minimum:
  - `pside.exe`
  - `psdmt.exe`

  > 💡 To keep container size small, you may also include the Oracle client DLLs here so they travel with the PeopleSoft client folder:
  > - `oci.dll`
  > - `oraociei19.dll`
  > - `oraons.dll`

> ⚠️ We **do not commit vendor binaries**. You must bind-mount your licensed installs at runtime.

## 🚀 Quickstart (5 min)

### 1) Publish the API once (self-contained)

```powershell
dotnet publish .\wrappers\peoplesoft-http\ServicePeoplesoftAzure-Wrapper\src\Wrapper.Api\Wrapper.Api.csproj `
  -c Release -r win-x64 --self-contained true -o .\.out

docker build -f docker/Dockerfile.v2 -t containererp/peoplesoft-wrapper:v0.2.0 --build-arg PUBLISH_DIR=.out .
docker run --rm -p 5000:8080 containererp/peoplesoft-wrapper:v0.2.0

Swagger UI → http://localhost:5000/swagger/index.html
http://localhost:5000/api/migrate/step/Compare
http://localhost:5000/api/migrate/step/Migrate
http://localhost:5000/api/migrate/step/Build
http://localhost:5000/api/migrate/step/EmptyContainer (works but for future release)
 {
  "project": "ISA_TEST2A",
  "sourceServer": "",
  "sourceDb": "DEVL",
  "sourceUser": "PeoplesoftSourceUser",
  "sourcePwd": "PeoplesoftSourcePassword",
  "targetServer": "",
  "targetDb": "FSTST",
  "targetUser": "PeoplesoftTargetUser",,
  "targetPwd": "PeoplesoftTargetpassword", 
  "connectId": "people",
  "connectPwd": "AskDbasbutnoneedofencrypting",
  "workDir": "C:\\temp\\export",
  "dbUser":   "OracleUserID",
  "dbPwd":    "OraclePassword" ,  
  "exportForUndo": false 
}




Firewall: check DB connectivity if timeouts persist. 
ExportForUndo: planned feature to rollback migration by saving EmptyContainer outputs.

## ⚙️ Configuration Notes

- **pside-args.json**  
  Required to pass PeopleSoft Application Designer arguments.  
  Sample parameters and explanations:  
  [Oracle Docs – Application Designer CLI](https://docs.oracle.com/cd/F44947_01/pt858pbr3/eng/pt/tlcm/concept_UnderstandingPeopleSoftApplicationDesignerCommandLineParameters-07741f.html?pli=ul_d102e86_tlcm)

- **ptbld.cfg**  
  Required for `-PJB` (Project Build).  
  A sample file is available at `<PS_HOME>\setup\ptbld.cfg`.

- **appsettings.Container.json**  
  We use the **SQL-based probe approach** (querying DB tables for success/failure) instead of the process exit code approach.  
  - Reason: this repo was developed over a slow VPN; SQL checks were more reliable.  
  - On Azure, you can switch to process-based checks if preferred.  
  - SQL probes always returned consistent results in our testing.

- **Compare Reports**  
  Compare report files are generated and can be viewed outside the API. Not wired into the REST response yet, but easily extended if needed.

 
