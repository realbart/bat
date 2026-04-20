@echo off
REM TODO: ENABLEEXTENSIONS / DISABLEEXTENSIONS
set OUTER=before
setlocal
set INNER=inside
echo OUTER=%OUTER%
echo INNER=%INNER%
setlocal enabledelayedexpansion
set X=first
set X=second
echo immediate: %X%
echo delayed: !X!
endlocal
echo after inner endlocal: INNER=%INNER%
endlocal
echo after outer endlocal: OUTER=%OUTER%
echo INNER in outer scope: %INNER%