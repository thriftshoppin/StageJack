@echo off
if not exist "%~dp0dist\StageJack.exe" call "%~dp0Build-StageJack.cmd"
if exist "%~dp0dist\StageJack.exe" start "" "%~dp0dist\StageJack.exe"
