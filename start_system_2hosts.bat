@echo off
SETLOCAL

echo ===============================================
echo  SkiJobControl - 2 Hosts Simulation Startup
echo ===============================================
echo.

echo [0/5] Cleaning previous Host/Console processes...
taskkill /F /IM SkiJobControl.Console.exe /T >nul 2>&1
taskkill /F /IM SkiJobControl.Host.exe /T >nul 2>&1
taskkill /F /IM SkiJobControl.Worker.exe /T >nul 2>&1
echo Cleanup done.
echo.

echo [1/5] Building solution...
dotnet build "%~dp0SkiJobControl.slnx" -c Debug -v q || (
    echo.
    echo Build FAILED. Please fix errors before running.
    pause
    exit /b 1
)
echo Build successful.
echo.

echo [2/5] Starting Console (Web UI + gRPC :5249/:5250)...
start "SkiJobControl.Console" /d "%~dp0SkiJobControl.Console" cmd /k "dotnet run --no-launch-profile"

echo [3/5] Waiting for Console to initialize (8 seconds)...
timeout /t 8 /nobreak > nul

echo [4/5] Starting Host #1 ...
start "SkiJobControl.Host.1" /d "%~dp0SkiJobControl.Host" cmd /k "dotnet run --no-launch-profile"

echo [5/5] Starting Host #2 ...
start "SkiJobControl.Host.2" /d "%~dp0SkiJobControl.Host" cmd /k "dotnet run --no-launch-profile"

echo Waiting for hosts to connect (6 seconds)...
timeout /t 6 /nobreak > nul

echo.
echo ===============================================
echo  2-Host simulation started!
echo   Web UI : http://localhost:5249
echo   Swagger: http://localhost:5249/swagger
echo   gRPC   : http://localhost:5250
echo.
echo  On Web UI, click "產生模擬 Job" to enqueue jobs.
echo  You should see both hosts picking jobs and worker logs.
echo ===============================================
echo.

echo Opening Web UI in browser...
start "" "http://localhost:5249"

ENDLOCAL
exit /b 0
