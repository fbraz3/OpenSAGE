#!/bin/bash

# Test BGFX Backend Integration
# This script tests the BGFX graphics backend with the OpenSAGE game engine

set -e

cd "$(dirname "$0")"

echo "================================"
echo "Testing BGFX Backend Integration"
echo "================================"
echo ""

# Set the game path
export CNC_GENERALS_PATH="/Users/felipebraz/GeneralsX/Generals"

echo "Game Path: $CNC_GENERALS_PATH"
echo "Backend: BGFX"
echo ""

# Run the game with a timeout
echo "Starting game launcher..."
echo "Timeout: 20 seconds"
echo ""

timeout -s 9 20s dotnet run --project src/OpenSage.Launcher/OpenSage.Launcher.csproj -- \
    --renderer bgfx \
    --noshellmap \
    2>&1 | tee /tmp/bgfx_test.log || true

echo ""
echo "================================"
echo "Test Output Saved to: /tmp/bgfx_test.log"
echo "================================"
echo ""

# Check results
if grep -q "Starting..." /tmp/bgfx_test.log; then
    echo "✅ BGFX Backend: INITIALIZED SUCCESSFULLY"
else
    echo "❌ BGFX Backend: INITIALIZATION FAILED"
fi

if grep -q "Exception\|Error" /tmp/bgfx_test.log; then
    echo "⚠️  Found errors in output:"
    grep "Exception\|Error" /tmp/bgfx_test.log | head -5
else
    echo "✅ No critical errors detected"
fi
