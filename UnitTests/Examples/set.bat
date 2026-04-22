@echo off
set MYVAR=hello
set MYVAR
set MYVAR2=world
set MYVAR
set /a RESULT=6*7
echo RESULT=%RESULT%
set /a X=0x1F
echo X=%X%
set /a Y=010
echo Y=%Y%
set /a Z=5^&3
echo Z=%Z%
set MYVAR=
set MYVAR
echo ERRORLEVEL=%ERRORLEVEL%