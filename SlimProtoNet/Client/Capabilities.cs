using System;
using System.Collections.Generic;
using System.Linq;

namespace SlimProtoNet.Client;

/// <summary>
/// Client capability types that can be negotiated with LMS.
/// </summary>
public enum Capability
{
    Wma, // Windows Media Audio
    Wmap, // Windows Media Audio Pro
    Wmal, // Windows Media Audio Lossless
    Ogg,
    Flc, // FLAC
    Pcm,
    Aif,
    Mp3,
    Alc, // ALAC - Apple Lossless
    Aac,
    MaxSampleRate,
    Model,
    ModelName,
    Rhap, // Rhapsody
    AccuratePlayPoints, // for synchronization without additional filtering
    SyncgroupID,
    HasDigitalOut,
    HasPreAmp,
    HasDisableDAC,
    Firmware,
    Balance,
    CanHTTPS,
}

/// <summary>
/// Represents a capability type with an optional value.
/// Supports both predefined capabilities (enum-based) and custom capabilities (string-based).
/// </summary>
public class CapabilityValue : IEquatable<CapabilityValue>
{
    /// <summary>
    /// The capability type (null for custom capabilities).
    /// </summary>
    public Capability? Type { get; }

    /// <summary>
    /// Custom capability name (null for predefined capabilities).
    /// </summary>
    public string? CustomName { get; }

    /// <summary>
    /// Optional value for capabilities that require parameters.
    /// </summary>
    public string? Value { get; }

    /// <summary>
    /// True if this is a custom capability, false if it's a predefined enum capability.
    /// </summary>
    public bool IsCustom => CustomName != null;

    /// <summary>
    /// Creates a new predefined capability value.
    /// </summary>
    /// <param name="type">The capability type.</param>
    /// <param name="value">Optional value for capabilities that require parameters.</param>
    public CapabilityValue(Capability type, string? value = null)
    {
        Type = type;
        CustomName = null;
        Value = value;
    }

    /// <summary>
    /// Creates a new custom capability.
    /// </summary>
    /// <param name="customName">The custom capability string (e.g., "mycodec", "feature=1", "CustomFeature=value").</param>
    public CapabilityValue(string customName)
    {
        if (string.IsNullOrWhiteSpace(customName))
            throw new ArgumentException("Custom capability string cannot be null or whitespace.", nameof(customName));

        Type = null;
        CustomName = customName;
        Value = null;
    }

    /// <summary>
    /// Implicitly converts a Capability to a CapabilityValue with no value.
    /// Enables cleaner syntax: capabilities.Add(Capability.Pcm) instead of capabilities.Add(new CapabilityValue(Capability.Pcm)).
    /// </summary>
    public static implicit operator CapabilityValue(Capability type) => new(type);

    public override string ToString()
    {
        if (IsCustom)
        {
            return CustomName!;
        }

        return Type switch
        {
            Capability.Wma => "wma",
            Capability.Wmap => "wmap",
            Capability.Wmal => "wmal",
            Capability.Ogg => "ogg",
            Capability.Flc => "flc",
            Capability.Pcm => "pcm",
            Capability.Aif => "aif",
            Capability.Mp3 => "mp3",
            Capability.Alc => "alc",
            Capability.Aac => "aac",
            Capability.MaxSampleRate => $"MaxSampleRate={Value}",
            Capability.Model => $"Model={Value}",
            Capability.ModelName => $"ModelName={Value}",
            Capability.Rhap => "Rhap",
            Capability.AccuratePlayPoints => "AccuratePlayPoints=1",
            Capability.SyncgroupID => $"SyncgroupID={Value}",
            Capability.HasDigitalOut => "HasDigitalOut=1",
            Capability.HasPreAmp => "HasPreAmp=1",
            Capability.HasDisableDAC => "HasDisableDac=1",
            Capability.Firmware => $"Firmware={Value}",
            Capability.Balance => "Balance=1",
            Capability.CanHTTPS => "CanHTTPS=1",
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public bool Equals(CapabilityValue? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Type == other.Type && CustomName == other.CustomName && Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as CapabilityValue);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (Type?.GetHashCode() ?? 0);
            hash = hash * 31 + (CustomName?.GetHashCode() ?? 0);
            hash = hash * 31 + (Value?.GetHashCode() ?? 0);
            return hash;
        }
    }
}

/// <summary>
/// Collection of client capabilities to be negotiated with LMS.
/// Defaults include common Squeezelite capabilities.
/// </summary>
public class Capabilities
{
    private readonly List<CapabilityValue> _capabilities = new List<CapabilityValue>();

    /// <summary>
    /// Creates a new capabilities collection with default Squeezelite capabilities.
    /// </summary>
    public Capabilities() : this(true)
    {
    }

    /// <summary>
    /// Creates a new capabilities collection.
    /// </summary>
    /// <param name="addDefaults">If true, adds default Squeezelite capabilities.</param>
    public Capabilities(bool addDefaults)
    {
        if (addDefaults)
        {
            // Default to most likely capabilities for a Squeezelite client
            _capabilities.Add(new CapabilityValue(Capability.Model, "squeezelite"));
            _capabilities.Add(new CapabilityValue(Capability.ModelName, "SqueezeLite"));
            _capabilities.Add(new CapabilityValue(Capability.AccuratePlayPoints));
            _capabilities.Add(new CapabilityValue(Capability.HasDigitalOut));
            _capabilities.Add(new CapabilityValue(Capability.HasPreAmp));
            _capabilities.Add(new CapabilityValue(Capability.HasDisableDAC));
        }
    }

    /// <summary>
    /// Adds or updates a capability. If a predefined capability type already exists, it is replaced.
    /// Custom capabilities are not deduplicated.
    /// </summary>
    /// <param name="capability">The capability to add.</param>
    public virtual void Add(CapabilityValue capability)
    {
        if (capability == null)
            throw new ArgumentNullException(nameof(capability));

        // Only deduplicate predefined capabilities (not custom ones)
        if (!capability.IsCustom)
        {
            int index = _capabilities.FindIndex(c => !c.IsCustom && c.Type == capability.Type);
            if (index >= 0)
            {
                _capabilities.RemoveAt(index);
            }
        }

        _capabilities.Add(capability);
    }

    /// <summary>
    /// Sets the model name capability.
    /// </summary>
    /// <param name="name">The model name to advertise.</param>
    public virtual void AddName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or whitespace.", nameof(name));

        Add(new CapabilityValue(Capability.ModelName, name));
    }

    public override string ToString()
    {
        return string.Join(",", _capabilities.Select(c => c.ToString()));
    }
}
