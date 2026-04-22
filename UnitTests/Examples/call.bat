@echo off
echo before call
call :sub hello world
echo after call
call :sub again
echo done
exit /b 0

:sub
echo sub arg1=%1 arg2=%2
exit /b 0