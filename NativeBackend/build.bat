@echo off
setlocal

set "BUILD_DIR=build"
set "OUTPUT_DIR=bin"

if not exist "%BUILD_DIR%" mkdir "%BUILD_DIR%"
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

cd "%BUILD_DIR%"

:: Configure CMake
cmake .. -G "Visual Studio 17 2022" -A x64

:: Build the project
cmake --build . --config Release

:: Copy the DLL to the project output directory
copy "Release\EnglishListenNative.dll" "..\..\bin\x64\Release\EnglishListenNative.dll"

if %ERRORLEVEL% EQU 0 (
    echo Build successful! EnglishListenNative.dll copied to output directory.
) else (
    echo Build failed with error code %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

endlocal