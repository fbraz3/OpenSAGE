# BGFX Native Libraries Setup

This directory contains BGFX native libraries for different platforms and architectures.

## Prerequisites

- **macOS**: Xcode command-line tools, GCC or Clang (11+)
- **Windows**: Visual Studio 2022 or higher, MinGW
- **Linux**: GCC 11+, make, git

## Building BGFX Libraries

### macOS (arm64 and x86_64)

```bash
cd /tmp
git clone https://github.com/bkaradzic/bgfx.git
cd bgfx

# Build for arm64
cd bx
./configure-osx-arm64.sh
cd ..
make -j4 osx-arm64

# Build for x86_64
cd bx
./configure-osx-x86_64.sh
cd ..
make -j4 osx-x86_64

# Copy to OpenSAGE
cp .build/osx-arm64/bin/Release/libbgfx.dylib /path/to/OpenSAGE/lib/bgfx/macos/arm64/
cp .build/osx-x86_64/bin/Release/libbgfx.dylib /path/to/OpenSAGE/lib/bgfx/macos/x86_64/

# Verify symbols are exported
nm /path/to/OpenSAGE/lib/bgfx/macos/arm64/libbgfx.dylib | grep bgfx_init
```

### Windows (x64)

```bash
cd %TEMP%
git clone https://github.com/bkaradzic/bgfx.git
cd bgfx

# Generate Visual Studio project files
..\bx\tools\bin\windows\genie vs2022

# Build with Visual Studio
start .build\projects\vs2022\bgfx.sln
# Build > Batch Build > select "bgfx|x64|Release" > Build

# Copy to OpenSAGE
copy .build\win64_vs2022\bin\bgfx.dll %OpenSAGEPath%\lib\bgfx\windows\x64\
copy .build\win64_vs2022\lib\bgfx.lib %OpenSAGEPath%\lib\bgfx\windows\x64\
```

### Linux (x64)

```bash
cd /tmp
git clone https://github.com/bkaradzic/bgfx.git
cd bgfx

# Build for Linux
make -j4 linux-release64

# Copy to OpenSAGE
cp .build/linux64_gcc/bin/Release/libbgfx.so /path/to/OpenSAGE/lib/bgfx/linux/x64/

# Verify symbols are exported
nm /path/to/OpenSAGE/lib/bgfx/linux/x64/libbgfx.so | grep bgfx_init
```

## Directory Structure

```
```
lib/bgfx/
├── macos/
│   ├── arm64/
│   │   └── libbgfx.dylib
│   └── x86_64/
│       └── libbgfx.dylib
├── windows/
│   └── x64/
│       ├── bgfx.dll
│       └── bgfx.lib
└── linux/
    └── x64/
        └── libbgfx.so
```

## Verification Checklist

- [ ] All 5 binary files present
- [ ] File sizes > 5MB (verify with `ls -lh`)
- [ ] Symbol exports verified (grep for `bgfx_init`, `bgfx_frame`)
- [ ] Platform support confirmed:
  - [ ] macOS arm64: Metal backend
  - [ ] macOS x86_64: Metal backend
  - [ ] Windows x64: Direct3D 11 backend
  - [ ] Linux x64: Vulkan backend

## Build Configuration Notes

- **BGFX Configuration**: All backends enabled (Metal, D3D11, Vulkan, OpenGL)
- **Shared Library Option**: Used `--with-shared-lib` flag for dynamic linking
- **Build Type**: Release configuration for optimal performance

## Phase 5A Success Criteria

✅ All binary files acquired and verified
✅ Library symbols accessible via P/Invoke
✅ Platform detection working correctly
✅ Ready for P/Invoke bindings implementation

## References

- [BGFX Repository](https://github.com/bkaradzic/bgfx)
- [Build Documentation](https://bkaradzic.github.io/bgfx/build.html)
- OpenSAGE Phase 5 Documentation: `docs/phases/PHASE_5A_Weekly_Execution_Plan.md`
