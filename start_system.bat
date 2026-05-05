@echo off
SETLOCAL

echo ========================================
echo  SkiJobControl - Distributed Job System
echo ========================================
echo.

echo [0/4] Cleaning previous Host/Console processes...
taskkill /F /IM SkiJobControl.Console.exe /T >nul 2>&1
taskkill /F /IM SkiJobControl.Host.exe /T >nul 2>&1
echo Cleanup done.
echo.

echo [1/4] Building solution...
dotnet build "%~dp0SkiJobControl.slnx" -c Debug -v q || (
    echo.
    echo Build FAILED. Please fix errors before running.
    pause
    exit /b 1
)
echo Build successful.
echo.

echo [2/4] Starting Console (Web UI + gRPC :5249/:5250)...
start "SkiJobControl.Console" /d "%~dp0SkiJobControl.Console" cmd /k "dotnet run --no-launch-profile"

echo [3/4] Waiting for Console to initialize (8 seconds)...
timeout /t 8 /nobreak > nul

echo [4/4] Starting Host (Worker pool + gRPC client connecting to Console)...
start "SkiJobControl.Host" /d "%~dp0SkiJobControl.Host" cmd /k "dotnet run --no-launch-profile"

echo Waiting for Host to connect to Console (5 seconds)...
timeout /t 5 /nobreak > nul

echo.
echo ========================================
echo  System started!
echo   Web UI : http://localhost:5249
echo   Swagger: http://localhost:5249/swagger
echo   gRPC   : http://localhost:5250
echo ========================================
echo.
echo Opening Web UI in browser...
start "" "http://localhost:5249"

ENDLOCAL
exit /b 0
