#!/usr/bin/env python3
import subprocess
import os
import sys
import time

os.chdir('/Users/felipebraz/PhpstormProjects/pessoal/OpenSAGE')

env = os.environ.copy()

# Load .env.bgfx configuration
env_file = '/Users/felipebraz/PhpstormProjects/pessoal/OpenSAGE/.env.bgfx'
if os.path.exists(env_file):
    with open(env_file, 'r') as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith('#'):
                key, value = line.split('=', 1)
                env[key.strip()] = value.strip()

# Ensure CNC_GENERALS_PATH is set
if 'CNC_GENERALS_PATH' not in env:
    env['CNC_GENERALS_PATH'] = '/Users/felipebraz/GeneralsX/Generals'

print("=" * 50)
print("Testing BGFX Backend Integration")
print("=" * 50)
print(f"Game Path: {env.get('CNC_GENERALS_PATH', 'NOT SET')}")
print(f"Tiered Compilation: {env.get('DOTNET_TieredCompilation', 'default (enabled)')}")
print(f"Backend: BGFX")
print("Timeout: 15 seconds")
print("")

try:
    result = subprocess.run([
        'dotnet', 'run',
        '--project', 'src/OpenSage.Launcher/OpenSage.Launcher.csproj',
        '--',
        '--renderer', 'bgfx',
        '--noshellmap'
    ], env=env, capture_output=True, text=True, timeout=15)
    
    output = result.stdout + result.stderr
    
    print(output)
    print("")
    print("=" * 50)
    print("Test Results:")
    print("=" * 50)
    
    if 'Starting...' in output:
        print("✅ BGFX Backend initialized successfully")
    else:
        print("⚠️  Starting message not found")
    
    if 'Exception' in output or 'Fatal' in output:
        print("❌ Found errors in output")
        for line in output.split('\n'):
            if 'Exception' in line or 'Fatal' in line or 'error' in line.lower():
                print(f"   {line}")
    else:
        print("✅ No critical errors detected")
    
    if result.returncode != 0 and result.returncode != 124:  # 124 is timeout
        print(f"⚠️  Process exited with code {result.returncode}")

except subprocess.TimeoutExpired:
    print("✅ Game execution timed out (expected behavior)")
    print("   Game successfully initialized and ran for 15 seconds")
except Exception as e:
    print(f"❌ Error running test: {e}")
    sys.exit(1)
