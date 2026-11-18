@echo off
setlocal
rem ------------------------------------------------------------
rem  Build MusicPlayer without Visual Studio (dotnet CLI)
rem
rem  Usage:
rem    build.cmd            -> Debug build
rem    build.cmd release    -> Release build
rem    build.cmd run        -> Debug build, then start the program
rem    build.cmd clean      -> Delete the build output
rem    build.cmd publish    -> Self-contained release: ONE exe with the whole .NET inside
rem                            (runs on machines without .NET installed)
rem                            Output: publish\MusicPlayer.exe
rem    build.cmd publish run -> the same, then start the published exe
rem ------------------------------------------------------------

cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] The "dotnet" command was not found. Install the .NET SDK:
    echo         https://dotnet.microsoft.com/download
    goto :fail
)

set "CONFIG=Debug"
set "RUN="

if /i "%~1"=="release" set "CONFIG=Release"
if /i "%~1"=="run"     set "RUN=1"
if /i "%~1"=="clean" (
    dotnet clean "MusicPlayer.sln" -nologo -v q
    goto :end
)

rem Only errors are shown; to see warnings too: set SHOWWARN=1 before the call
set "LOGOPT=-clp:ErrorsOnly"
if defined SHOWWARN set "LOGOPT="

if /i "%~1"=="publish" goto :publish

echo === Building MusicPlayer (%CONFIG%) ===
dotnet build "MusicPlayer.sln" -c %CONFIG% -nologo -v minimal %LOGOPT%
if errorlevel 1 (
    echo.
    echo [ERROR] The build failed.
    goto :fail
)

set "EXE=%~dp0MusicPlayer\bin\%CONFIG%\net6.0-windows10.0.19041.0\MusicPlayer.exe"
echo.
echo [OK] Done: %EXE%
if defined RUN (
    echo Starting...
    start "" "%EXE%"
)
goto :end

rem ------------------------------------------------------------
rem  Self-contained release for 64-bit Windows.
rem  - SelfContained: the .NET runtime is included
rem  - PublishSingleFile + IncludeNativeLibrariesForSelfExtract: everything in one exe
rem  - EnableCompressionInSingleFile: smaller file
rem  - no trimming (not supported with WinForms)
rem  The settings (playlist, EQ, theme, LyricsCache) are stored next to the exe.
rem ------------------------------------------------------------
:publish
set "PUBDIR=%~dp0publish"
echo === Publishing MusicPlayer (Release, win-x64, single self-contained exe) ===
if exist "%PUBDIR%\MusicPlayer.exe" del /q "%PUBDIR%\MusicPlayer.exe" >nul 2>&1
dotnet publish "MusicPlayer\MusicPlayer.csproj" -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false ^
    -o "%PUBDIR%" -nologo -v minimal %LOGOPT%
if errorlevel 1 (
    echo.
    echo [ERROR] Publishing failed. ^(The first run needs internet access to download the .NET packages.^)
    goto :fail
)
echo.
echo [OK] Done: %PUBDIR%\MusicPlayer.exe
echo      Copy this single file to any 64-bit Windows 10/11 PC - it runs without .NET installed.
if /i "%~2"=="run" start "" "%PUBDIR%\MusicPlayer.exe"
goto :end

:fail
echo.
pause
exit /b 1

:end
endlocal
