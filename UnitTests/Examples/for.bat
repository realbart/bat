@echo off
:: FOR with a simple set
for %%i in (alpha beta gamma) do echo item: %%i
:: FOR /L counting
for /l %%n in (1,1,5) do echo count: %%n
:: FOR /L reverse
for /l %%n in (3,-1,1) do echo down: %%n
:: FOR with wildcards (use the script itself)
for %%f in ("%~dp0for.bat") do echo file: %%~nxf
:: FOR /F parsing a string
for /f "tokens=1,2,3 delims=," %%a in ("one,two,three") do echo a=%%a b=%%b c=%%c
:: FOR /F with usebackq and a command
for /f "usebackq tokens=*" %%a in (`echo hello from command`) do echo cmd: %%a
:: Nested FOR
for %%x in (A B) do for %%y in (1 2) do echo %%x%%y
