# squeeze-net-cli

Sample SlimProto player demonstrating SlimProtoNet library usage.

## What it demonstrates

- Server discovery via UDP broadcast
- SlimProto connection handshake
- Audio streaming (PCM/MP3/FLAC decoding via MiniAudioExNET)
- Server-controlled playback (pause, resume, stop commands)
- Server-controlled volume (per-channel gain)
- Status reporting to server

## Running

Auto-discover LMS on network:
```powershell
dotnet run
```

Connect to specific server:
```powershell
dotnet run -- 192.168.1.100:3483
```

Once running, control playback from LMS web interface (Settings → Players → SqueezeNetCli).

## Code structure

- **Program.cs** - Connection setup and message loop
- **MessageHandler.cs** - Handles server commands (stream, pause, volume, etc.)
- **PlaybackManager.cs** - Manages audio stream lifecycle
- **MiniAudioPlayer.cs** - Decoding and audio output
- **CircularBuffer.cs** - Network buffering

## What's missing

This is a sample, not a production player. No local controls, no gapless playback, minimal error recovery.
