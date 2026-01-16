# SlimProtoNet

[![NuGet](https://img.shields.io/nuget/v/SlimProtoNet.svg)](https://www.nuget.org/packages/SlimProtoNet/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

.NET Standard 2.0 library for building Squeezebox-compatible clients that communicate with Logitech Media Server / Lyrion Music Server (LMS).

Ported from Rust [slim-client-protocol-rs](https://github.com/GeoffClements/slim-client-protocol-rs), with bug fixes and .NET-specific QoL improvements.

## Features

- Binary protocol codec with length-prefixed TCP framing
- UDP server discovery
- Client capability negotiation
- Status tracking with wrapping counters
- Zero external dependencies (production)
- Fully mockable API (virtual methods)

**Reference example client**: [squeeze-net-cli](squeeze-net-cli/README.md) - PCM/MP3/FLAC player

## Quick Start

```csharp
using SlimProtoNet.Client;
using SlimProtoNet.Discovery;

// Find server
var discovery = new ServerDiscovery();
var server = await discovery.DiscoverAsync(timeout: TimeSpan.FromSeconds(10));

// Configure capabilities
var caps = new Capabilities(false);
caps.Add(new CapabilityValue(Capability.Model, "MyPlayer"));
caps.Add(new CapabilityValue(Capability.Pcm));
caps.Add(new CapabilityValue(Capability.Mp3));

// Connect and send status
using var client = new SlimClient();
await client.ConnectAsync(server.EndPoint, caps);

var status = new StatusData();
await client.SendAsync(status.CreateStatusMessage(StatusCode.Connect));

// Message loop
while (true)
{
    var msg = await client.ReceiveAsync();
    switch (msg)
    {
        case StreamMessage stream:
            // Handle stream setup
            break;
        case PauseMessage pause:
            // Handle pause command
            break;
    }
}
```

## API Overview

### Core Classes

**SlimClient** - TCP connection manager
- `ConnectAsync(IPEndPoint, Capabilities, byte[]? macAddress, CancellationToken)`
- `SendAsync(ClientMessage, CancellationToken)`
- `ReceiveAsync(CancellationToken)` → `ServerMessage`

**ServerDiscovery** - UDP broadcast discovery
- `DiscoverAsync(string bindAddress, TimeSpan? timeout)` → `Server?`

**StatusData** - Playback state tracking
- Wrapping byte counters, jiffies tracking
- `CreateStatusMessage(StatusCode)` → `StatMessage`

**SlimCodec** - Binary message serialization
- `Encode(ClientMessage)` → `byte[]`
- `Decode(byte[])` → `ServerMessage`

See [ARCHITECTURE.md](ARCHITECTURE.md) for design principles and detailed structure.

## Build & Test

```powershell
dotnet build
dotnet test
```

**Requirements**: .NET, .NET Core 2.0 or higher, .NET Framework 4.6.1 or higher<br />
**Target framework**: .NET Standard 2.0<br />
**Tests**: MSTest + NSubstitute

## Versioning

This project uses [Semantic Versioning](https://semver.org/) with [MinVer](https://github.com/adamralph/minver) for automatic version management based on git tags.

- **Releases**: Tagged as `v{major}.{minor}.{patch}` (e.g., `v0.1.0`)
- **Pre-releases**: Automatic `-alpha.0.{commits}` suffix between releases

## References

- [SlimProto Developer Guide](https://wiki.lyrion.org/index.php/SlimProtoDeveloperGuide.html)
- [Protocol Specification](https://wiki.lyrion.org/index.php/SlimProto_TCP_protocol.html)
- [Rust Reference Implementation](https://github.com/GeoffClements/slim-client-protocol-rs)

## License

[MIT License](LICENSE)
