@echo off
cd /d "%~dp0"
if exist "play\TranslateUni.exe" (
    "play\TranslateUni.exe"
) else (
    dotnet run --project "%~dp0TranslateUni.csproj" --configuration Release
)
if errorlevel 1 pause
