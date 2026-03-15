:; exec /home/bart/.local/bin/bat "$0" "$@"
@echo off
setlocal EnableExtensions

:: Vraag of we D: willen koppelen aan ~
choice /C JN /N /M "Wil je station D: koppelen aan %HOME%? (J/N): "
if errorlevel 2 goto END

:: Koppel D: aan ~
subst D: "%HOME%"
if errorlevel 1 (
    echo [FOUT] Mislukt om D: te koppelen aan "%HOME%".
) else (
    echo [OK] D: gekoppeld aan "%HOME%".
)

:END
endlocal