using System;
using System.Collections.Generic;
using System.Numerics;
using OpenSage.Graphics.Cameras;
using OpenSage.IO;
using OpenSage.Logic.Object;
using SharpAudio;
using SharpAudio.Codec;
using SharpAudio.Codec.Wave;

namespace OpenSage.Audio;

public sealed class AudioSystem : GameSystem
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private readonly List<AudioSource> _sources;
    private readonly Dictionary<string, AudioBuffer> _cached;
    private readonly AudioEngine _engine;
    private readonly AudioSettings _settings;
    private readonly Audio3DEngine _3dengine;
    private readonly Dictionary<AudioVolumeSlider, Submixer> _mixers;
    private readonly bool _isAudioInitialized;

    private readonly Random _random;

    private readonly Dictionary<string, int> _musicTrackFinishedCounts = new Dictionary<string, int>();

    private string _currentTrackName;
    private SoundStream _currentTrack;

    public AudioSystem(IGame game) : base(game)
    {
        _sources = new List<AudioSource>();
        _cached = new Dictionary<string, AudioBuffer>();
        _mixers = new Dictionary<AudioVolumeSlider, Submixer>();
        _random = new Random();
        _isAudioInitialized = false;

        try
        {
            // Load audio settings first
            Logger.Info("AudioSystem: Loading AudioSettings...");
            _settings = game.AssetStore?.AudioSettings?.Current;
            if (_settings == null)
            {
                Logger.Warn("AudioSettings not found. Audio system will be disabled.");
                return;
            }

            // Attempt to initialize audio engine
            Logger.Info("AudioSystem: Attempting to initialize audio engine (platform: {0})...", 
                GetPlatformName());
            _engine = AddDisposable(AudioEngine.CreateDefault());

            if (_engine == null)
            {
                // This is a known issue on macOS and some Linux systems where SharpAudio
                // cannot initialize the platform audio backend (OpenAL/PulseAudio missing)
                LogAudioInitializationFailure();
                return;
            }

            // Initialize 3D audio engine
            Logger.Info("AudioSystem: Creating 3D audio engine...");
            _3dengine = _engine.Create3DEngine();

            // Create audio submixers for volume control
            Logger.Info("AudioSystem: Creating audio submixers...");
            CreateSubmixers();

            _isAudioInitialized = true;
            Logger.Info("AudioSystem: Initialized successfully.");
        }
        catch (Exception ex)
        {
            LogAudioInitializationFailure();
            Logger.Debug(ex, "Exception details during audio initialization");
        }
    }

    private static string GetPlatformName()
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Windows))
        {
            return "Windows";
        }
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.OSX))
        {
            return "macOS";
        }
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Linux))
        {
            return "Linux";
        }
        return "Unknown";
    }

    private void LogAudioInitializationFailure()
    {
        var platform = GetPlatformName();
        Logger.Warn("Audio engine initialization failed. The audio system will operate in disabled mode.");
        
        if (platform == "macOS")
        {
            Logger.Warn("On macOS: Install OpenAL via Homebrew: brew install openal-soft");
        }
        else if (platform == "Linux")
        {
            Logger.Warn("On Linux: Install PulseAudio development libraries");
        }
        
        Logger.Warn("Games will continue to work without audio output.");
    }

    internal override void OnSceneChanged()
    {
        _musicTrackFinishedCounts.Clear();
    }

    public void Update(Camera camera)
    {
        if (!_isAudioInitialized)
        {
            return;
        }

        if (camera != null)
        {
            UpdateListener(camera);
        }

        if (_currentTrack != null && !_currentTrack.IsPlaying)
        {
            _musicTrackFinishedCounts.TryGetValue(_currentTrackName, out var count);
            _musicTrackFinishedCounts[_currentTrackName] = count + 1;
            _currentTrack.Dispose();
            _currentTrack = null;
            _currentTrackName = null;
        }
    }

    private void CreateSubmixers()
    {
        if (_settings == null)
        {
            return;
        }

        // Create all available mixers
        _mixers[AudioVolumeSlider.SoundFX] = _engine.CreateSubmixer();
        _mixers[AudioVolumeSlider.SoundFX].Volume = (float)_settings.DefaultSoundVolume;

        _mixers[AudioVolumeSlider.Music] = _engine.CreateSubmixer();
        _mixers[AudioVolumeSlider.Music].Volume = (float)_settings.DefaultMusicVolume;

        _mixers[AudioVolumeSlider.Ambient] = _engine.CreateSubmixer();
        _mixers[AudioVolumeSlider.Ambient].Volume = (float)_settings.DefaultAmbientVolume;

        _mixers[AudioVolumeSlider.Voice] = _engine.CreateSubmixer();
        _mixers[AudioVolumeSlider.Voice].Volume = (float)_settings.DefaultVoiceVolume;

        _mixers[AudioVolumeSlider.Movie] = _engine.CreateSubmixer();
        _mixers[AudioVolumeSlider.Movie].Volume = (float)_settings.DefaultMovieVolume;
    }

    protected override void Dispose(bool disposeManagedResources)
    {
        base.Dispose(disposeManagedResources);

        _sources.Clear();
        _cached.Clear();
    }

    /// <summary>
    /// Opens a cached audio file. Usually used for small audio files (.wav)
    /// </summary>
    public AudioSource GetSound(FileSystemEntry entry,
        AudioVolumeSlider? vslider = AudioVolumeSlider.None, bool loop = false)
    {
        if (!_isAudioInitialized)
        {
            return null;
        }

        AudioBuffer buffer;

        if (!_cached.ContainsKey(entry.FilePath))
        {
            var decoder = new WaveDecoder(entry.Open());
            byte[] data = null;
            decoder.GetSamples(ref data);

            buffer = AddDisposable(_engine.CreateBuffer());
            buffer.BufferData(data, decoder.Format);

            _cached[entry.FilePath] = buffer;
        }
        else
        {
            buffer = _cached[entry.FilePath];
        }

        var mixer = (vslider.HasValue && vslider.Value != AudioVolumeSlider.None) ?
                    _mixers[vslider.Value] : null;
        var source = AddDisposable(_engine.CreateSource(mixer));
        source.QueueBuffer(buffer);
        // TODO: Implement looping

        _sources.Add(source);

        return source;
    }

    private FileSystemEntry ResolveAudioEventEntry(AudioEvent ev)
    {
        if (ev.Sounds.Length == 0)
        {
            return null;
        }

        // TOOD: Check control flag before choosing at random.
        var sound = ev.Sounds[_random.Next(ev.Sounds.Length)];
        return sound.AudioFile.Value?.Entry;
    }

    private FileSystemEntry ResolveDialogEventEntry(DialogEvent ev)
    {
        return ev.File.Value.Entry;
    }

    /// <summary>
    /// Open a music/audio file that gets streamed.
    /// </summary>
    public SoundStream GetStream(FileSystemEntry entry)
    {
        if (!_isAudioInitialized)
        {
            return null;
        }

        // TODO: Use submixer (currently not possible)
        return AddDisposable(new SoundStream(entry.Open(), _engine));
    }

    public void PlayAudioEvent(string eventName)
    {
        var audioEvent = Game.AssetStore.AudioEvents.GetByName(eventName);

        if (audioEvent == null)
        {
            Logger.Warn($"Missing AudioEvent: {eventName}");
            return;
        }

        PlayAudioEvent(audioEvent);
    }

    public void DisposeSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        if (source.IsPlaying())
        {
            source.Stop();
        }
        _sources.Remove(source);
    }

    private AudioSource PlayAudioEventBase(BaseAudioEventInfo baseAudioEvent, bool looping = false)
    {
        if (baseAudioEvent is not BaseSingleSound audioEvent)
        {
            return null;
        }

        var entry = baseAudioEvent switch
        {
            AudioEvent ae => ResolveAudioEventEntry(ae),
            DialogEvent de => ResolveDialogEventEntry(de),
            _ => null, // todo
        };

        if (entry == null)
        {
            Logger.Warn($"Missing Audio File: {audioEvent.Name}");
            return null;
        }

        var source = GetSound(entry, audioEvent.SubmixSlider, looping || audioEvent.Control.HasFlag(AudioControlFlags.Loop));
        if (source == null)
        {
            return null;
        }

        source.Volume = (float)audioEvent.Volume;
        return source;
    }

    private void UpdateListener(Camera camera)
    {
        _3dengine.SetListenerPosition(camera.Position);
        var front = Vector3.Normalize(camera.Target - camera.Position);
        _3dengine.SetListenerOrientation(camera.Up, front);
    }

    public AudioSource PlayAudioEvent(GameObject emitter, BaseAudioEventInfo baseAudioEvent, bool looping = false)
    {
        var source = PlayAudioEventBase(baseAudioEvent, looping);
        if (source == null)
        {
            return null;
        }

        // TODO: fix issues with some units
        //_3dengine.SetSourcePosition(source, emitter.Transform.Translation);
        source.Play();
        return source;
    }

    public AudioSource PlayAudioEvent(Vector3 position, BaseAudioEventInfo baseAudioEvent, bool looping = false)
    {
        var source = PlayAudioEventBase(baseAudioEvent, looping);
        if (source == null)
        {
            return null;
        }

        _3dengine.SetSourcePosition(source, position);
        source.Play();
        return source;
    }

    public AudioSource PlayAudioEvent(BaseAudioEventInfo baseAudioEvent, bool looping = false)
    {
        var source = PlayAudioEventBase(baseAudioEvent, looping);
        if (source == null)
        {
            return null;
        }

        source.Play();
        return source;
    }

    public void PlayMusicTrack(MusicTrack musicTrack, bool fadeIn, bool fadeOut)
    {
        if (!_isAudioInitialized)
        {
            return;
        }

        // TODO: fading
        StopCurrentMusicTrack(fadeOut);

        _currentTrackName = musicTrack.Name;
        _currentTrack = GetStream(musicTrack.File.Value.Entry);
        if (_currentTrack != null)
        {
            _currentTrack.Volume = (float)musicTrack.Volume;
            _currentTrack.Play();
        }
    }

    public void StopCurrentMusicTrack(bool fadeOut = false)
    {
        // todo: fade out
        if (_currentTrack != null)
        {
            _currentTrack.Stop();
            _currentTrack.Dispose();
        }
    }

    public int GetFinishedCount(string musicTrackName)
    {
        return _musicTrackFinishedCounts.TryGetValue(musicTrackName, out var number) ? number : 0;
    }
}
