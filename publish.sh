#!/bin/bash
cd  # Publish script for dosux utilities as self-contained executables

echo "Building dosux..."
dotnet publish dosux/dosux.csproj -c Release -r linux-x64 --self-contained -o ./publish/dosux

echo "Building doskey..."
dotnet publish doskey/doskey.csproj -c Release -r linux-x64 --self-contained -o ./publish/doskey

echo "Building subst..."
dotnet publish subst/subst.csproj -c Release -r linux-x64 --self-contained -o ./publish/subst

echo ""
echo "Build complete!"
echo "dosux binary: ./publish/dosux/dosux"
echo "doskey binary: ./publish/doskey/doskey"
echo "subst binary: ./publish/subst/subst"
echo ""
echo "File sizes:"
ls -lh ./publish/dosux/dosux ./publish/doskey/doskey ./publish/subst/subst 2>/dev/null || echo "Binaries not found - check build output"

#move to ~/.local/bin
mv ./publish/dosux/dosux/* ~/.local/bin/
mv ./publish/dosux/doskey/* ~/.local/bin/
mv ./publish/dosux/subst/* ~/.local/bin/
