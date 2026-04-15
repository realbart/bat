@echo off
echo before call inner
call :inner
echo after call inner, errorlevel=%ERRORLEVEL%
exit /b 0

:inner
echo in inner
exit /b 42
echo never reached