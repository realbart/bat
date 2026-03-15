#!/bin/bash
# Publish script for bat utilities as self-contained executables

echo "Building bat..."
dotnet publish bat/bat.csproj -c Release -r linux-x64 --self-contained -o ./publish/bat

echo "Building doskey..."
dotnet publish doskey/doskey.csproj -c Release -r linux-x64 --self-contained -o ./publish/doskey

echo "Building subst..."
dotnet publish subst/subst.csproj -c Release -r linux-x64 --self-contained -o ./publish/subst

echo ""
echo "Build complete!"
echo "bat binary: ./publish/bat/bat"
echo "doskey binary: ./publish/doskey/doskey"
echo "subst binary: ./publish/subst/subst"
echo ""
echo "File sizes:"
ls -lh ./publish/bat/bat ./publish/doskey/doskey ./publish/subst/subst 2>/dev/null || echo "Binaries not found - check build output"

#move to ~/.local/bin
mv ./publish/bat/* ~/.local/bin/
mv ./publish/doskey/* ~/.local/bin/
mv ./publish/subst/* ~/.local/bin/