# OpenSAGE Audio System

## Overview

OpenSAGE uses **SharpAudio** (v1.0.27-beta) for cross-platform audio support. SharpAudio is a C# wrapper around platform-specific audio APIs:

- **Windows**: DirectSound
- **Linux**: PulseAudio/ALSA
- **macOS**: OpenAL (via native framework binding)

## Architecture

### AudioSystem Class

Located in `src/OpenSage.Game/Audio/AudioSystem.cs`, the `AudioSystem` is a `GameSystem` that manages:

- **Audio engine initialization** - Creates platform-specific audio backend
- **3D audio** - Spatial audio positioning via `Audio3DEngine`
- **Sound buffering** - Caches decoded audio files in memory
- **Volume control** - Submixers for different audio categories (SFX, Music, Voice, etc.)
- **Music playback** - Streams music files with fade support

### Core Components

```text
AudioSystem
├─ AudioEngine (platform audio backend)
├─ Audio3DEngine (3D spatial audio)
├─ Submixers (volume channels: SoundFX, Music, Voice, etc.)
├─ AudioBuffer cache (decoded WAV files)
└─ SoundStream (music file streaming)
```

### Initialization Flow

When AudioSystem is constructed:

1. Load AudioSettings from asset store
2. Attempt AudioEngine.CreateDefault()
3. If success: Create 3D audio engine, volume submixers, mark as initialized
4. If failure: Log error and continue without audio support

## Platform-Specific Issues

### macOS

**Problem**: `AudioEngine.CreateDefault()` returns `null`

**Root Cause**: SharpAudio on macOS requires the OpenAL library to be present at runtime. The macOS system framework `OpenAL.framework` is insufficient; the library needs to be compiled and available.

**Symptoms**:

- Game starts normally
- Audio system is disabled
- Log shows: "Audio engine initialization failed. The audio system will operate in disabled mode."
- On macOS: This typically requires OpenAL: `brew install openal-soft`

**Solutions**:

1. Install OpenAL (Recommended)

```bash
brew install openal-soft
```

2. For Development: Copy library to build output

```bash
cp /usr/local/Cellar/openal-soft/1.24.3/lib/libopenal.1.24.3.dylib ./bin/Debug/net8.0/
ln -sf libopenal.1.24.3.dylib ./bin/Debug/net8.0/libopenal.dylib
```

3. Status: SharpAudio PR #70 attempted to fix macOS support, but issues remain with v1.0.27-beta on ARM64 Macs

### Linux

**Problem**: May require PulseAudio or ALSA development libraries

**Solution**:

```bash
# Ubuntu/Debian
sudo apt-get install libopenal-dev libpulse-dev

# Fedora
sudo dnf install openal-soft-devel pulseaudio-libs-devel
```

### Windows

Generally works out of the box. DirectSound is built into Windows.

## Graceful Degradation

When audio initialization fails, OpenSAGE implements graceful degradation:

1. **Game continues to run** - No audio, but full gameplay
2. **Clear logging** - Users know why audio is disabled
3. **Null-safe operations** - All audio methods check initialization flag

Example:

```csharp
public void Update(Camera camera)
{
    if (!_isAudioInitialized)
    {
        return;  // Skip audio updates if not initialized
    }
    // ... audio update logic
}
```

## Audio Methods

### Sound Playback

```csharp
// Play a one-shot sound effect at world position
AudioSystem.PlayAudioEvent(position: Vector3, audioEvent: AudioEvent);

// Play sound attached to game object
AudioSystem.PlayAudioEvent(gameObject: GameObject, audioEvent: AudioEvent);

// Play sound without spatial positioning
AudioSystem.PlayAudioEvent(audioEvent: AudioEvent);
```

### Music Playback

```csharp
// Play background music with optional fade
AudioSystem.PlayMusicTrack(track: MusicTrack, fadeIn: bool, fadeOut: bool);

// Stop currently playing music
AudioSystem.StopCurrentMusicTrack(fadeOut: bool);
```

### Audio File Access

```csharp
// Get cached sound buffer (small files like .wav)
AudioSource GetSound(entry: FileSystemEntry, submixer: AudioVolumeSlider?);

// Get streamed audio (large files like music)
SoundStream GetStream(entry: FileSystemEntry);
```

## Performance Considerations

- **Buffering**: Small audio files (SFX) are cached in memory
- **Streaming**: Large files (music) are streamed to avoid full memory load
- **Volume mixing**: Submixers allow efficient volume control

## Known Limitations

1. **macOS arm64**: SharpAudio 1.0.27-beta has stability issues
2. **3D audio**: 3D positioning currently disabled
3. **Looping**: Audio looping not fully implemented
4. **Fading**: Music fade in/out not yet implemented

## Testing Audio

```bash
# Run with debug logging
dotnet run --project src/OpenSage.Launcher/OpenSage.Launcher.csproj

# Check logs for AudioSystem section
```

## Future Improvements

- Update SharpAudio to newer version when available
- Implement proper audio looping
- Add music fade in/out support
- Fix 3D audio positioning for units
- Consider alternative audio backends for macOS
