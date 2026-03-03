@echo off
echo Starting TechStore.API...
start "TechStore.API" cmd /k "dotnet run --project src\TechStore.API"

echo Starting TechStore.Web...
start "TechStore.Web" cmd /k "dotnet run --project src\TechStore.Web"

echo Both projects are starting in separate windows.
