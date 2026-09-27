@echo off
setlocal

d:
cd "D:\SteamLibrary\steamapps\common\Cyberpunk 2077\tools\savegame-manager"

echo === dotnet build ===
dotnet build
if errorlevel 1 (
    echo.
    echo BUILD FEHLGESCHLAGEN - abgebrochen.
    goto :end
)

echo.
echo === Tests ===
dotnet run --project src\Cp77SaveManager.Core.Tests
if errorlevel 1 (
    echo.
    echo TESTS FEHLGESCHLAGEN - abgebrochen, kein Publish.
    goto :end
)

rem Version kommt einzig aus <Version> in Cp77SaveManager.App.csproj (z.B.
rem "0.0.0-beta2") - hier nirgends mehr hardcoden, nur dort bumpen.
rem
rem WICHTIG: Die Batch-Variablen hier heissen bewusst NICHT "VERSION" - dotnet/
rem MSBuild liest Umgebungsvariablen als impliziten Anfangswert fuer
rem gleichnamige Properties, und "VERSION" kollidiert direkt mit der MSBuild-
rem Property "Version". Genau das hat eben den Fehler verursacht: die csproj
rem setzt "0.0.0-beta2", aber die Env-Var "VERSION=beta2" (die Kurzform fuer
rem den Zip-Namen) hat das beim Restore ueberschrieben - und "beta2" allein
rem ist kein gueltiger NuGet-Versionsstring, daher der Fehler "beta2 ist keine
rem gueltige Versionszeichenfolge". Mit CP77_* als Praefix passiert das nicht mehr.
set CP77_FULL_VERSION=
for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command "([xml](Get-Content 'src\Cp77SaveManager.App\Cp77SaveManager.App.csproj')).Project.PropertyGroup.Version | Select-Object -First 1"`) do set CP77_FULL_VERSION=%%V
if "%CP77_FULL_VERSION%"=="" (
    echo.
    echo Konnte Version nicht aus Cp77SaveManager.App.csproj lesen - abgebrochen.
    goto :end
)

rem Kurzform fuer Ordner-/Zip-Namen: alles nach dem ersten "-" (z.B. aus
rem "0.0.0-beta2" wird "beta2"). Ohne "-" im Wert wird die volle Version genommen.
set CP77_TAG=%CP77_FULL_VERSION%
for /f "tokens=1,* delims=-" %%A in ("%CP77_FULL_VERSION%") do if not "%%B"=="" set CP77_TAG=%%B

echo.
set /p PUBLISH="Alles gruen. Release %CP77_TAG% (Assembly-Version %CP77_FULL_VERSION%) publishen? (j/n) "
if /i not "%PUBLISH%"=="j" (
    echo Publish uebersprungen.
    goto :end
)

rem Zielstruktur entspricht 1:1 dem Cyberpunk-2077-Wurzelverzeichnis, damit
rem das Zip weiter unten direkt dort hinein entpackt werden kann:
rem   <Cyberpunk 2077>\tools\CP77-SaveGame-Manager\cp77sgm.exe
set PUBLISHDIR=publish\tools\CP77-SaveGame-Manager

echo.
echo === Publish (Release, self-contained, single-file, Version %CP77_FULL_VERSION%) ===
if exist "%PUBLISHDIR%" rmdir /s /q "%PUBLISHDIR%"
dotnet publish src\Cp77SaveManager.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%PUBLISHDIR%"
if errorlevel 1 (
    echo.
    echo PUBLISH FEHLGESCHLAGEN.
    goto :end
)

echo.
echo === Zip fuer Nexus bauen ===
rem Kein FOMOD noetig - das ist nur fuer Mods, die Vortex/MO2 in die
rem Load-Order des Spiels einhaengen. cp77sgm ist ein eigenstaendiges Tool
rem unter tools\, dafuer reicht ein normales Zip zum manuellen Entpacken
rem (Nexus-Kategorie "Miscellaneous").
set ZIPNAME=cp77sgm-%CP77_TAG%.zip
if exist "publish\%ZIPNAME%" del "publish\%ZIPNAME%"
powershell -NoProfile -Command "Compress-Archive -Path 'publish\tools' -DestinationPath 'publish\%ZIPNAME%' -Force"
if errorlevel 1 (
    echo.
    echo ZIP-ERSTELLUNG FEHLGESCHLAGEN.
    goto :end
)

echo.
echo Fertig: publish\%ZIPNAME%
echo Einfach in den Cyberpunk-2077-Wurzelordner entpacken -^> landet automatisch unter tools\CP77-SaveGame-Manager\

:end
pause
