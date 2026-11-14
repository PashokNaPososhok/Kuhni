@echo off
chcp 65001 >nul
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
 echo Установите .NET 10 SDK и повторите запуск.
 pause
 exit /b 1
)
dotnet run --no-launch-profile -- --admin
pause
