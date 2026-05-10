@echo off
chcp 65001 >nul
title CariErinc - Kırtasiye Programı
cd /d "%~dp0"

echo.
echo ========================================
echo   CariErinc - Kırtasiye Cari Programı
echo ========================================
echo.
echo Uygulama baslatiliyor...
echo.
echo Tarayicida su adrese gidin: http://localhost:5000
echo (Port farkli olabilir - asagida yazacaktir)
echo.
echo Giris: admin / admin123
echo.
echo Kapatmak icin bu pencerede Ctrl+C yapin
echo veya pencereyi kapatın.
echo ========================================
echo.

CariErinc.exe

if errorlevel 1 (
    echo.
    echo Hata olustu. .NET Runtime veya PostgreSQL kontrol edin.
    pause
)
