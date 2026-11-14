#!/usr/bin/env bash
# Builds ONE exe for Windows x64 that needs nothing installed (.NET is inside): dist/<AppInfo.Name>.exe
# Runs on Linux too (EnableWindowsTargeting). Needs a .NET 8 SDK that includes the WindowsDesktop SDK
# (Microsoft's build; Ubuntu's own dotnet package does not have it).
# Usage: ./publish.sh            (Windows: publish.cmd)
set -euo pipefail
cd "$(dirname "$0")"
NAME=$(sed -n 's/.*public const string Name = "\([^"]*\)".*/\1/p' src/Earbuds.Presentation/AppInfo.cs)
VERSION=$(sed -n 's/.*public const string Version = "\([^"]*\)".*/\1/p' src/Earbuds.Presentation/AppInfo.cs)
[ -n "$NAME" ] || { echo "AppInfo.Name not found"; exit 1; }
rm -rf dist publish-tmp
dotnet publish src/Earbuds.App/Earbuds.App.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none -p:AppExeName="$NAME" -p:Version="$VERSION" -o publish-tmp
mkdir -p dist
cp "publish-tmp/$NAME.exe" "dist/$NAME.exe"
rm -rf publish-tmp
( cd dist && sha256sum "$NAME.exe" > "$NAME.exe.sha256" )
ls -la dist
