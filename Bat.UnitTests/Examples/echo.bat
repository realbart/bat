:: This script demonstrates all the different options of the echo command
:: Run it on an actual command prompt (pipe it to a file)
:: and using bat (pipe it to another file)
:: then compare the results
:: Displays the help text. Initially, Echo should be on.
echo /?
:: Display the echo status
echo
:: Echo text
echo hallo
:: Suppress echo-ing and echo text
@echo hello
:: Echo a blank line
@echo.
:: Turn echo off
@echo off
:: And again. This is not echoed now
echo off
:: Display the echo status
echo
:: It can still be suppressed, even though there is no difference
@echo hallo
:: Parameter
echo dit bestand heet %0
echo computernaam is %COMPUTERNAME%
:: Echo on
echo on
:: Echo on again (echoed)
echo on