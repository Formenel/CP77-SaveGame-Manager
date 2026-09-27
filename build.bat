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

echo.
set /p PUBLISH="Alles gruen. Release-exe publishen? (j/n) "
if /i not "%PUBLISH%"=="j" (
    echo Publish uebersprungen.
    goto :end
)

echo.
echo === Publish (Release, self-contained, single-file) ===
dotnet publish src\Cp77SaveManager.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
if errorlevel 1 (
    echo.
    echo PUBLISH FEHLGESCHLAGEN.
    goto :end
)

echo.
echo Fertig: publish\cp77sgm.exe

:end
pause
