@echo off
rem ============================================================
rem  KKBlocks: Meshes (.mu) fuer alle Formen neu erzeugen
rem  (Wuerfel, Rampe, Pyramide, Kugel, Zylinder, Kuppel)
rem
rem  Groessen (Meter, kommasepariert) hier anpassen:
set SIZES=0.5,1,2,4,8,16,32
rem ============================================================

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

"%CSC%" /nologo /out:"%~dp0MuTool.exe" /r:System.Drawing.dll "%~dp0MuTool.cs"
if errorlevel 1 (
  echo Kompilieren fehlgeschlagen.
  pause
  exit /b 1
)

"%~dp0MuTool.exe" gen "%~dp0.." %SIZES%

echo.
echo Fertig. Die neuen .mu-Dateien liegen in GameData\KKBlocks.
echo Damit KSP sie laedt, muessen passende STATIC-Eintraege in
echo KKBlocks.cfg existieren (einen vorhandenen Block kopieren und
echo name / mesh / title anpassen).
pause
