@echo off
rem One-click self-contained release: publish\MusicPlayer.exe (with the whole .NET inside).
rem Same as: build.cmd publish
call "%~dp0build.cmd" publish %*
pause
