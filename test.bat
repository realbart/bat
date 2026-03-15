@echo off
echo Batch test started
:LABEL1
echo Inside LABEL1
if "a"=="a" echo a is a
if not "a"=="b" echo a is not b
if exist autoexec.bat (
  echo autoexec.bat exists
) else (
  echo autoexec.bat does not exist
)
goto :LABEL2
echo This should be skipped
:LABEL2
echo Inside LABEL2
if errorlevel 0 echo Errorlevel is 0 or more
goto :EOF
echo This should also be skipped
