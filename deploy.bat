@echo off
echo Building Multiplayer Trade Mod (Release)...
dotnet build MultiplayerTrade.csproj --configuration Release -v q

if %errorlevel% neq 0 (
    echo Build failed! Aborting deployment.
    exit /b %errorlevel%
)

echo.
echo Deploying to Local Mods folder...

set "ONI_MODS_DIR=%USERPROFILE%\Documents\Klei\OxygenNotIncluded\mods\Local\MultiplayerTrade"
set "ONI_MP_ASSETS=C:\Users\ELECTRO-RIMO\Desktop\Games Mods\Oxygen Not Included\Oxygen_Not_Included_Multiplayer\ClassLibrary1\ModAssets\assets"

if not exist "%ONI_MODS_DIR%" (
    mkdir "%ONI_MODS_DIR%"
)

copy /Y "bin\Release\net472\MultiplayerTrade.dll" "%ONI_MODS_DIR%\"
copy /Y "mod.yaml" "%ONI_MODS_DIR%\"
copy /Y "mod_info.yaml" "%ONI_MODS_DIR%\"

if exist "%ONI_MP_ASSETS%" (
    echo Copying AssetBundles...
    xcopy /Y /I /E "%ONI_MP_ASSETS%" "%ONI_MODS_DIR%\assets"
)

echo.
echo Deployment Complete! You can now launch Oxygen Not Included.
