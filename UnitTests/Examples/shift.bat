@echo off
call :demo one two three four
exit /b 0

:demo
echo before: %%1=%1 %%2=%2 %%3=%3 %%4=%4
shift
echo after shift: %%1=%1 %%2=%2 %%3=%3 %%4=%4
shift /2
echo after shift /2: %%1=%1 %%2=%2 %%3=%3 %%4=%4
exit /b 0