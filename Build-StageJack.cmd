@echo off
rem Builds StageJack with the C# compiler that ships with Windows (no Visual Studio needed).
set "ROOT=%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "NETSTANDARD=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\netstandard.dll"
if not exist "%NETSTANDARD%" set "NETSTANDARD=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\netstandard.dll"
if not exist "%ROOT%dist" mkdir "%ROOT%dist"
"%CSC%" /nologo /optimize+ /target:winexe /win32icon:"%ROOT%assets\StageJack.ico" /out:"%ROOT%dist\StageJack.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:"%NETSTANDARD%" /reference:"%ROOT%lib\NAudio.Core.dll" /reference:"%ROOT%lib\NAudio.Wasapi.dll" "%ROOT%src\AssemblyInfo.cs" "%ROOT%src\StageJackApp.cs" "%ROOT%src\StageJackAudio.cs" "%ROOT%src\StageSlider.cs" "%ROOT%src\EqView.cs"
if errorlevel 1 (echo. & echo BUILD FAILED - copy the error above and open an issue. & pause & exit /b 1)
copy /y "%ROOT%assets\fonts\*.ttf" "%ROOT%dist\" >nul
copy /y "%ROOT%assets\fonts\Rajdhani-OFL.txt" "%ROOT%dist\" >nul
copy /y "%ROOT%lib\NAudio.Core.dll" "%ROOT%dist\" >nul
copy /y "%ROOT%lib\NAudio.Wasapi.dll" "%ROOT%dist\" >nul
copy /y "%ROOT%lib\NAudio.LICENSE.txt" "%ROOT%dist\" >nul
echo Build OK: dist\StageJack.exe
