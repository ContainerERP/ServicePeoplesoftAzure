
# Base image
FROM mcr.microsoft.com/windows/server:ltsc2025

# Use PowerShell for convenience during build
SHELL ["powershell","-NoLogo","-NoProfile","-Command"]

# --- Oracle client & config ---
# Copy your Instant Client folder (depends if you want SQL plus copy everything but need peoplesoft just 3 dlls)
COPY OracleClient/ C:/OracleClient/
COPY psft_portable/ C:/psft_portable/
# (Docker will create intermediate dirs)

# --- Tools / scripts ---
RUN New-Item -ItemType Directory -Path C:\tools | Out-Null
COPY Test-OracleConnectivity.ps1 C:/tools/Test-OracleConnectivity.ps1

# --- Environment ---
# Backslashes + include existing PATH. 
ENV TNS_ADMIN=C:/OracleClient/network/admin
ENV PATH="C:/OracleClient;C:/psft_portable; 
ENV ORACLE_HOME=C:/OracleClient

# (Optional) Working dir
WORKDIR C:/tools 
