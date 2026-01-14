@echo off
cd /d "%~dp0"
setlocal

echo ===================================================
echo   RESET DE BASE DE DATOS (DOCUMENTOS) - v3
echo ===================================================
echo.
echo ESTO BORRARA TODOS LOS DOCUMENTOS Y CARPETAS de PostgreSQL.
echo.

:: 1. Buscar PSQL
set "PSQL_CMD=psql"
where psql >nul 2>nul
if %errorlevel% equ 0 goto found_path

:: Check common paths
if exist "C:\Program Files\PostgreSQL\16\bin\psql.exe" (
    set "PSQL_CMD=C:\Program Files\PostgreSQL\16\bin\psql.exe"
    goto found_path
)
if exist "C:\Program Files\PostgreSQL\15\bin\psql.exe" (
    set "PSQL_CMD=C:\Program Files\PostgreSQL\15\bin\psql.exe"
    goto found_path
)
if exist "C:\Program Files\PostgreSQL\14\bin\psql.exe" (
    set "PSQL_CMD=C:\Program Files\PostgreSQL\14\bin\psql.exe"
    goto found_path
)

echo [!] ERROR CRITICO: No se encuentra 'psql.exe'.
pause
exit /b 1

:found_path
echo [*] Usando PSQL en: "%PSQL_CMD%" 

set /p confirm="Escribe SI para confirmar borrado de 'qmsflowdoc': "
if /I "%confirm%" NEQ "SI" goto end

echo.
echo [*] Conectando a PostgreSQL...
echo [INFO] Usuario: postgres - DB: qmsflowdoc
echo [INPUT] Introduce tu contrasenna de PostgreSQL cuando se pida:
echo.

"%PSQL_CMD%" -U postgres -d qmsflowdoc -f "reset_doc_tables.sql"

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Fallo al ejecutar SQL. Revisa la contrasenna o conexion.
    pause
    exit /b 1
)

echo.
echo [OK] Base de datos limpia correctamente.
pause
goto final

:end
echo Cancelado.
pause

:final
