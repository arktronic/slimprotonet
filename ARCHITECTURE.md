# SlimProtoNet Architecture

.NET Standard 2.0 library implementing the SlimProto TCP protocol for building Squeezebox-compatible clients.

## Namespace Structure

```
SlimProtoNet.Protocol/
├── Messages/
│   ├── ClientMessage.cs           - Client→server messages
│   ├── ServerMessage.cs           - Server→client messages
│   └── MessageEnums.cs            - Protocol enums (AutoStart, Format, PcmSampleSize, etc.)
├── SlimCodec.cs                   - Binary message serialization/deserialization
├── StreamExtensions.cs            - Binary read/write helpers for Stream
└── Constants.cs                   - Protocol constants (SLIM_PORT = 3483, opcodes)

SlimProtoNet.Client/
├── SlimClient.cs                  - TCP connection manager and message routing
├── Capabilities.cs                - Client capability definitions
├── StatusData.cs                  - Playback state tracking with wrapping counters
└── StatusCode.cs                  - Status event codes enum

SlimProtoNet.Discovery/
├── ServerDiscovery.cs             - UDP broadcast-based server discovery
└── Server.cs                      - Server result object

SlimProtoNet.Wrappers/
├── StopwatchWrapper.cs            - Mockable wrapper for Stopwatch
├── TcpClientWrapper.cs            - Mockable wrapper for TcpClient
└── TcpClientFactory.cs            - Factory for creating TcpClientWrapper instances
```


## Design Principles

### Message Type Hierarchy
- **Abstract base classes** (`ClientMessage`, `ServerMessage`) for all protocol messages
- Concrete message types derive from bases (e.g., `HeloMessage : ClientMessage`)
- All types non-sealed for extensibility and test subclassing
- Unknown opcodes handled gracefully with `UnknownServerMessage`

### Binary Serialization
**SlimCodec** handles wire format conversion:
- `virtual byte[] Encode(ClientMessage msg)` - serialize to bytes
- `virtual ServerMessage Decode(byte[] data)` - deserialize from bytes
- Big-endian byte order for all multi-byte integers
- Virtual methods enable mocking without interfaces

### Connection Management
**SlimClient** manages TCP lifecycle:
- `virtual Task ConnectAsync(IPEndPoint, Capabilities, byte[]? macAddress, CancellationToken)`
- `virtual Task SendAsync(ClientMessage, CancellationToken)` - encode and send
- `virtual Task<ServerMessage> ReceiveAsync(CancellationToken)` - receive and decode
- Length-prefixed framing (2-byte big-endian length header) for messages from server
- No automatic reconnection - caller responsibility

### Status Tracking
**StatusData** maintains playback state:
- Wrapping byte counters for network traffic (`unchecked` arithmetic)
- Jiffies (uptime) tracked via `StopwatchWrapper`

## Testability

**Virtual methods** for mockability:
- All public API methods marked `virtual` by default
- Enables NSubstitute mocking without interface proliferation
- Concrete classes preferred over interfaces

**Wrapper classes** for unmockable BCL types:
- `StopwatchWrapper` - wraps `Stopwatch` for deterministic timing in tests
- `TcpClientWrapper` - wraps `TcpClient` for network I/O mocking
- `TcpClientFactory` - creates `TcpClientWrapper` instances

**Dependency injection**:
- Constructor injection for all dependencies
- Default implementations provided for production use
- Tests inject mocks via constructor

## Error Handling

- **Network errors**: Propagate `IOException`, `SocketException` naturally
- **Protocol errors**: Throw `InvalidDataException` for malformed bytes
- **Unknown opcodes**: Return `UnknownServerMessage` (don't throw)
- **Connection loss**: Graceful cleanup, no automatic recovery

## Thread Safety

- **SlimClient**: Not thread-safe - caller synchronizes send/receive
- **StatusData**: Not thread-safe - single-threaded update pattern
- **SlimCodec**: Stateless - safe for concurrent use
- **ServerDiscovery**: Thread-safe for concurrent discovery calls

## Dependencies

**Production**: .NET Standard 2.0 BCL only (zero external packages)
**Tests**: MSTest + NSubstitute
