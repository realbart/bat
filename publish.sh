#!/bin/bash
# Publish script for bat utilities as self-contained executables

set -e

echo "Cleaning previous builds..."
rm -rf ./publish

echo "Building bat..."
dotnet publish bat/bat.csproj -c Release -r linux-x64 --self-contained -o ./publish/bat

echo "Building doskey..."
dotnet publish doskey/doskey.csproj -c Release -r linux-x64 --self-contained -o ./publish/doskey

echo "Building subst..."
dotnet publish subst/subst.csproj -c Release -r linux-x64 --self-contained -o ./publish/subst

echo "Building choice..."
dotnet publish choice/choice.csproj -c Release -r linux-x64 --self-contained -o ./publish/choice

echo ""
echo "Build complete!"
echo "bat binary: ./publish/bat/bat"
echo "doskey binary: ./publish/doskey/doskey"
echo "subst binary: ./publish/subst/subst"
echo "choice binary: ./publish/choice/choice"
echo ""
echo "File sizes:"
ls -lh ./publish/bat/bat ./publish/doskey/doskey ./publish/subst/subst ./publish/choice/choice 2>/dev/null || echo "Binaries not found - check build output"

#move to ~/.local/bin
mkdir -p ~/.local/bin
mv ./publish/bat/* ~/.local/bin/
mv ./publish/doskey/* ~/.local/bin/
mv ./publish/subst/* ~/.local/bin/
mv ./publish/choice/* ~/.local/bin/
cp ./autoexec.bat ~/.local/bin/autoexec.bat