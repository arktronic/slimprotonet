using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using SlimProtoNet.Protocol.Messages;

namespace SlimProtoNet.Protocol;

/// <summary>
/// Encodes and decodes SlimProto binary messages.
/// </summary>
public class SlimCodec
{
    private const double GAIN_FACTOR = 65536.0;

    /// <summary>
    /// Encodes a client message to binary format.
    /// </summary>
    /// <param name="message">The message to encode.</param>
    /// <returns>Binary representation of the message.</returns>
    public virtual byte[] Encode(ClientMessage message)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        switch (message)
        {
            case HeloMessage helo:
                EncodeHelo(writer, helo);
                break;
            case StatMessage stat:
                EncodeStat(writer, stat);
                break;
            case ByeMessage bye:
                EncodeBye(writer, bye);
                break;
            case SetNameMessage setName:
                EncodeSetName(writer, setName);
                break;
            default:
                throw new InvalidOperationException($"Unknown client message type: {message.GetType().Name}");
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Decodes a server message from binary format.
    /// </summary>
    /// <param name="data">Binary message data including opcode.</param>
    /// <returns>Decoded server message.</returns>
    public virtual ServerMessage Decode(byte[] data)
    {
        if (data.Length < 4)
        {
            throw new InvalidDataException("Message too short - missing opcode");
        }

        var opcode = Encoding.ASCII.GetString(data, 0, 4);
        var payload = new ArraySegment<byte>(data, 4, data.Length - 4);

        return opcode switch
        {
            Constants.SERVER_SERV => DecodeServ(payload),
            Constants.SERVER_STRM => DecodeStrm(payload),
            Constants.SERVER_AUDE => DecodeEnable(payload),
            Constants.SERVER_AUDG => DecodeGain(payload),
            Constants.SERVER_VERS => DecodeVers(payload),
            Constants.SERVER_SETD => DecodeSetd(payload),
            _ => new UnknownServerMessage { Opcode = opcode, RawData = data }
        };
    }

    private void EncodeHelo(BinaryWriter writer, HeloMessage helo)
    {
        // Write opcode
        writer.Write(Encoding.ASCII.GetBytes(Constants.CLIENT_HELO));
        
        var capabilitiesBytes = Encoding.ASCII.GetByteCount(helo.Capabilities.ToString());
        var payloadLength = 1 + 1 + 6 + 16 + 2 + 8 + 2 + capabilitiesBytes;
        
        // Write 4-byte big-endian payload length
        WriteBigEndian(writer, (uint)payloadLength);
        
        // Write payload directly
        writer.Write(helo.DeviceId);
        writer.Write(helo.Revision);
        writer.Write(helo.MacAddress);
        writer.Write(helo.Uuid);
        WriteBigEndian(writer, helo.WlanChannelList);
        WriteBigEndian(writer, helo.BytesReceived);
        foreach (var c in helo.Language)
        {
            writer.Write((byte)c);
        }
        writer.Write(Encoding.ASCII.GetBytes(helo.Capabilities.ToString()));
    }

    private void EncodeStat(BinaryWriter writer, StatMessage stat)
    {
        // Write opcode
        writer.Write(Encoding.ASCII.GetBytes(Constants.CLIENT_STAT));
        
        // STAT payload: 4 event code + 1 crlf + 2 reserved + 6x4 bytes (uint) + 1x8 bytes (ulong) + 3x2 bytes (ushort) = 53 bytes
        const uint payloadLength = 53;
        
        // Write 4-byte big-endian payload length
        WriteBigEndian(writer, payloadLength);
        
        // Write payload directly
        writer.Write(Encoding.ASCII.GetBytes(stat.EventCode));
        
        var data = stat.StatusData;
        writer.Write(data.Crlf);
        WriteBigEndian(writer, (ushort)0); // Reserved field
        WriteBigEndian(writer, data.BufferSize);
        WriteBigEndian(writer, data.Fullness);
        WriteBigEndian(writer, data.BytesReceived);
        WriteBigEndian(writer, data.SignalStrength);
        WriteBigEndian(writer, (uint)data.Jiffies.TotalMilliseconds);
        WriteBigEndian(writer, data.OutputBufferSize);
        WriteBigEndian(writer, data.OutputBufferFullness);
        WriteBigEndian(writer, data.ElapsedSeconds);
        WriteBigEndian(writer, data.Voltage);
        WriteBigEndian(writer, data.ElapsedMilliseconds);
        WriteBigEndian(writer, (uint)data.Timestamp.TotalMilliseconds);
        WriteBigEndian(writer, data.ErrorCode);
    }

    private void EncodeBye(BinaryWriter writer, ByeMessage bye)
    {
        // Write opcode
        writer.Write(Encoding.ASCII.GetBytes(Constants.CLIENT_BYE));
        
        // Build payload (just 1 byte)
        byte[] payload = new byte[] { bye.DisconnectReason };
        
        // Write 4-byte big-endian payload length
        WriteBigEndian(writer, (uint)payload.Length);
        
        // Write payload
        writer.Write(payload);
    }

    private void EncodeSetName(BinaryWriter writer, SetNameMessage setName)
    {
        // Write opcode
        writer.Write(Encoding.ASCII.GetBytes(Constants.CLIENT_SETD));
        
        var nameBytes = Encoding.ASCII.GetByteCount(setName.Name);
        var payloadLength = 1 + nameBytes; // 1 byte tag + name
        
        // Write 4-byte big-endian payload length
        WriteBigEndian(writer, (uint)payloadLength);
        
        // Write payload directly
        writer.Write((byte)0); // Tag
        writer.Write(Encoding.ASCII.GetBytes(setName.Name));
    }

    private ServerMessage DecodeServ(ArraySegment<byte> payload)
    {
        if (payload.Count < 4)
        {
            throw new InvalidDataException("SERV message too short");
        }

        var ipBytes = Slice(payload, 0, 4).ToArray();
        var ipAddress = new IPAddress(ipBytes);

        string? syncGroupId = null;
        if (payload.Count > 4)
        {
            syncGroupId = Encoding.ASCII.GetString(payload.Array, payload.Offset + 4, payload.Count - 4);
        }

        return new ServMessage
        {
            IpAddress = ipAddress,
            SyncGroupId = syncGroupId
        };
    }

    private ServerMessage DecodeStrm(ArraySegment<byte> payload)
    {
        if (payload.Count < 1)
        {
            throw new InvalidDataException("STRM message too short");
        }

        var command = (char)GetByte(payload, 0);
        var data = Slice(payload, 1);

        return command switch
        {
            't' => DecodeStatus(data),
            's' => DecodeStream(data),
            'q' => new StopMessage(),
            'f' => new FlushMessage(),
            'p' => DecodePause(data),
            'u' => DecodeUnpause(data),
            'a' => DecodeSkip(data),
            _ => new UnknownServerMessage { Opcode = $"strm_{command}", RawData = payload.ToArray() }
        };
    }

    private ServerMessage DecodeStatus(ArraySegment<byte> data)
    {
        if (data.Count < 17)
        {
            throw new InvalidDataException("STRM status message too short");
        }

        var timestamp = ReadBigEndianUInt32(data, 13);
        return new StatusRequestMessage
        {
            Interval = TimeSpan.FromMilliseconds(timestamp)
        };
    }

    private ServerMessage DecodeStream(ArraySegment<byte> data)
    {
        if (data.Count < 23)
        {
            throw new InvalidDataException("STRM stream message too short");
        }

        var stream = new StreamMessage();
        var offset = 0;

        stream.AutoStart = (char)GetByte(data, offset++) switch
        {
            '0' => AutoStart.None,
            '1' => AutoStart.Auto,
            '2' => AutoStart.Direct,
            '3' => AutoStart.AutoDirect,
            _ => throw new InvalidDataException("Invalid AutoStart value")
        };

        stream.Format = (char)GetByte(data, offset++) switch
        {
            'p' => Format.Pcm,
            'm' => Format.Mp3,
            'f' => Format.Flac,
            'w' => Format.Wma,
            'o' => Format.Ogg,
            'a' => Format.Aac,
            'l' => Format.Alac,
            _ => throw new InvalidDataException("Invalid Format value")
        };

        stream.PcmSampleSize = (char)GetByte(data, offset++) switch
        {
            '0' => PcmSampleSize.Eight,
            '1' => PcmSampleSize.Sixteen,
            '2' => PcmSampleSize.Twenty,
            '3' => PcmSampleSize.ThirtyTwo,
            '?' => PcmSampleSize.SelfDescribing,
            _ => throw new InvalidDataException("Invalid PcmSampleSize value")
        };

        stream.PcmSampleRate = (char)GetByte(data, offset++) switch
        {
            '0' => PcmSampleRate.Rate11000,
            '1' => PcmSampleRate.Rate22000,
            '2' => PcmSampleRate.Rate32000,
            '3' => PcmSampleRate.Rate44100,
            '4' => PcmSampleRate.Rate48000,
            '5' => PcmSampleRate.Rate8000,
            '6' => PcmSampleRate.Rate12000,
            '7' => PcmSampleRate.Rate16000,
            '8' => PcmSampleRate.Rate24000,
            '9' => PcmSampleRate.Rate96000,
            '?' => PcmSampleRate.SelfDescribing,
            _ => throw new InvalidDataException("Invalid PcmSampleRate value")
        };

        stream.PcmChannels = (char)GetByte(data, offset++) switch
        {
            '1' => PcmChannels.Mono,
            '2' => PcmChannels.Stereo,
            '?' => PcmChannels.SelfDescribing,
            _ => throw new InvalidDataException("Invalid PcmChannels value")
        };

        stream.PcmEndian = (char)GetByte(data, offset++) switch
        {
            '0' => PcmEndian.Big,
            '1' => PcmEndian.Little,
            '?' => PcmEndian.SelfDescribing,
            _ => throw new InvalidDataException("Invalid PcmEndian value")
        };

        stream.Threshold = (uint)GetByte(data, offset++) * 1024u;

        stream.SpdifEnable = GetByte(data, offset++) switch
        {
            0 => SpdifEnable.Auto,
            1 => SpdifEnable.On,
            2 => SpdifEnable.Off,
            _ => throw new InvalidDataException("Invalid SpdifEnable value")
        };

        stream.TransitionPeriod = TimeSpan.FromSeconds(GetByte(data, offset++));

        stream.TransitionType = (char)GetByte(data, offset++) switch
        {
            '0' => TransType.None,
            '1' => TransType.Crossfade,
            '2' => TransType.FadeIn,
            '3' => TransType.FadeOut,
            '4' => TransType.FadeInOut,
            _ => throw new InvalidDataException("Invalid TransType value")
        };

        stream.Flags = (StreamFlags)GetByte(data, offset++);

        stream.OutputThreshold = TimeSpan.FromMilliseconds(GetByte(data, offset++) * 10);

        offset++; // Reserved byte

        stream.ReplayGain = ReadBigEndianUInt32(data, offset) / GAIN_FACTOR;
        offset += 4;

        stream.ServerPort = ReadBigEndianUInt16(data, offset);
        offset += 2;

        var ipBytes = Slice(data, offset, 4).ToArray();
        stream.ServerIp = new IPAddress(ipBytes);
        offset += 4;

        if (data.Count > offset)
        {
            stream.HttpHeaders = Encoding.ASCII.GetString(data.Array, data.Offset + offset, data.Count - offset);
        }

        return stream;
    }

    private ServerMessage DecodePause(ArraySegment<byte> data)
    {
        if (data.Count < 17)
        {
            throw new InvalidDataException("STRM pause message too short");
        }

        var timestamp = ReadBigEndianUInt32(data, 13);
        return new PauseMessage
        {
            Timestamp = TimeSpan.FromMilliseconds(timestamp)
        };
    }

    private ServerMessage DecodeUnpause(ArraySegment<byte> data)
    {
        if (data.Count < 17)
        {
            throw new InvalidDataException("STRM unpause message too short");
        }

        var timestamp = ReadBigEndianUInt32(data, 13);
        return new UnpauseMessage
        {
            Timestamp = TimeSpan.FromMilliseconds(timestamp)
        };
    }

    private ServerMessage DecodeSkip(ArraySegment<byte> data)
    {
        if (data.Count < 17)
        {
            throw new InvalidDataException("STRM skip message too short");
        }

        var timestamp = ReadBigEndianUInt32(data, 13);
        return new SkipMessage
        {
            Timestamp = TimeSpan.FromMilliseconds(timestamp)
        };
    }

    private ServerMessage DecodeEnable(ArraySegment<byte> payload)
    {
        if (payload.Count < 2)
        {
            throw new InvalidDataException("AUDE message too short");
        }

        return new EnableMessage
        {
            SpdifEnabled = GetByte(payload, 0) != 0,
            DacEnabled = GetByte(payload, 1) != 0
        };
    }

    private ServerMessage DecodeGain(ArraySegment<byte> payload)
    {
        if (payload.Count < 18)
        {
            throw new InvalidDataException("AUDG message too short");
        }

        var leftGain = ReadBigEndianUInt32(payload, 10) / GAIN_FACTOR;
        var rightGain = ReadBigEndianUInt32(payload, 14) / GAIN_FACTOR;

        return new GainMessage
        {
            LeftGain = leftGain,
            RightGain = rightGain
        };
    }

    private ServerMessage DecodeSetd(ArraySegment<byte> payload)
    {
        if (payload.Count == 0)
        {
            throw new InvalidDataException("SETD message too short");
        }

        var id = GetByte(payload, 0);
        var data = Slice(payload, 1);

        switch (id)
        {
            case 0:
                if (data.Count == 0)
                {
                    return new QueryNameMessage();
                }
                else
                {
                    var name = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count - 1);
                    return new SetNameRequestMessage { Name = name };
                }

            case 4:
                return new DisableDacMessage();

            default:
                return new UnknownServerMessage
                {
                    Opcode = $"setd_{id}",
                    RawData = payload.ToArray()
                };
        }
    }

