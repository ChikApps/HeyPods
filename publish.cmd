@echo off
rem Builds ONE exe for Windows x64 that needs nothing installed (.NET is inside): dist\<AppInfo.Name>.exe
rem Needs the .NET 8 SDK. Usage: double-click, or run "publish.cmd" in this folder.
setlocal
cd /d "%~dp0"
for /f "tokens=2 delims==;" %%a in ('findstr /c:"public const string Name =" src\Earbuds.Presentation\AppInfo.cs') do set NAME=%%~a
for /f "tokens=2 delims==;" %%a in ('findstr /c:"public const string Version =" src\Earbuds.Presentation\AppInfo.cs') do set VERSION=%%~a
set NAME=%NAME: =%
set NAME=%NAME:"=%
set VERSION=%VERSION: =%
set VERSION=%VERSION:"=%
if exist dist rmdir /s /q dist
if exist publish-tmp rmdir /s /q publish-tmp
dotnet publish src\Earbuds.App\Earbuds.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:AppExeName=%NAME% -p:Version=%VERSION% -o publish-tmp || exit /b 1
mkdir dist
copy /y "publish-tmp\%NAME%.exe" "dist\%NAME%.exe" >nul
rmdir /s /q publish-tmp
certutil -hashfile "dist\%NAME%.exe" SHA256
echo Done: dist\%NAME%.exe
