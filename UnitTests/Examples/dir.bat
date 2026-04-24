@echo off
:: Full dir output — timestamps and sizes must match on the same machine
dir "%~dp0"
:: Bare format
dir /b "%~dp0"
:: Directories only
dir /b /ad "%~dp0"
:: Files only
dir /b /a-d "%~dp0"
:: Sorted by name
dir /b /on "%~dp0"
:: Lowercase
dir /b /l "%~dp0"
