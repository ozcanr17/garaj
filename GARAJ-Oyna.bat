@echo off
REM GARAJ - Windows'ta cift tikla baslat.
REM Bu dosyaya cift tiklayinca oyun baslar.

cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo HATA: dotnet bulunamadi.
  echo Kurmak icin:  winget install --id Microsoft.DotNet.SDK.10
  echo.
  pause
  exit /b 1
)

cls
echo GARAJ derleniyor ve baslatiliyor (ilk sefer biraz surebilir)...
echo.
dotnet run --project src\Garaj.Console -c Release -- %*

echo.
pause
