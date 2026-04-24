@echo off
:: Basic string comparison
if "hello"=="hello" echo equal strings: yes
if "hello"=="world" (echo should not print) else echo unequal strings: correct
:: Case insensitive
if /i "HELLO"=="hello" echo case insensitive: yes
:: NOT
if not "a"=="b" echo not equal: correct
:: DEFINED
set TESTVAR=exists
if defined TESTVAR echo defined: yes
if defined NOSUCHVAR (echo should not print) else echo not defined: correct
:: EXIST
if exist "%~f0" echo file exists: yes
if exist "Z:\no_such_file_ever.xyz" (echo should not print) else echo file not exists: correct
:: ERRORLEVEL
cmd /c exit /b 0
if errorlevel 1 (echo should not print) else echo errorlevel 0: correct
cmd /c exit /b 5
if errorlevel 5 echo errorlevel 5: yes
if errorlevel 6 (echo should not print) else echo errorlevel not 6: correct
:: Nested if
if "a"=="a" if "b"=="b" echo nested: yes
:: EQU NEQ LSS LEQ GTR GEQ
if 5 EQU 5 echo equ: yes
if 5 NEQ 3 echo neq: yes
if 3 LSS 5 echo lss: yes
if 5 LEQ 5 echo leq: yes
if 7 GTR 5 echo gtr: yes
if 5 GEQ 5 echo geq: yes
