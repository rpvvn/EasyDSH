@echo off
setlocal

REM Find .NET Framework 4.0 compiler (csc.exe)
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set FRAMEWORK=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set WPF=%FRAMEWORK%\WPF

if not exist "%CSC%" (
    set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
    set FRAMEWORK=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
    set WPF=%FRAMEWORK%\WPF
)

if not exist "%CSC%" (
    echo [ERROR] .NET Framework 4.0 compiler not found.
    exit /b 1
)

"%CSC%" /nologo /codepage:65001 /target:winexe /win32icon:icon.ico /out:DSH-Launcher.exe ^
  /r:"%WPF%\PresentationCore.dll" ^
  /r:"%WPF%\PresentationFramework.dll" ^
  /r:"%WPF%\WindowsBase.dll" ^
  /r:"%FRAMEWORK%\System.Xaml.dll" ^
  /r:"%FRAMEWORK%\System.Drawing.dll" ^
  /r:"%FRAMEWORK%\System.Management.dll" ^
  DshLauncher.cs

if errorlevel 1 (
    echo [ERROR] Build failed.
    exit /b 1
)

echo [OK] DSH-Launcher.exe built successfully.
