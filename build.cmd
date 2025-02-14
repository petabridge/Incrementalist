@echo off
PowerShell.exe -NoProfile -ExecutionPolicy Bypass -Command "& {./build.ps1 %*; exit $LASTEXITCODE}"