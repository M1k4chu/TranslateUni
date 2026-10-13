@echo off
cd /d "%~dp0"
if exist "play\BusinessGameV2.exe" (
    "play\BusinessGameV2.exe"
) else (
    dotnet run --project "%~dp0BusinessGameV2.csproj" --configuration Release
)
if errorlevel 1 pause
