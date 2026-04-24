@echo off
:: Redirect stdout to file
echo hello > "%TEMP%\bat_test_redir.txt"
type "%TEMP%\bat_test_redir.txt"
:: Append
echo world >> "%TEMP%\bat_test_redir.txt"
type "%TEMP%\bat_test_redir.txt"
:: Pipe
echo pipe test | findstr "pipe"
:: Redirect stderr (command that fails)
dir Z:\no_such_dir_xyz 2>nul
echo after stderr redirect: %ERRORLEVEL%
:: Cleanup
del "%TEMP%\bat_test_redir.txt" 2>nul