    private ServerMessage DecodeVers(ArraySegment<byte> payload)
    {
        var version = Encoding.ASCII.GetString(payload.Array, payload.Offset, payload.Count);
        return new VersMessage { Version = version };
    }

    private void WriteBigEndian(BinaryWriter writer, ushort value)
    {
        writer.Write((byte)(value >> 8));
        writer.Write((byte)(value & 0xFF));
    }

    private void WriteBigEndian(BinaryWriter writer, uint value)
    {
        writer.Write((byte)(value >> 24));
        writer.Write((byte)((value >> 16) & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    private void WriteBigEndian(BinaryWriter writer, ulong value)
    {
        writer.Write((byte)(value >> 56));
        writer.Write((byte)((value >> 48) & 0xFF));
        writer.Write((byte)((value >> 40) & 0xFF));
        writer.Write((byte)((value >> 32) & 0xFF));
        writer.Write((byte)((value >> 24) & 0xFF));
        writer.Write((byte)((value >> 16) & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    private ushort ReadBigEndianUInt16(ArraySegment<byte> data, int offset)
    {
        return (ushort)((GetByte(data, offset) << 8) | GetByte(data, offset + 1));
    }

    private uint ReadBigEndianUInt32(ArraySegment<byte> data, int offset)
    {
        return (uint)((data.Array[data.Offset + offset] << 24) |
                     (data.Array[data.Offset + offset + 1] << 16) |
                     (data.Array[data.Offset + offset + 2] << 8) |
                     data.Array[data.Offset + offset + 3]);
    }

    private byte GetByte(ArraySegment<byte> data, int index)
    {
        return data.Array[data.Offset + index];
    }

    private ArraySegment<byte> Slice(ArraySegment<byte> data, int start)
    {
        return new ArraySegment<byte>(data.Array, data.Offset + start, data.Count - start);
    }

    private ArraySegment<byte> Slice(ArraySegment<byte> data, int start, int length)
    {
        return new ArraySegment<byte>(data.Array, data.Offset + start, length);
    }
}
