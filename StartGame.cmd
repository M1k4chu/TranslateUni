@echo off
cd /d "%~dp0"
if exist "play\BusinessGameV3.exe" (
    "play\BusinessGameV3.exe"
) else (
    dotnet run --project "%~dp0BusinessGameV3.csproj" --configuration Release
)
if errorlevel 1 pause
