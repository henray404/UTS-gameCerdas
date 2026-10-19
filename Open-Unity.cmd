@echo off
set "UNITY_EDITOR=C:\Program Files\Unity\Hub\Editor\6000.2.10f1\Editor\Unity.exe"
if not exist "%UNITY_EDITOR%" (
  echo Unity 6000.2.10f1 tidak ditemukan. Buka folder project melalui Unity Hub.
  pause
  exit /b 1
)
start "" "%UNITY_EDITOR%" -projectPath "%~dp0."
