#!/bin/bash
set -euo pipefail

echo "==============================="
echo "A.I.L. ENV RESET START"
echo "==============================="

PROJECT_PATH="/Volumes/DevDrive/Dev/platform/AIL"

echo "→ Navigating to project..."
cd "$PROJECT_PATH" || { echo "❌ Failed to cd into project"; exit 1; }

echo "→ Killing lingering dotnet processes..."
killall dotnet 2>/dev/null || true
killall VBCSCompiler 2>/dev/null || true

echo "→ Fixing permissions..."
chmod -R u+rw .

echo "→ Removing bin/ and obj/ folders..."
find . -type d -name "bin" -exec rm -rf {} +
find . -type d -name "obj" -exec rm -rf {} +

echo "→ Verifying drive write access..."
touch .write_test || { echo "❌ Drive is NOT writable"; exit 1; }
rm .write_test

echo "→ Running clean restore/build/test..."
dotnet clean AIL.slnx
dotnet restore AIL.slnx
dotnet build AIL.slnx
dotnet test AIL.slnx

echo "==============================="
echo "A.I.L. ENV RESET COMPLETE"
echo "==============================="
