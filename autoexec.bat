:; exec /home/bart/.local/bin/bat "$0" "$@"
@echo off
setlocal EnableExtensions

:: -------------------- Doelpad bepalen (HOME of fallback) --------------------
if not defined HOME (
    echo [INFO] Omgeving variabele HOME is niet gezet.
    echo        Ik gebruik tijdelijk je gebruikersprofiel als doel.
    set "TARGET=%USERPROFILE%"
) else (
    set "TARGET=%HOME%"
)

echo.
echo Doelpad voor de koppeling wordt: "%TARGET%"
if not exist "%TARGET%" (
    echo [FOUT] Het pad bestaat niet. Maak het eerst aan of kies een ander pad.
    goto :END
)

:: -------------------- Vraag of we D: willen gebruiken -----------------------
echo.
choice /C JN /N /M "Wil je station D: koppelen aan dit pad met SUBST? (J/N): "
if errorlevel 2 goto ASK_REMOVE

:: -------------------- Koppelen naar D: (of andere letter) -------------------
set "DRIVE=D"

call :EnsureFreeOrSubstRemovable "%DRIVE%"
if errorlevel 1 (
    echo.
    echo [LET OP] %DRIVE%: lijkt al in gebruik en is niet als SUBST te verwijderen.
    choice /C OWX /N /M "Overschrijven met andere letter (O), toch proberen (W), of Afbreken (X)? "
    if errorlevel 3 goto END
    if errorlevel 2 goto TRY_FORCE
    if errorlevel 1 goto PICK_LETTER
)

:MAKE_D
call :MakeSubst "%DRIVE%" "%TARGET%"
if errorlevel 1 goto END
goto PERSIST_ASK

:TRY_FORCE
:: Probeer eerst SUBST te ontkoppelen; als het geen SUBST is, blijft het staan.
subst %DRIVE%: /D >nul 2>&1
call :MakeSubst "%DRIVE%" "%TARGET%"
if errorlevel 1 goto END
goto PERSIST_ASK

:PICK_LETTER
echo.
set "DRIVE="
set /P DRIVE=Voer een VRIJE stationsletter in (bijv. P): 
if not defined DRIVE goto PICK_LETTER
set "DRIVE=%DRIVE::=%"
set "DRIVE=%DRIVE:~0,1%"
call :EnsureFreeOrSubstRemovable "%DRIVE%"
if errorlevel 1 (
    echo [FOUT] %DRIVE%: is bezet en niet als SUBST te verwijderen. Kies een andere letter.
    goto PICK_LETTER
)
call :MakeSubst "%DRIVE%" "%TARGET%"
if errorlevel 1 goto END
goto PERSIST_ASK

:: -------------------- Optioneel: bestaande SUBST ontkoppelen ----------------
:ASK_REMOVE
choice /C VD /N /M "Wil je een bestaande SUBST-koppeling verwijderen (V) of Doorgaan/stoppen (D)? "
if errorlevel 2 goto END
set "RMD="
set /P RMD=Welke letter wil je ontkoppelen (bijv. D): 
if not defined RMD goto ASK_REMOVE
set "RMD=%RMD::=%"
set "RMD=%RMD:~0,1%"
subst %RMD%: /D
if errorlevel 1 (
    echo [INFO] %RMD%: is geen SUBST-station of bestaat niet.
) else (
    echo [OK] %RMD%: ontkoppeld.
)
goto END

:: -------------------- Blijvend maken (opstartscript) ------------------------
:PERSIST_ASK
echo.
choice /C JN /N /M "Moet deze koppeling bij elke aanmelding automatisch terugkomen? (J/N): "
if errorlevel 2 goto END

set "STARTUP=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup"
if not exist "%STARTUP%" (
    echo [FOUT] Opstartmap niet gevonden: "%STARTUP%"
    goto END
)

(
  echo @echo off
  echo subst %DRIVE%: "%TARGET%"
) > "%STARTUP%\subst_%DRIVE%.bat"

if errorlevel 1 (
    echo [FOUT] Kon het opstartscript niet aanmaken.
) else (
    echo [OK] Opstartscript gemaakt: "%STARTUP%\subst_%DRIVE%.bat"
    echo      De koppeling %DRIVE%: -> "%TARGET%" wordt voortaan bij aanmelden gezet.
)

goto END

:: -------------------- Helpers --------------------
:EnsureFreeOrSubstRemovable
:: In:  %1 = letter (zonder dubbelepunt)
:: Out: ERRORLEVEL 0 = vrij of als SUBST te verwijderen, 1 = bezet (niet‑SUBST)
setlocal
set "_L=%~1"
:: Is de letter al een SUBST? Dan kunnen we hem verwijderen.
subst | findstr /I /B /C:"%_L%:\:" >nul
if not errorlevel 1 (
    endlocal & exit /b 0
)
:: Bestaat de letter als station? Zo ja, niet vrij.
if exist %_L%:\ (
    endlocal & exit /b 1
)
endlocal & exit /b 0

:MakeSubst
:: In: %1 = letter, %2 = doelpad
setlocal
subst %~1: "%~2"
if errorlevel 1 (
    echo [FOUT] Mislukt om %~1: te koppelen aan "%~2%".
    endlocal & exit /b 1
) else (
    echo [OK] Klaar: %~1: -> "%~2%"
    endlocal & exit /b 0
)

:END
endlocal