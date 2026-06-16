@echo off
setlocal

rem Start MyProxy Admin UI + Gateway in separate terminals.
rem Requires PostgreSQL with migrations applied (see scripts/reset-postgres-password.ps1).

cd /d "%~dp0"

rem Release MSBuild/compiler locks and free ports from a previous run.
dotnet build-server shutdown >nul 2>&1
powershell -NoProfile -Command "Get-NetTCPConnection -LocalPort 5106,5176 -State Listen -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }" >nul 2>&1

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [.NET SDK not found. Install .NET 10 SDK and try again.]
    pause
    exit /b 1
)

echo Restoring and building solution once...
dotnet restore "%~dp0YarpGateway.sln"
if errorlevel 1 (
    echo Restore failed. Fix restore errors and run again.
    pause
    exit /b 1
)

dotnet build "%~dp0YarpGateway.sln" --no-restore
if errorlevel 1 (
    echo Build failed. Fix build errors and run again.
    pause
    exit /b 1
)

echo Starting MyProxy Admin  (http://localhost:5106) ...
start "MyProxy Admin" cmd /k dotnet run --no-build --project "%~dp0MyProxy.Admin\MyProxy.Admin.csproj" --launch-profile http

echo Starting MyProxy Gateway (http://localhost:5176) ...
start "MyProxy Gateway" cmd /k dotnet run --no-build --project "%~dp0MyProxy\MyProxy.csproj" --launch-profile http

echo.
echo Admin:   http://localhost:5106
echo Gateway: http://localhost:5176
echo.
echo Two terminal windows were opened. Close them to stop the apps.
echo.

timeout /t 4 /nobreak >nul
start "" "http://localhost:5106"

endlocal
