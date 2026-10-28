@echo off
rem Builds StageJack with the C# compiler that ships with Windows (no Visual Studio needed).
set "ROOT=%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%ROOT%dist" mkdir "%ROOT%dist"
"%CSC%" /nologo /optimize+ /target:winexe /out:"%ROOT%dist\StageJack.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll "%ROOT%src\StageJackApp.cs" "%ROOT%src\StageJackAudio.cs" "%ROOT%src\StageSlider.cs"
if errorlevel 1 (echo. & echo BUILD FAILED - copy the error above and open an issue. & pause & exit /b 1)
copy /y "%ROOT%assets\fonts\*.ttf" "%ROOT%dist\" >nul
copy /y "%ROOT%assets\fonts\Rajdhani-OFL.txt" "%ROOT%dist\" >nul
echo Build OK: dist\StageJack.exe
